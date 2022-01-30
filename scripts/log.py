print('Logging...')

from pijuice import PiJuice
import time
import shutil
import os
import json
try:
    import requests
except ImportError:
    print('requests module not found, try "python3 -m pip install requests"')


pj = PiJuice(1, 0x14)
config = json.load(open('./config.json'))
log = f'{time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())}, {pj.status.GetChargeLevel()["data"]}, {pj.status.GetBatteryTemperature()["data"]}, {pj.status.GetStatus()["data"]["battery"]}, {time.time() - time.clock_gettime(time.CLOCK_BOOTTIME)}\n'

if config['logToFile']:
    log = f'{time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())}, {pj.status.GetChargeLevel()["data"]}, {pj.status.GetBatteryTemperature()["data"]}\n'

    outFile = './../output/log.csv'

    with open(outFile, 'a') as f:
        f.write(log)

    if pj.status.GetBatteryTemperature()["data"] > tempWarning:
        with open('./tempWarning.log', 'a') as f:
            f.write(f'{time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())}, {pj.status.GetBatteryTemperature()["data"]}')

    print('Logged to file.')

if config['logToAPI']:

    api_data = {
                'batteryPercent': pj.status.GetChargeLevel()['data'],
                'temperatureC': pj.status.GetBatteryTemperature()['data'],
                'diskSpaceFree': shutil.disk_usage('/')[2] // (1024**3), # shutil.disk_usage returns tuple of (total, used, free), converted to int gb
                'uptimeSeconds': int(time.clock_gettime(time.CLOCK_BOOTTIME)),
                'deviceId': config['deviceId'],      # I'll sort this out in a bit.
            }

    #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)
    session = requests.Session()

    print(api_data)

    postResponse = session.post(config['apiUrl'] + 'Telemetry',data=api_data)
    print(postResponse)
    assert postResponse.status_code == 200, "API returned error code"
    #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

    print('Logged to API.')
