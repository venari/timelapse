print('Logging...')

from pijuice import PiJuice
import time
import psutil
import os
import json
try:
    import requests
except ImportError:
    print('requests module not found, try "python3 -m pip install requests"')


pj = PiJuice(1, 0x14)
config = json.load(open(os.path.dirname(os.path.realpath(__file__)) + '/config.json'))
log = f'{time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())}, {pj.status.GetChargeLevel()["data"]}, {pj.status.GetBatteryTemperature()["data"]}, {pj.status.GetStatus()["data"]["battery"]}, {time.time() - psutil.boot_time()}\n'

if config['logToFile']:
    log = f'{time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())}, {pj.status.GetChargeLevel()["data"]}, {pj.status.GetBatteryTemperature()["data"]}\n'

    outFile = os.path.dirname(os.path.realpath(__file__)) + '/../output/log.csv'

    with open(outFile, 'a') as f:
        f.write(log)

    print('Logged to file.')

if config['logToAPI']:
    api_data = {
    #            'time': time.time(), <--- leave empty - API will populate it.
                'batteryPercent': pj.status.GetChargeLevel(),
                'temperatureC': pj.status.GetBatteryTemperature(),
                'diskSpaceFree': 0,
                'uptimeSeconds': time.time() - psutil.boot_time(),
                'deviceId': 1,      # I'll sort this out in a bit.
            }

    requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

    print('Logged to API.')
