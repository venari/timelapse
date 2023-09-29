import subprocess
import json
import pijuice
import os
import time
import shutil
import datetime
import sys
import logging
from logging.handlers import TimedRotatingFileHandler
import pathlib
import glob
import suntime
from dateutil import tz

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
# logFilePath = logFilePath.replace(".log", ".saveTelemetry.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)
# os.chmod(os.path.dirname(logFilePath), 0o777) # Make sure pijuice user scrip can write to log file.


formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = TimedRotatingFileHandler(logFilePath, 
                                   when='midnight',
                                   backupCount=10)
handler.setFormatter(formatter)
logger = logging.getLogger("saveTelemetry")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info("Starting up saveTelemetry.py...")
os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.

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

# suntime
if config['dynamic_sunrise_sunset']:
    latitude = config['latitude']
    longitude = config['longitude']
    sun = suntime.Sun(latitude, longitude)

    sunrise_time = sun.get_local_sunrise_time().astimezone(tz.UTC)
    sunset_time = sun.get_local_sunset_time().astimezone(tz.UTC)

    if sunrise_time > sunset_time: # sun.get_local_sunrise_time() assumes UTC as default so gives sunrise of next day instead of this day
        sunrise_time = sunrise_time - datetime.timedelta(1)

else:
    sunrise_time = datetime.datetime.now().replace(hour=config['sunrise_time_h'])
    sunset_time = datetime.datetime.now().replace(hour=config['sunset_time_h'])

logger.debug(f'sunrise_time: {sunrise_time.isoformat()}')
logger.debug(f'sunset_time: {sunset_time.isoformat()}')


# pijuice
time.sleep(10)
pj = pijuice.PiJuice(1, 0x14)
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

        if config['sleep_during_night'] == True and (datetime.datetime.now(tz=tz.UTC) > sunset_time + datetime.timedelta(hours=config['sunset_offset_h']) or datetime.datetime.now(tz=tz.UTC) < sunrise_time + datetime.timedelta(hours=config['sunrise_offset_h'])):
            if config['supportMode'] == True:
                logger.warning("Night time - we would have scheduled shutdown, but we're in support mode.")

            if bCharging:
                logger.info("Night time - but we're charging/powered, so we'll stay on.")


        # Hibernate mode? Lets have 5 minutes to give it a chance to check again before hibernating.
        if config['hibernateMode']:
            logger.info('hibernate mode - stay awake for 5 mins')
            if uptimeSeconds > 300:
                logger.info('hibernate mode - sleeping for 6 hours...')

                hoursToWakeAfter = 6
                hourToWakeAt = datetime.datetime.utcnow().hour + hoursToWakeAfter
                if hourToWakeAt >= 24:
                    hourToWakeAt = hourToWakeAt - 24


                alarmObj = {
                    'year': 'EVERY_YEAR',
                    'month': 'EVERY_MONTH',
                    'day': 'EVERY_DAY',
                    'hour': hourToWakeAt,
                    'minute': 0,
                    'second': 0,
                }

                setAlarm = True

        else:

            if config['sleep_during_night'] == True and (datetime.datetime.now(tz=tz.UTC) >= sunset_time + datetime.timedelta(hours=config['sunset_offset_h']) or datetime.datetime.now(tz=tz.UTC) < sunrise_time + datetime.timedelta(hours=config['sunrise_offset_h'])) and config['supportMode'] == False and bCharging == False:
                logger.info("Night time so we're scheduling shutdown")

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

            else:

                # sleep_at_battery_percent - at this battery percentage, we go to sleep and wake up every 10 minutes.
                # hibernate_at_battery_percent - at this battery percentage, the pi_juice min_charge setting puts us to sleep until battery gets to wakeup_on_charge value,
                # so we let this setting take precedence via the pijuice_config.JSON file and don't set an alarm here.

                if config['sleep_at_battery_percent'] > 0 and config['hibernate_at_battery_percent'] > 0 \
                and pj.status.GetChargeLevel()['data'] <= config['sleep_at_battery_percent'] \
                and pj.status.GetChargeLevel()['data'] > config['hibernate_at_battery_percent'] \
                and pj.status.GetStatus()['data']['battery'] != 'NOT_PRESENT':
                    logger.info('scheduling 10 minute sleep due to low battery')
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
                            triggerRestart = True

                        if secondsSinceLastUpload > 1800:
                            logger.warning('Most recent uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            triggerRestart = True

                        if len(mostRecentPendingFiles) == 0 and len(mostRecentUploadedFiles) == 0:
                            logger.debug("No uploaded or captured images found - restarting...")
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
                    wakeUpEnabled = True

            logger.debug('rtcAlarm.GetControlStatus(): ' + str(pj.rtcAlarm.GetControlStatus()))
            logger.debug('rtcAlarm.GetTime(): ' + str(pj.rtcAlarm.GetTime()))

            logger.debug('Clearing Alarm Flag...')
            pj.rtcAlarm.ClearAlarmFlag()
            logger.debug('rtcAlarm.GetControlStatus(): ' + str(pj.rtcAlarm.GetControlStatus()))
            logger.debug('rtcAlarm.GetTime(): ' + str(pj.rtcAlarm.GetTime()))

            if triggerRestart:
                logger.info('Restart scheduled for ' + str(minsToWakeAfter) + ' minutes from now')
                logger.info("So we'll skip the power off.")
            else:
                logger.info('Power off scheduled for 30s from now')
                pj.power.SetPowerOff(30)
        
            logger.info('Setting System Power Switch to Off:')
            pj.power.SetSystemPowerSwitch(0)
            powerDownSIM7600X()
            logger.info('Shutting down now...')
            subprocess.call(['sudo', 'shutdown', '-h', 'now'])
        else:
            # logger.debug('skipping shutdown scheduling because of config.json')
            # # Ensure Wake up alarm is *not* enabled - or it will cause pi to reboot
            # status = pj.rtcAlarm.SetWakeupEnabled(False)

            SetSafetyWakeup()

    except Exception as e:
        logger.error("scheduleShutdown() failed.")
        logger.exception(e)

def SetSafetyWakeup():
                
    try:
            minsToWakeAfter = 10

            logger.debug('skipping shutdown - scheduling safety wakeup in ' + str(minsToWakeAfter) + ' minutes incase we crash...')
            # Set wake up for near period in future in case we crash.

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
                    logger.debug('Safety Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))
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
                    logger.debug('Safety Wakeup enabled')
                    wakeUpEnabled = True

    except Exception as e:
        logger.error("SetSafetyWakeup() failed.")
        logger.exception(e)



def saveTelemetry():
    try:
        warningTemp = 50
        api_data = {
                    'batteryPercent': pj.status.GetChargeLevel()['data'],
                    'temperatureC': pj.status.GetBatteryTemperature()['data'],
                    'diskSpaceFree': shutil.disk_usage('/')[2] // (1024**3), # shutil.disk_usage returns tuple of (total, used, free), converted to int gb
                    'pendingImages': len(os.listdir(pendingImageFolder)),
                    'uploadedImages': len(os.listdir(uploadedImageFolder)),
                    'pendingTelemetry': len(os.listdir(pendingTelemetryFolder)),
                    'uploadedTelemetry': len(os.listdir(uploadedTelemetryFolder)),
                    'uptimeSeconds': int(time.clock_gettime(time.CLOCK_BOOTTIME)),
                    'status': str({ 'status': pj.status.GetStatus()['data'],
                                'batteryVoltage': pj.status.GetBatteryVoltage()['data'],
                                'batteryCurrent': pj.status.GetBatteryCurrent()['data'],
                                'ioVoltage': pj.status.GetIoVoltage()['data'],
                                'ioCurrent': pj.status.GetIoCurrent()['data']
                            }),
                    # 'Timestamp': datetime.datetime.now().astimezone().isoformat(),
                    'SerialNumber': serialNumber
                }

        telemetryFilename = pendingTelemetryFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.json')
        with open(telemetryFilename, 'w') as outfile:
            json.dump(api_data, outfile)
            logger.debug('telemetry saved')

    except Exception as e:
        logger.error("saveTelemetry() failed.")
        logger.exception(e)

try:
    waitForRTCAttempts = 0
    while not os.path.exists('/dev/rtc') and waitForRTCAttempts <= 60:
        logger.info("dev rtc doesn't exist - waiting... " + str(waitForRTCAttempts))
        time.sleep(1)
        waitForRTCAttempts = waitForRTCAttempts + 1
        subprocess.call(['sudo', 'modprobe', '-r', 'rtc_ds1307'])
        subprocess.call(['sudo', 'modprobe', 'rtc_ds1307'])

    logger.debug('setting sys clock from RTC...')
    subprocess.call(['sudo', 'hwclock', '--hctosys'])
    logger.debug("sudo hwclock --hctosys succeeded")
except Exception as e:
    logger.error("sudo hwclock --hctosys failed")
    logger.exception(e)
    

try:
    logger.info('In saveTelemetry.py')

    # Set safety wakeup right up front incase modem causes us to fall over.
    SetSafetyWakeup()

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
    logger.exception(e)
