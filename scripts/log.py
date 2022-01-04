print('Logging...')

from pijuice import PiJuice
import time
import os

pj = PiJuice(1, 0x14) #?

log = f'{time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())}, {pj.status.GetChargeLevel()["data"]}, {pj.status.GetBatteryTemperature()["data"]}\n'

outFile = os.path.dirname(os.path.realpath(__file__)) + '/log.csv'

with open(outFile, 'a') as f:
    f.write(log)

print('Logging complete')
