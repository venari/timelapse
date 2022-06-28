import subprocess
import json
from this import s
import pijuice
from picamera import PiCamera
import os
import time
import shutil
import datetime
import sys
import requests

config = json.load(open('config.json'))
# clock
while not os.path.exists('/dev/i2c-1'):
    time.sleep(0.1)

outputImageFolder = '../output/images/'
pendingImageFolder = outputImageFolder + 'pending/'
uploadedImageFolder = outputImageFolder + 'uploaded/'

outputTelemetryFolder = '../output/telemetry/'
pendingTelemetryFolder = outputImageFolder + 'pending/'
uploadedTelemetryFolder = outputImageFolder + 'uploaded/'

# pijuice
pj = pijuice.PiJuice(1, 0x14)

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

    print(str(datetime.datetime.now()) + ' scheduleShutdown')
    setAlarm = False

    if config['shutdown']:
        print(str(datetime.datetime.now()) + ' scheduling regular shutdown')
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

    print(str(datetime.datetime.now()) + ' scheduleShutdown 3')
    print(str(datetime.datetime.now()) + ' ' + str(datetime.datetime.now().hour))
    if datetime.datetime.now().hour >=18 or datetime.datetime.now().hour <= 7:
        print(str(datetime.datetime.now()) + " Night time so we're scheduling shutdown")

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
        print(str(datetime.datetime.now()) + " scheduleShutdown - we're setting the shutdown...")

        alarmSet = False
        while alarmSet == False:
            status = pj.rtcAlarm.SetAlarm(alarmObj)

            if status['error'] != 'NO_ERROR':
                print('Cannot set alarm\n')
                # sys.exit()
                alarmSet = False
                print('Sleeping and retrying...\n')
                time.sleep(10)
            else:
                print('Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))
                alarmSet = True

        # Ensure Wake up alarm is actually enabled!
        wakeUpEnabled = False
        while wakeUpEnabled == False:

            status = pj.rtcAlarm.SetWakeupEnabled(True)

            if status['error'] != 'NO_ERROR':
                print('Cannot enable wakeup\n')
                # sys.exit()
                wakeUpEnabled = False
                print('Sleeping and retrying for wakeup...\n')
                time.sleep(10)
            else:
                print('Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))
                wakeUpEnabled = True

        print(str(datetime.datetime.now()) + ' Shutting down...')
        subprocess.call(['sudo', 'shutdown'])
        print(str(datetime.datetime.now()) + ' Power off scheduled for 30s from now')
        pj.power.SetPowerOff(30)
    else:
        print(str(datetime.datetime.now()) + ' skipping shutdown scheduling because of config.json')
        # Ensure Wake up alarm is *not* enabled - or it will cause pi to reboot
        status = pj.rtcAlarm.SetWakeupEnabled(False)


def savePhotos():
    os.makedirs(outputImageFolder, exist_ok = True)
    os.makedirs(pendingImageFolder, exist_ok = True)
    # txtTime = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')

    try:
        with PiCamera() as camera:

            camera.vflip = config['camera.vflip']
            camera.hflip = config['camera.hflip']
            camera.resolution = (config['camera.resolution.width'], config['camera.resolution.height'])
            camera.rotation = config['camera.rotation']

            while True:
                print(str(datetime.datetime.now()) + ' beginning capture')
                camera.start_preview()
                # Camera warm-up time
                print(str(datetime.datetime.now()) + ' warming up camera...')
                time.sleep(5)
                print(str(datetime.datetime.now()) + ' ready')

                IMAGEFILENAME = pendingImageFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.jpg')
                camera.capture(IMAGEFILENAME)
                print(str(datetime.datetime.now()) + ' image saved')

                saveTelemetry()
                scheduleShutdown()
                if config['shutdown']:
                    break
                else:
                    time.sleep(config['camera.interval'])

    except Exception as e:
        print(str(datetime.datetime.now()) + " SavePhoto() failed.")
        print(e)

def saveTelemetry():
    try:
        warningTemp = 50
        api_data = {
                    'batteryPercent': pj.status.GetChargeLevel()['data'],
                    'temperatureC': pj.status.GetBatteryTemperature()['data'],
                    'diskSpaceFree': shutil.disk_usage('/')[2] // (1024**3), # shutil.disk_usage returns tuple of (total, used, free), converted to int gb
                    'uptimeSeconds': int(time.clock_gettime(time.CLOCK_BOOTTIME)),
                    'status': str({ 'status': pj.status.GetStatus()['data'],
                                'batteryVoltage': pj.status.GetBatteryVoltage()['data'],
                                'batteryCurrent': pj.status.GetBatteryCurrent()['data'],
                                'ioVoltage': pj.status.GetIoVoltage()['data'],
                                'ioCurrent': pj.status.GetIoCurrent()['data']
                            }),
                    'SerialNumber': serialNumber
                }

        telemetryFilename = pendingTelemetryFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.json')
        with open(telemetryFilename, 'w') as outfile:
            json.dump(api_data, outfile)
            print(str(datetime.datetime.now()) + ' telemetry saved')

    except Exception as e:
        print(str(datetime.datetime.now()) + " saveTelemetry() failed.")
        print(e)


try:
    print(str(datetime.datetime.now()) + ' setting sys clock from RTC...')
    subprocess.call(['sudo', 'hwclock', '--hctosys'])
    print(str(datetime.datetime.now()) + " sudo hwclock --hctosys succeeded")
except Exception as e:
    print(str(datetime.datetime.now()) + " sudo hwclock --hctosys failed")
    print(e)
    

try:
    if config['shutdown']:
        print(str(datetime.datetime.now()) + ' Setting failsafe power off for 2 minutes 30 seconds from now.')
        pj.power.SetPowerOff(150)   # Fail safe turn the thing off

    savePhotos()
except Exception as e:
    print(str(datetime.datetime.now()) + " Catastrophic failure.")
    scheduleShutdown()
    print(e)
