import subprocess
import json
import pijuice
from picamera import PiCamera
import os
import time
import shutil
import datetime

config = json.load(open('config.json'))

# clock
while not os.path.exists('/dev/i2c-1'):
    time.sleep(0.1)
subprocess.call(['sudo', 'hwclock', '--hctosys'])

# pijuice
pj = pijuice.PiJuice(1, 0x14)

def scheduleShutdown():
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

    subprocess.call(['sudo', 'shutdown', '-h', 'now'])


def saveAndUploadPhoto():
    outputImageFolder = '../output/images/'
    os.makedirs(outputImageFolder, exist_ok = True)

    txtTime = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')

    camera = PiCamera()
    camera.start_preview()
    time.sleep(2)
    camera.capture(outputImageFolder + txtTime + '.jpg')

    print('beginning capture')
    camera = PiCamera()
    camera.vflip = True
    camera.hflip = True
    #camera.resolution = (1024, 768)
    #camera.resolution = (3280,2464) # Didn't work
    camera.start_preview()
    # Camera warm-up time
    print('warming up...')
    time.sleep(2)
    print('ready')
    IMAGEFILENAME = OUTPUTIMAGEFOLDER + datetime.datetime.now().strftime('%Y-%m-%d_%H%M.jpg')
    camera.capture(IMAGEFILENAME)
    print('image saved')

    # Send image to api
    files = {
        'File': open(IMAGEFILENAME, 'rb'),
    }

    data = {
        'DeviceId': localConfig['deviceId']
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
    api_data = {
                'batteryPercent': pj.status.GetChargeLevel()['data'],
                'temperatureC': pj.status.GetBatteryTemperature()['data'],
                'diskSpaceFree': shutil.disk_usage('/')[2] // (1024**3), # shutil.disk_usage returns tuple of (total, used, free), converted to int gb
                'uptimeSeconds': int(time.clock_gettime(time.CLOCK_BOOTTIME)),
                'deviceId': localConfig['deviceId'],      # I'll sort this out in a bit.
            }

    #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)
    session = requests.Session()

    print(api_data)

    postResponse = session.post(config['apiUrl'] + 'Telemetry',data=api_data)
    print(postResponse)
    assert postResponse.status_code == 200, "API returned error code"
    #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

    print('Logged to API.')



try:
    uploadTelemetry()
    saveAndUploadPhoto()

finally:
    scheduleShutdown()
