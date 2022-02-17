import subprocess
import json
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
subprocess.call(['sudo', 'hwclock', '--hctosys'])

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
    if config['shutdown']:
        print(str(datetime.datetime.now()) + ' scheduling shutdown')
        DELTA_MIN=10
        SHUTDOWN_TILL_MORNING=False

        if datetime.datetime.now().hour >=21 or datetime.datetime.now().hour <= 5:
            SHUTDOWN_TILL_MORNING=True

        alarmObj = {
                'year': 'EVERY_YEAR',
                'month': 'EVERY_MONTH',
                'day': 'EVERY_DAY',
                'hour': 18 if SHUTDOWN_TILL_MORNING else 'EVERY_HOUR',
                'minute_period': DELTA_MIN,
                'second': 0,
            }
        status = pj.rtcAlarm.SetAlarm(alarmObj)

        if status['error'] != 'NO_ERROR':
            print('Cannot set alarm\n')
            sys.exit()
        else:
            print('Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))

        print(str(datetime.datetime.now()) + ' Shutting down...')
        subprocess.call(['sudo', 'shutdown'])
        print(str(datetime.datetime.now()) + ' Power off scheduled for 30s from now')
        pj.power.SetPowerOff(30)
    else:
        print(str(datetime.datetime.now()) + ' skipping shutdown scheduling because of config.json')


def saveAndUploadPhoto():
    outputImageFolder = '../output/images/'
    os.makedirs(outputImageFolder, exist_ok = True)
    # txtTime = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')

    with PiCamera() as camera:

        print(str(datetime.datetime.now()) + ' beginning capture')
        camera.vflip = True
        camera.hflip = True
        #camera.resolution = (1024, 768)
        #camera.resolution = (3280,2464) # Didn't work
        camera.start_preview()
        # Camera warm-up time
        print(str(datetime.datetime.now()) + ' warming up camera...')
        time.sleep(5)
        print(str(datetime.datetime.now()) + ' ready')
        IMAGEFILENAME = '../output/images/' + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.jpg')
        camera.capture(IMAGEFILENAME)
        print(str(datetime.datetime.now()) + ' image saved')

    # Send image to api
    files = {
        'File': open(IMAGEFILENAME, 'rb'),
    }

    data = {
        'SerialNumber': serialNumber
    }

    print('data:')
    print(data)

    session = requests.Session()
    response = session.post(config['apiUrl'] + 'Image', files=files, data=data)

    print(f'Response code: {response.status_code}')
    print(f'Response text:')
    try:
        print(json.dumps(json.loads(response.text), indent = 4))
    except json.decoder.JSONDecodeError:
        print(response.text)


def uploadTelemetry():
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
    assert postResponse.status_code == 200, "API returned error code"
    #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

    print(str(datetime.datetime.now()) + ' Logged to API.')



try:
    uploadTelemetry()
    print(str(datetime.datetime.now()) + ' warming up... waiting 30s')
    # Give everything a chance to settle down.
    time.sleep(30)

    if config['shutdown']:
        print(str(datetime.datetime.now()) + ' Setting failsafe power off for 2 minutes from now.')
        pj.power.SetPowerOff(120)   # Fail safe turn the thing off
    uploadTelemetry()
    
    #time.sleep(40) # Wait for the camera and network to warm up
    saveAndUploadPhoto()
    uploadTelemetry()
    scheduleShutdown()
except Exception as e:
    print(str(datetime.datetime.now()) + e)
    scheduleShutdown()
