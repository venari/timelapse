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


def savePhoto():
    os.makedirs(outputImageFolder, exist_ok = True)
    os.makedirs(pendingImageFolder, exist_ok = True)
    # txtTime = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')

    try:
        with PiCamera() as camera:

            print(str(datetime.datetime.now()) + ' beginning capture')
            camera.vflip = config['camera.vflip']
            camera.hflip = config['camera.hflip']
            camera.resolution = (config['camera.resolution.width'], config['camera.resolution.height'])
            camera.rotation = config['camera.rotation']

            #camera.resolution = (1024, 768)
            #camera.resolution = (3280,2464) # Didn't work
            camera.start_preview()
            # Camera warm-up time
            print(str(datetime.datetime.now()) + ' warming up camera...')
            time.sleep(5)
            print(str(datetime.datetime.now()) + ' ready')

            IMAGEFILENAME = pendingImageFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.jpg')
            camera.capture(IMAGEFILENAME)
            print(str(datetime.datetime.now()) + ' image saved')

    except Exception as e:
        print(str(datetime.datetime.now()) + " SavePhoto() failed.")
        print(e)

def uploadPendingPhotos():
    try:
        os.makedirs(pendingImageFolder, exist_ok = True)
        os.makedirs(uploadedImageFolder, exist_ok = True)
        for IMAGEFILENAME in os.listdir(pendingImageFolder):
            print(str(datetime.datetime.now()) + ' uploading ' + IMAGEFILENAME)

            imageTimestamp = datetime.datetime.strptime(IMAGEFILENAME, '%Y-%m-%d_%H%M%S.jpg')
            print('imageTimestamp:')
            print(imageTimestamp)

            files = {
                'File': open(pendingImageFolder + IMAGEFILENAME, 'rb'),
            }

            data = {
                'SerialNumber': serialNumber,
                # 'Timestamp': (datetime.datetime.utcfromtimestamp(imageTimestamp.timestamp)).strftime('%Y-%m-%d %H:%M:%S')
                'Timestamp': imageTimestamp.astimezone().isoformat()
            }

            print('data:')
            print(data)

            session = requests.Session()
            print('Posting image to API...')
            response = session.post(config['apiUrl'] + 'Image', files=files, data=data)

            print(f'Response code: {response.status_code}')
            if response.status_code == 200:
                print(f'Image uploaded successfully')
                shutil.move(pendingImageFolder + IMAGEFILENAME, uploadedImageFolder + IMAGEFILENAME)

            else:
                print(f'Image upload failed')

            print(f'Response text:')
            try:
                print(json.dumps(json.loads(response.text), indent = 4))
            except json.decoder.JSONDecodeError:
                print(response.text)
    except Exception as e:
        print(str(datetime.datetime.now()) + " uploadPendingPhotos() failed.")
        print(e)


def uploadTelemetry():
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

        if api_data['temperatureC'] > warningTemp:
            print(f'WARNING: temperature is {api_data["temperatureC"]}C')
            with open('tempWarning.log', 'a') as f:
                f.write(f'{datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")}: {api_data["temperatureC"]}C\n')

        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)
        session = requests.Session()

        print(api_data)

        postResponse = session.post(config['apiUrl'] + 'Telemetry',data=api_data)
        print(postResponse)
        #assert postResponse.status_code == 200, "API returned error code"
        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

        print(str(datetime.datetime.now()) + ' Logged to API.')

    except Exception as e:
        print(str(datetime.datetime.now()) + " uploadTelemetry() failed.")
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

    uploadTelemetry()
    print(str(datetime.datetime.now()) + ' warming up... waiting 30s')
    # Give everything a chance to settle down.
    time.sleep(30)

    uploadTelemetry()
    
    #time.sleep(40) # Wait for the camera and network to warm up
    savePhoto()
    uploadPendingPhotos()
    uploadTelemetry()
    scheduleShutdown()
except Exception as e:
    print(str(datetime.datetime.now()) + " Catastrophic failure.")
    scheduleShutdown()
    print(e)
