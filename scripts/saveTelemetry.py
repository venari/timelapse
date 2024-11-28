import subprocess
import json
import pijuice
import os
import time
import shutil
import datetime
import sys
import logging
# from logging.handlers import TimedRotatingFileHandler
from logging.handlers import SocketHandler
import pathlib
import glob

from helpers import flashLED

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
intentLogFilePath = logFilePath.replace("timelapse.log", "intent.log")
logFilePath = logFilePath.replace(".log", ".saveTelemetry.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)
# os.chmod(os.path.dirname(logFilePath), 0o777) # Make sure pijuice user scrip can write to log file.


formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
# handler = TimedRotatingFileHandler(logFilePath, 
#                                    when='midnight',
#                                    backupCount=10)
handler = SocketHandler('localhost', 8000)
handler.setFormatter(formatter)
logger = logging.getLogger("saveTelemetry")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

handlerIntent = logging.FileHandler(intentLogFilePath)
handlerIntent.setFormatter(formatter)
loggerIntent = logging.getLogger("intent")
loggerIntent.addHandler(handlerIntent)
loggerIntent.setLevel(logging.DEBUG)

logger.info("Starting up saveTelemetry.py...")
loggerIntent.info("Starting up saveTelemetry.py...")
# os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.

# clock
while not os.path.exists('/dev/i2c-1'):
    logger.info("dev i2c-1 doesn't exist")
    time.sleep(0.1)

outputImageFolder = '../output/images/'
workingImageFolder = outputImageFolder + 'working/'
pendingImageFolder = outputImageFolder + 'pending/'
uploadedImageFolder = outputImageFolder + 'uploaded/'

outputTelemetryFolder = '../output/telemetry/'
pendingTelemetryFolder = outputTelemetryFolder + 'pending/'
uploadedTelemetryFolder = outputTelemetryFolder + 'uploaded/'

# pijuice
time.sleep(10)
pj=None
# Assign PiJuice but handle error if not connected
try:
    pj = pijuice.PiJuice(1, 0x14)
except:
    logger.error("PiJuice not connected - PiJuice functionality will not be available")

if pj is None or pj.status.GetStatus()['error'] == 'COMMUNICATION_ERROR':
    logger.info('PiJuice not connected')
else:
    logger.info('PiJuice is connected')


logger.info("Starting up saveTelemetry.py 3b...")

def getSerialNumber():
  # Extract serial from cpuinfo file
  cpuserial = "0000000000000000"
  try:
    f = open('/proc/cpuinfo','r')
    for line in f:
      if line[0:6]=='Serial':
        cpuserial = line[10:26]
    f.close()
  except:
    cpuserial = "ERROR000000000"

  return cpuserial

serialNumber = getSerialNumber()

def scheduleShutdown():
    try:
        if pj is None or pj.status.GetStatus()['error'] == 'COMMUNICATION_ERROR':
            logger.info('PiJuice not connected')
            return
        
        alarmObj = {}

        # print(str(datetime.datetime.now()) + ' scheduleShutdown')
        logger.debug('scheduleShutdown')
        logger.debug('rtcAlarm.GetControlStatus(): ' + str(pj.rtcAlarm.GetControlStatus()))
        logger.debug('rtcAlarm.GetTime(): ' + str(pj.rtcAlarm.GetTime()))

        setAlarm = False
        triggerRestart = False

        config = json.load(open('config.json'))

        # if config['shutdown']:
        #     # print(str(datetime.datetime.now()) + ' scheduling regular shutdown')
        #     logger.info('scheduling regular shutdown')
        #     DELTA_MIN=10

        #     alarmObj = {
        #             'year': 'EVERY_YEAR',
        #             'month': 'EVERY_MONTH',
        #             'day': 'EVERY_DAY',
        #             'hour': 'EVERY_HOUR',
        #             'minute_period': DELTA_MIN,
        #             'second': 0,
        #     }

        #     setAlarm = True


        uptimeSeconds = int(time.clock_gettime(time.CLOCK_BOOTTIME))

        bCharging = False
        if (
            (pj.status.GetStatus()['data']['battery'] == 'CHARGING_FROM_IN' 
            or pj.status.GetStatus()['data']['battery'] == 'CHARGING_FROM_5V_IO' )
            and  pj.status.GetStatus()['data']['powerInput'] == 'PRESENT'
        ):
            bCharging = True

        if config['sleep_during_night'] == True and (datetime.datetime.now().hour >= config['daytime_ends_at_h'] or datetime.datetime.now().hour < config['daytime_starts_at_h']):
            if config['supportMode'] == True:
                logger.warning("Night time - we would have scheduled shutdown, but we're in support mode.")

            if bCharging:
                logger.info("Night time - but we're charging/powered, so we'll stay on.")

        # Has battery been disconnected, and RTC reset? Go into support mode until we connect and reset RTC.
        if (pj.rtcAlarm.GetTime()['data']['year'] == 2000):
                logger.warning('Looks like RTC has been reset - going into support mode until we reconnect.')
                loggerIntent.warning('Looks like RTC has been reset - going into support mode until we reconnect.')
                config['supportMode'] = True
                json.dump(config, open('config.json', 'w'), indent=4)


        # Hibernate mode? 
        if config['hibernateMode']:
            # If we've awoken from hibernate - let's check it's within 5 minutes of the hour, or if hour is other than 0, 6, 12, or 18.
            # If not, user may have pressed button - let's switch out of hibernate mode.
            # 
            if uptimeSeconds < 300 and (pj.rtcAlarm.GetTime()['data']['minute'] > 5 or pj.rtcAlarm.GetTime()['data']['hour'] % 6 != 0):
                logger.info('hibernate mode - but looks like we have been woken by user - switching out of hibernate mode.')
                loggerIntent.info('hibernate mode - but looks like we have been woken by user - switching out of hibernate mode, and into support mode.')
                flashLED(pj, 'D2', 255, 0, 0, 2, 1)
                flashLED(pj, 'D2', 0, 255, 0, 2, 1)
                flashLED(pj, 'D2', 0, 0, 255, 2, 1)
                pj.status.SetLedState('D2', [0, 0, 0])
                config['hibernateMode'] = False
                config['supportMode'] = True
                json.dump(config, open('config.json', 'w'), indent=4)


        # Power Off mode? 
        if config['powerOff']:
            # If we've awoken from Power Off, let's switch out of Power Off 
            # 
            if uptimeSeconds < 300 :
                logger.info('Power Off - but looks like we have been woken by user - switching out of Power Off.')
                loggerIntent.info('Power Off - but looks like we have been woken by user - switching out of Power Off.')
                flashLED(pj, 'D2', 255, 0, 0, 2, 1)
                flashLED(pj, 'D2', 0, 255, 0, 2, 1)
                flashLED(pj, 'D2', 0, 0, 255, 2, 1)
                pj.status.SetLedState('D2', [0, 0, 0])
                config['hibernateMode'] = False
                config['powerOff'] = False
                config['supportMode'] = True
                json.dump(config, open('config.json', 'w'), indent=4)

            else:

                # Otherwise, let's cancel wake up alarms, watchdog, and power down.
                logger.info('Power Off - cancelling wake up alarms, watchdog, and power down.')
                loggerIntent.info('Power Off - cancelling wake up alarms, watchdog, and power down.')
                pj.rtcAlarm.SetWakeupEnabled(False)
                SetWatchdog(0)
                
                loggerIntent.info('Power off scheduled for 1 min from now')
                pj.power.SetPowerOff(60)
                logger.info('Setting System Power Switch to Off:')
                pj.power.SetSystemPowerSwitch(0)
                powerDownSIM7600X()
                logger.info('Shutting down now...')
                loggerIntent.info('Shutting down now...')
                subprocess.call(['sudo', 'shutdown', '-h', 'now'])
                
                # Exit to make sure we don't than go and undo the power off!
                return


        # If hibernating, wake up at next 6 hourly interval
        # e.g. midnight, 6am, 12pm, 6pm (UTC)
        hibernateHoursToWakeAfter = 6 - (datetime.datetime.utcnow().hour % 6)
        hibernateHourToWakeAt = datetime.datetime.utcnow().hour + hibernateHoursToWakeAfter

        # loggerIntent.info('datetime.datetime.utcnow().hour')
        # loggerIntent.info(datetime.datetime.utcnow().hour)
        # loggerIntent.info('hoursToWakeAfter')
        # loggerIntent.info(hoursToWakeAfter)
        # loggerIntent.info('hourToWakeAt')
        # loggerIntent.info(hourToWakeAt)

        if hibernateHourToWakeAt >= 24:
            hibernateHourToWakeAt = hibernateHourToWakeAt - 24


        # Hibernate mode? Lets have 10 minutes to give it a chance to check again before hibernating.
        if config['hibernateMode']:
            logger.info('hibernate mode - stay awake for 10 mins')
            loggerIntent.info('hibernate mode - stay awake for 10 mins')
            if uptimeSeconds > 600:
                logger.info('hibernate mode - sleeping for 6 hours...')
                loggerIntent.info('hibernate mode - sleeping for 6 hours...')

                alarmObj = {
                    'year': 'EVERY_YEAR',
                    'month': 'EVERY_MONTH',
                    'day': 'EVERY_DAY',
                    'hour': hibernateHourToWakeAt,
                    'minute': 0,
                    'second': 0,
                }

                setAlarm = True

                # Set watchdog to one day just to catch wakeup alarm failure
                SetWatchdog(60*24)

        else:

            if config['sleep_during_night'] == True \
                and (datetime.datetime.now().hour >= config['daytime_ends_at_h'] or datetime.datetime.now().hour < config['daytime_starts_at_h']) \
                and config['supportMode'] == False \
                and datetime.datetime.now().minute >= 10 \
                and bCharging == False:
                logger.info("Night time so we're scheduling shutdown")
                loggerIntent.info("Night time so we're scheduling shutdown")

                alarmObj = {
                    'year': 'EVERY_YEAR',
                    'month': 'EVERY_MONTH',
                    'day': 'EVERY_DAY',
                    # 'hour': 20, # 8am
                    # 'minute_period': DELTA_MIN,
                    'hour': 'EVERY_HOUR',
                    'minute': 0,
                    'second': 0,
                }

                setAlarm = True

                # Set Watchdog to 2 hours to catch wakeup failure
                SetWatchdog(120)

            else:

                # sleep_at_battery_percent - at this battery percentage, we go to sleep and wake up every 10 minutes.
                # pijuice_config.JSON.system_task.min_charge.threshold - at this battery percentage, the pi_juice min_charge setting puts us to sleep until battery gets to wakeup_on_charge value,
                # so we let this setting take precedence via the pijuice_config.JSON file and don't set an alarm here.
                # We'll also pre-empt it by 5% to try and shut down more gracefully than the PiJuice might do.

                # Also let's give it a chance to upload once an hour to catch up and avoid anxiety that camera has been stolen

                if config['sleep_at_battery_percent'] > 0 and config['pijuice_config.JSON.system_task.min_charge.threshold'] > 0 \
                and pj.status.GetChargeLevel()['data'] <= config['sleep_at_battery_percent'] \
                and pj.status.GetStatus()['data']['battery'] != 'NOT_PRESENT' \
                and datetime.datetime.now().minute >= 10 \
                and config['supportMode'] == False \
                and bCharging == False:

                    if pj.status.GetChargeLevel()['data'] > config['pijuice_config.JSON.system_task.min_charge.threshold'] + 5:
                        logger.info('scheduling 10 minute sleep due to low battery')
                        loggerIntent.info('scheduling 10 minute sleep due to low battery')
                        logger.info(pj.status.GetChargeLevel())
                        logger.info(pj.status.GetStatus())
                        DELTA_MIN=10

                        time.sleep(30)

                        alarmObj = {
                                'year': 'EVERY_YEAR',
                                'month': 'EVERY_MONTH',
                                'day': 'EVERY_DAY',
                                'hour': 'EVERY_HOUR',
                                'minute_period': DELTA_MIN,
                                'second': 0,
                        }

                        # Set watchdog to 15 mins to catch wakeup alarm failure
                        SetWatchdog(15)

                    # If we're down at hibernate level, let's just hibernate.
                    else:
                        logger.info('Hibernating due to very low battery')
                        loggerIntent.info('Hibernating 10 minute sleep due to very low battery')
                        logger.info(pj.status.GetChargeLevel())
                        logger.info(pj.status.GetStatus())
                        alarmObj = {
                            'year': 'EVERY_YEAR',
                            'month': 'EVERY_MONTH',
                            'day': 'EVERY_DAY',
                            'hour': hibernateHourToWakeAt,
                            'minute': 0,
                            'second': 0,
                        }
                        # Set watchdog to 9 hours just to catch wakeup alarm failure
                        SetWatchdog(60*9)

                    setAlarm = True

                else:

                    # If we've been up for more than 2 modem cycles or 30 minutes, and the most recently captured image is older than 10 minutes, or the most recently uploaded image is older than 30 minutes, 
                    # either network is out, or we can't get a cellular signal, DNS is messing around, or camera isn't capturing.
                    # Let's shutdown, power down, and wake up again in 3 mins to see if that fixes it.
                    power_interval = config['modem.power_interval']
                    
                    if uptimeSeconds > power_interval * 2 and uptimeSeconds > 1800:
                        mostRecentUploadedFiles = sorted(glob.iglob(uploadedImageFolder + "/*.*"), key=os.path.getctime, reverse=True)
                        mostRecentPendingFiles = sorted(glob.iglob(pendingImageFolder + "/*.*"), key=os.path.getctime, reverse=True)

                        secondsSinceLastUpload = -1
                        secondsSinceLastImageCapture = -1

                        if len(mostRecentPendingFiles) > 0:
                            latestImageCapturedFilename = max(mostRecentPendingFiles, key=os.path.getctime)
                            secondsSinceLastImageCapture = (datetime.datetime.now() - datetime.datetime.fromtimestamp(os.path.getctime(latestImageCapturedFilename))).total_seconds()
                            logger.debug("secondsSinceLastImageCapture: " + str(secondsSinceLastImageCapture))

                        if len(mostRecentUploadedFiles) > 0:

                            latestUploadedFilename = max(mostRecentUploadedFiles, key=os.path.getctime)
                            # logger.debug("latestUploadedFilename: " + str(latestUploadedFilename))

                            secondsSinceLastUpload = (datetime.datetime.now() - datetime.datetime.fromtimestamp(os.path.getctime(latestUploadedFilename))).total_seconds()
                            logger.debug("secondsSinceLastUpload: " + str(secondsSinceLastUpload))


                        # Most recent image captured (may also be in uploaded folder) is older than 10 minutes
                        if secondsSinceLastImageCapture > 600 and secondsSinceLastUpload > 600:
                            logger.warning('Most recent captured image is ' + str(secondsSinceLastImageCapture) + 'seconds old, and uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            loggerIntent.warning('Most recent captured image is ' + str(secondsSinceLastImageCapture) + 'seconds old, and uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            triggerRestart = True

                        if secondsSinceLastUpload > 1800:
                            logger.warning('Most recent uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            loggerIntent.warning('Most recent uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            triggerRestart = True

                        if len(mostRecentPendingFiles) == 0 and len(mostRecentUploadedFiles) == 0:
                            logger.debug("No uploaded or captured images found - restarting...")
                            loggerIntent.debug("No uploaded or captured images found - restarting...")
                            triggerRestart = True

                        if triggerRestart:
                            minsToWakeAfter = 3
                            minToWakeAt = datetime.datetime.now().minute + minsToWakeAfter
                            if minToWakeAt >= 60:
                                minToWakeAt = minToWakeAt - 60

                            alarmObj = {
                                    'year': 'EVERY_YEAR',
                                    'month': 'EVERY_MONTH',
                                    'day': 'EVERY_DAY',
                                    'hour': 'EVERY_HOUR',
                                    # 'minute_period': DELTA_MIN,
                                    'minute': minToWakeAt,
                                    'second': 0,
                            }

                            setAlarm = True

        if setAlarm == True:
            logger.info("scheduleShutdown - we're setting the shutdown...")
            loggerIntent.info("scheduleShutdown - we're setting the shutdown...")

            alarmSet = False
            while alarmSet == False:
                status = pj.rtcAlarm.SetAlarm(alarmObj)

                if status['error'] != 'NO_ERROR':
                    logger.error('Cannot set alarm\n')
                    # sys.exit()
                    alarmSet = False
                    logger.info('Sleeping and retrying...\n')
                    time.sleep(10)
                else:
                    logger.debug('Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))
                    loggerIntent.debug('Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))
                    alarmSet = True

            # Ensure Wake up alarm is actually enabled!
            wakeUpEnabled = False
            while wakeUpEnabled == False:

                status = pj.rtcAlarm.SetWakeupEnabled(True)

                if status['error'] != 'NO_ERROR':
                    logger.error('Cannot enable wakeup\n')
                    # sys.exit()
                    wakeUpEnabled = False
                    logger.info('Sleeping and retrying for wakeup...\n')
                    time.sleep(10)
                else:
                    logger.debug('Wakeup enabled')
                    loggerIntent.debug('Wakeup enabled')
                    wakeUpEnabled = True

            logger.debug('rtcAlarm.GetControlStatus(): ' + str(pj.rtcAlarm.GetControlStatus()))
            logger.debug('rtcAlarm.GetTime(): ' + str(pj.rtcAlarm.GetTime()))

            logger.debug('Clearing Alarm Flag...')
            pj.rtcAlarm.ClearAlarmFlag()
            logger.debug('rtcAlarm.GetControlStatus(): ' + str(pj.rtcAlarm.GetControlStatus()))
            logger.debug('rtcAlarm.GetTime(): ' + str(pj.rtcAlarm.GetTime()))
            loggerIntent.debug('rtcAlarm.GetControlStatus(): ' + str(pj.rtcAlarm.GetControlStatus()))
            loggerIntent.debug('rtcAlarm.GetTime(): ' + str(pj.rtcAlarm.GetTime()))
            loggerIntent.debug('power.GetWatchdog(): ' + str(pj.power.GetWatchdog()))

            if triggerRestart:
                logger.info('Restart scheduled for ' + str(minsToWakeAfter) + ' minutes from now')
                logger.info("So we'll skip the power off.")
            else:
                logger.info('Power off scheduled for 1 min from now')
                loggerIntent.info('Power off scheduled for 1 min from now')
                pj.power.SetPowerOff(60)
        
            logger.info('Setting System Power Switch to Off:')
            pj.power.SetSystemPowerSwitch(0)
            powerDownSIM7600X()
            logger.info('Shutting down now...')
            loggerIntent.info('Shutting down now...')
            subprocess.call(['sudo', 'shutdown', '-h', 'now'])
        else:
            # Ensure Wake up alarm is *not* enabled - or it will cause pi to reboot
            wakeUpCancelled = False
            while wakeUpCancelled == False:

                status = pj.rtcAlarm.SetWakeupEnabled(False)

                if status['error'] != 'NO_ERROR':
                    logger.error('Cannot cancel wakeup\n')
                    # sys.exit()
                    wakeUpCancelled = False
                    logger.info('Sleeping and retrying to cancel wakeup...\n')
                    time.sleep(10)
                else:
                    # logger.debug('Wakeup cancelled')
                    # loggerIntent.debug('Wakeup cancelled')
                    wakeUpCancelled = True

        #     SetSafetyWakeup()

    except Exception as e:
        logger.error("scheduleShutdown() failed.")
        logger.error(e)

def SetWatchdog(timeout = 3, non_volatile = False):
    try:
        if pj is None or pj.status.GetStatus()['error'] == 'COMMUNICATION_ERROR':
            logger.info('PiJuice not connected')
            return

        if(pj.power.GetWatchdog()['data'] == timeout and pj.power.GetWatchdog()['non_volatile'] == non_volatile):
            return
        
        logger.debug('Setting Watchdog...')
        loggerIntent.debug('Setting Watchdog...')

        watchdogSet = False
        while watchdogSet == False:
            status = pj.power.SetWatchdog(timeout, non_volatile)

            if status['error'] != 'NO_ERROR':
                logger.error('Cannot set watchdog\n')
                watchdogSet = False
                logger.info('Sleeping and retrying...\n')
                time.sleep(10)
            else:
                logger.debug('Watchdog set for ' + str(pj.power.GetWatchdog()))
                loggerIntent.debug('Watchdog set for ' + str(pj.power.GetWatchdog()))
                watchdogSet = True

    except Exception as e:
        logger.error("SetWatchdog() failed.")
        logger.error(e)


# def SetSafetyWakeup():
                
#     try:
#             minsToWakeAfter = 10

#             logger.debug('skipping shutdown - scheduling safety wakeup in ' + str(minsToWakeAfter) + ' minutes incase we crash...')
#             # Set wake up for near period in future in case we crash.

#             minToWakeAt = datetime.datetime.now().minute + minsToWakeAfter
#             if minToWakeAt >= 60:
#                 minToWakeAt = minToWakeAt - 60

#             alarmObj = {
#                     'year': 'EVERY_YEAR',
#                     'month': 'EVERY_MONTH',
#                     'day': 'EVERY_DAY',
#                     'hour': 'EVERY_HOUR',
#                     # 'minute_period': DELTA_MIN,
#                     'minute': minToWakeAt,
#                     'second': 0,
#             }

#             alarmSet = False
#             while alarmSet == False:
#                 status = pj.rtcAlarm.SetAlarm(alarmObj)

#                 if status['error'] != 'NO_ERROR':
#                     logger.error('Cannot set alarm\n')
#                     # sys.exit()
#                     alarmSet = False
#                     logger.info('Sleeping and retrying...\n')
#                     time.sleep(10)
#                 else:
#                     logger.debug('Safety Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))
#                     alarmSet = True

#             # Ensure Wake up alarm is actually enabled!
#             wakeUpEnabled = False
#             while wakeUpEnabled == False:

#                 status = pj.rtcAlarm.SetWakeupEnabled(True)

#                 if status['error'] != 'NO_ERROR':
#                     logger.error('Cannot enable wakeup\n')
#                     # sys.exit()
#                     wakeUpEnabled = False
#                     logger.info('Sleeping and retrying for wakeup...\n')
#                     time.sleep(10)
#                 else:
#                     logger.debug('Safety Wakeup enabled')
#                     wakeUpEnabled = True

#     except Exception as e:
#         logger.error("SetSafetyWakeup() failed.")
#         logger.error(e)



def saveTelemetry():
    try:
        warningTemp = 50

        api_data = {
                    'diskSpaceFree': shutil.disk_usage('/')[2] // (1024**3), # shutil.disk_usage returns tuple of (total, used, free), converted to int gb
                    'pendingImages': len(os.listdir(pendingImageFolder)),
                    'uploadedImages': len(os.listdir(uploadedImageFolder)),
                    'pendingTelemetry': len(os.listdir(pendingTelemetryFolder)),
                    'uploadedTelemetry': len(os.listdir(uploadedTelemetryFolder)),
                    'uptimeSeconds': int(time.clock_gettime(time.CLOCK_BOOTTIME)),
                    'SerialNumber': serialNumber
                }

        if pj is None or pj.status.GetStatus()['error'] == 'COMMUNICATION_ERROR':
            api_data['batteryPercent'] = 0
            api_data['temperatureC'] = 0
        else:
            api_data['batteryPercent'] = pj.status.GetChargeLevel()['data']
            api_data['temperatureC'] = pj.status.GetBatteryTemperature()['data']
            api_data['status']= str({ 'status': pj.status.GetStatus()['data'],
                                'batteryVoltage': pj.status.GetBatteryVoltage()['data'],
                                'batteryCurrent': pj.status.GetBatteryCurrent()['data'],
                                'ioVoltage': pj.status.GetIoVoltage()['data'],
                                'ioCurrent': pj.status.GetIoCurrent()['data']
                            })

        telemetryFilename = pendingTelemetryFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.json')
        with open(telemetryFilename, 'w') as outfile:
            json.dump(api_data, outfile)
            logger.debug('telemetry saved')

    except Exception as e:
        logger.error("saveTelemetry() failed.")
        logger.error(e)

if pj is not None and pj.status.GetStatus()['error'] == 'NO_ERROR':
    try:
        waitForRTCAttempts = 0
        while not os.path.exists('/dev/rtc') and waitForRTCAttempts <= 60:
            logger.info("dev rtc doesn't exist - waiting... " + str(waitForRTCAttempts))
            time.sleep(1)
            waitForRTCAttempts = waitForRTCAttempts + 1
            subprocess.call(['sudo', 'modprobe', '-r', 'rtc_ds1307'])
            subprocess.call(['sudo', 'modprobe', 'rtc_ds1307'])

        logger.debug('setting sys clock from RTC...')
        loggerIntent.debug('setting sys clock from RTC...')
        subprocess.call(['sudo', 'hwclock', '--hctosys'])
        logger.debug("sudo hwclock --hctosys succeeded")
    except Exception as e:
        logger.error("sudo hwclock --hctosys failed")
        logger.error(e)
        

try:
    logger.info('In saveTelemetry.py')

    # Set safety wakeup right up front incase modem causes us to fall over.
    # SetSafetyWakeup()
    SetWatchdog()

    # if config['shutdown']:
    #     logger.info('Setting failsafe power off for 2 minutes 30 seconds from now.')
    #     pj.power.SetPowerOff(150)   # Fail safe turn the thing off

    while True:
        saveTelemetry()
        time.sleep(60)
        scheduleShutdown()
except Exception as e:
    logger.error("Catastrophic failure.")
    scheduleShutdown()
    logger.error(e)
