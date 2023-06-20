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

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
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
        setAlarm = False

        config = json.load(open('config.json'))

        if config['shutdown']:
            # print(str(datetime.datetime.now()) + ' scheduling regular shutdown')
            logger.info('scheduling regular shutdown')
            DELTA_MIN=10

            alarmObj = {
                    'year': 'EVERY_YEAR',
                    'month': 'EVERY_MONTH',
                    'day': 'EVERY_DAY',
                    'hour': 'EVERY_HOUR',
                    'minute_period': DELTA_MIN,
                    'second': 0,
            }

            setAlarm = True

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

        if config['sleep_during_night'] == True and (datetime.datetime.now().hour >= config['daytime_ends_at_h'] or datetime.datetime.now().hour < config['daytime_starts_at_h']):
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


        # If we've been up for more than 2 modem cycles or 30 minutes, and the most recently captured image is older than 10 minutes, or the most recently uploaded image is older than 30 minutes, 
        # either network is out, or we can't get a cellular signal, DNS is messing around, or camera isn't capturing.
        # Let's shutdown, power down, and wake up again in 3 mins to see if that fixes it.
        uptimeSeconds = int(time.clock_gettime(time.CLOCK_BOOTTIME))
        power_interval = config['modem.power_interval']
        
        if uptimeSeconds > power_interval * 2 and uptimeSeconds > 1800:
            mostRecentUploadedFiles = sorted(glob.iglob(uploadedImageFolder + "/*.*"), key=os.path.getctime, reverse=True)
            mostRecentPendingFiles = sorted(glob.iglob(pendingImageFolder + "/*.*"), key=os.path.getctime, reverse=True)

            secondsSinceLastUpload = -1
            secondsSinceLastImageCapture = -1

            triggerRestart = False

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

            logger.info('Shutting down...')
            subprocess.call(['sudo', 'shutdown'])
            logger.info('Power off scheduled for 30s from now')
            pj.power.SetPowerOff(30)
            logger.info('Setting System Power Switch to Off:')
            pj.power.SetSystemPowerSwitch(0)
        else:
            # logger.debug('skipping shutdown scheduling because of config.json')
            # # Ensure Wake up alarm is *not* enabled - or it will cause pi to reboot
            # status = pj.rtcAlarm.SetWakeupEnabled(False)

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
        logger.error("scheduleShutdown() failed.")
        logger.error(e)

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
        logger.error(e)

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
    logger.error(e)
    

try:
    logger.info('In saveTelemetry.py')
    if config['shutdown']:
        logger.info('Setting failsafe power off for 2 minutes 30 seconds from now.')
        pj.power.SetPowerOff(150)   # Fail safe turn the thing off

    while True:
        saveTelemetry()
        time.sleep(60)
        scheduleShutdown()
except Exception as e:
    logger.error("Catastrophic failure.")
    scheduleShutdown()
    logger.error(e)
