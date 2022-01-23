# From https://github.com/PiSupply/PiJuice/issues/91


import time
import pijuice
import subprocess
import datetime
import os
import sys 
from gpiozero import CPUTemperature
from picamera import PiCamera

DELTA_MIN=10
SHUTDOWN_TILL_MORNING=False

OUTPUTIMAGEFOLDER = os.path.dirname(os.path.realpath(__file__)) + '/../output/images/'
LOGFILE = os.path.dirname(os.path.realpath(__file__)) + '/../output/test.log'
CSVOUTPUTFILE = os.path.dirname(os.path.realpath(__file__)) + '/../output/test.csv'

os.makedirs(OUTPUTIMAGEFOLDER, exist_ok = True)

if datetime.datetime.now().hour >=21 or datetime.datetime.now().hour <= 5:
#    DELTA_MIN=60
    SHUTDOWN_TILL_MORNING=True
#DELTA_MIN=10

# This script is started at reboot by cron.
# Since the start is very early in the boot sequence we wait for the i2c-1 device
while not os.path.exists('/dev/i2c-1'):
    time.sleep(0.1)

# Rely on RTC to keep the time
subprocess.call(["sudo", "hwclock", "--hctosys"])

# Record start time
txtTime = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')
txt = txtTime + ' -- Started\n'
with open(LOGFILE,'a') as f:
    f.write(txt)


try:
    pj = pijuice.PiJuice(1, 0x14)
except:
    print("Cannot create pijuice object")
    sys.exit()

txtStatus = str(pj.status.GetStatus())
txtChargeLevel = str(pj.status.GetChargeLevel())
txtCPUTemp =str(CPUTemperature().temperature)
#with open(LOGFILE,'a') as f:
#    f.write(txtStatus + "\n")
#    f.write(txtChargeLevel + "\n")
#    f.write("CPU Tempperature: " + txtCPUTemp + "\n")
#    f.write(txtTime + " - rtcAlarm.GetTime(): " + str(pj.rtcAlarm.GetTime()))

#with open(CSVOUTPUTFILE,'a') as f:
#    f.write(txtTime + ", " + str(pj.status.GetChargeLevel()['data']) + ", " + str(CPUTemperature().temperature) + "," + str(pj.status.GetStatus()['data']['battery']) + "\n")

## Do the work
for i in range(60):
   print('*', end='', flush=True)
   #sys.stdout.flush()
   time.sleep(1)
print()

# take the picture
#DATE=$(date +"%Y-%m-%d_%H%M")
#mkdir -p $OUTPUTIMAGEFOLDER
#raspistill -vf -hf --nopreview -o $OUTPUTIMAGEFOLDER/$DATE.jpg

with open(LOGFILE,'a') as f:
    f.write(datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S') + "- About to take picture....\n")

camera = PiCamera()
#camera.resolution = (1024, 768)
camera.start_preview()
# Camera warm-up time
time.sleep(2)
IMAGEFILENAME = OUTPUTIMAGEFOLDER + datetime.datetime.now().strftime('%Y-%m-%d_%H%M.jpg')
camera.capture(IMAGEFILENAME)

with open(LOGFILE,'a') as f:
    f.write(datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S') + " - Picture taken and saved.\n")
    f.write("Checking if git pull works.\n")

# Set RTC alarm 5 minutes from now
# RTC is kept in UTC
a={}
a['year'] = 'EVERY_YEAR'
a['month'] = 'EVERY_MONTH'
a['day'] = 'EVERY_DAY'
a['hour'] = 'EVERY_HOUR'
if SHUTDOWN_TILL_MORNING:
    a['hour'] = 18 # 1800 UTC 7am NZ
t = datetime.datetime.utcnow()
#a['minute'] = (t.minute + DELTA_MIN) % 60
a['minute_period'] = DELTA_MIN
a['second'] = 0
status = pj.rtcAlarm.SetAlarm(a)
if status['error'] != 'NO_ERROR':
    print('Cannot set alarm\n')
    sys.exit()
else:
    print('Alarm set for ' + str(pj.rtcAlarm.GetAlarm()))

# Enable wakeup, otherwise power to the RPi will not be
# applied when the RTC alarm goes off
pj.rtcAlarm.SetWakeupEnabled(True)
time.sleep(0.4)

# PiJuice shuts down power to Rpi after 20 sec from now
# This leaves sufficient time to execute the shutdown sequence
# pj.power.SetPowerOff(20)
# subprocess.call(["sudo", "poweroff"])

