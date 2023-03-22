import subprocess
import json
import pijuice
import os
import time
import shutil
import datetime
import sys
import logging
import pathlib

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

logging.basicConfig(filename=logFilePath,
                    format='%(asctime)s %(levelname)s: %(message)s',
                    level = logging.DEBUG
                    # datefmt='%d/%m/%Y %I:%M:%S %p'
                    # encoding='utf-8'
                    )
# log = logging.getLogger()
logging.info("Starting up saveTelemetry.py...")

# clock
while not os.path.exists('/dev/i2c-1'):
    logging.info("dev i2c-1 doesn't exist")
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
logging.info("Starting up saveTelemetry.py 3b...")

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
    alarmObj = {}

    # print(str(datetime.datetime.now()) + ' scheduleShutdown')
    logging.debug('scheduleShutdown')
    setAlarm = False

    config = json.load(open('config.json'))

    if config['shutdown']:
        # print(str(datetime.datetime.now()) + ' scheduling regular shutdown')
        logging.info('scheduling regular shutdown')
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

    if config['sleep_during_night'] == True and (datetime.datetime.now().hour >= config['daytime_ends_at_h'] or datetime.datetime.now().hour < config['daytime_starts_at_h']):
        logging.info("Night time so we're scheduling shutdown")

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

    if setAlarm == True:
        logging.info("scheduleShutdown - we're setting the shutdown...")

        alarmSet = False
        while alarmSet == False:
            status = pj.rtcAlarm.SetAlarm(alarmObj)

            if status['error'] != 'NO_ERROR':
                logging.error('Cannot set alarm\n')
                # sys.exit()
                alarmSet = False
                logging.info('Sleeping and retrying...\n')
                time.sleep(10)
            else:
                logging.debug('Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))
                alarmSet = True

        # Ensure Wake up alarm is actually enabled!
        wakeUpEnabled = False
        while wakeUpEnabled == False:

            status = pj.rtcAlarm.SetWakeupEnabled(True)

            if status['error'] != 'NO_ERROR':
                logging.error('Cannot enable wakeup\n')
                # sys.exit()
                wakeUpEnabled = False
                logging.info('Sleeping and retrying for wakeup...\n')
                time.sleep(10)
            else:
                logging.debug('Wakeup set for ' + str(pj.rtcAlarm.GetAlarm()))
                wakeUpEnabled = True

        logging.info('Shutting down...')
        subprocess.call(['sudo', 'shutdown'])
        logging.info('Power off scheduled for 30s from now')
        pj.power.SetPowerOff(30)
    else:
        logging.debug('skipping shutdown scheduling because of config.json')
        # Ensure Wake up alarm is *not* enabled - or it will cause pi to reboot
        status = pj.rtcAlarm.SetWakeupEnabled(False)

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
            logging.debug('telemetry saved')

    except Exception as e:
        logging.error("saveTelemetry() failed.")
        logging.error(e)

try:
    waitForRTCAttempts = 0
    while not os.path.exists('/dev/rtc') and waitForRTCAttempts <= 60:
        logging.info("dev rtc doesn't exist - waiting... " + str(waitForRTCAttempts))
        time.sleep(1)
        waitForRTCAttempts = waitForRTCAttempts + 1
        subprocess.call(['sudo', 'modprobe', '-r rtc_ds1307'])
        subprocess.call(['sudo', 'modprobe', 'rtc_ds1307'])

    logging.debug('setting sys clock from RTC...')
    subprocess.call(['sudo', 'hwclock', '--hctosys'])
    logging.debug("sudo hwclock --hctosys succeeded")
except Exception as e:
    logging.error("sudo hwclock --hctosys failed")
    logging.error(e)
    

try:
    logging.info('In saveTelemetry.py')
    if config['shutdown']:
        logging.info('Setting failsafe power off for 2 minutes 30 seconds from now.')
        pj.power.SetPowerOff(150)   # Fail safe turn the thing off

    while True:
        saveTelemetry()
        scheduleShutdown()
        time.sleep(60)
except Exception as e:
    logging.error("Catastrophic failure.")
    scheduleShutdown()
    logging.error(e)
