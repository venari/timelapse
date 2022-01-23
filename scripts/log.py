print('Logging...')

from pijuice import PiJuice
import time
import os
import json
try:
    import requests
except ImportError:
    print('requests module not found, try "python3 -m pip install requests"')


config = json.load(open(os.path.dirname(os.path.realpath(__file__)) + '/config.json'))

if config['logToFile']:
    pj = PiJuice(1, 0x14) #?

    log = f'{time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())}, {pj.status.GetChargeLevel()["data"]}, {pj.status.GetBatteryTemperature()["data"]}\n'

    outFile = os.path.dirname(os.path.realpath(__file__)) + '/../output/log.csv'

    with open(outFile, 'a') as f:
        f.write(log)

    print('Logged to file.')

if config['logToAPI']:

    api_data = {
            'time': time.time(),
            'charge': pj.status.GetChargeLevel(),
            'temp': pj.status.GetBatteryTemperature()
        }

    requests.post(config['apiUrl'], json=api_data)

    print('Logged to API.')
