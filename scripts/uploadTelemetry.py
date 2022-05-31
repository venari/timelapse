import subprocess
import json
import pijuice
import os
import time
import shutil
import datetime
import sys
import requests

config = json.load(open('config.json'))

while not os.path.exists('/dev/i2c-1'):
    time.sleep(0.1)

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

def uploadTelemetry():
    warningTemp = 50

    print(pj.status.GetChargeLevel())

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
except Exception as e:
    print(e)
