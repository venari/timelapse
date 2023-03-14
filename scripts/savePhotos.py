import subprocess
import json
from picamera2 import Picamera2, Preview
from libcamera import Transform, controls
import os
import time
import shutil
import datetime
import sys
import requests
import logging
import pathlib

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

logging.basicConfig(filename=logFilePath,
                    format='%(asctime)s %(levelname)s: %(message)s',
                    level = logging.DEBUG
                    # datefmt='%d/%m/%Y %I:%M:%S %p'
                    # encoding='utf-8'
                    )
# log = logging.getLogger()
logging.info("Starting up savePhotos.py...")

# clock
while not os.path.exists('/dev/i2c-1'):
    time.sleep(0.1)

outputImageFolder = '../output/images/'
workingImageFolder = outputImageFolder + 'working/'
pendingImageFolder = outputImageFolder + 'pending/'
uploadedImageFolder = outputImageFolder + 'uploaded/'


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

def savePhotos():
    os.makedirs(outputImageFolder, exist_ok = True)
    os.makedirs(pendingImageFolder, exist_ok = True)
    os.makedirs(workingImageFolder, exist_ok = True)
    # txtTime = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')

    try:
        #with Picamera2() as camera:

        #    while True:
        while True:
            logging.debug('creating camera object...')
            with Picamera2() as camera:

                config = json.load(open('config.json'))
                #camera_config = camera.create_preview_configuration()
                camera_config = camera.create_still_configuration()
                camera_config["transform"] = Transform(vflip = config['camera.vflip'], hflip = config['camera.hflip'])
                camera_config["size"] = (config['camera.resolution.width'], config['camera.resolution.height'])
                logging.debug(camera_config["size"])

                focus_m = config['camera.focus_m']

                logging.debug('focus_m is ' + str(focus_m))

                if(focus_m < 0.1):
                    focus_m = 0.1
                
                if(focus_m > 100):
                    focus_m = 100

                lensposition = 1/focus_m

                logging.debug('setting lens position to ' + str(lensposition))

                camera.set_controls({"AfMode": controls.AfModeEnum.Manual, "LensPosition": lensposition})
                # camera.rotation = config['camera.rotation']
                camera.configure(camera_config)

                logging.debug('beginning capture')
                #camera.start_preview(Preview.DRM)
                camera.start()
                # Camera warm-up time
                logging.debug('warming up camera...')
                time.sleep(5)
                logging.debug('ready')

                IMAGEFILENAME = workingImageFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.jpg')
                camera.capture_file(IMAGEFILENAME)
                logging.debug('image saved to working folder')
                shutil.move(IMAGEFILENAME, pendingImageFolder + pathlib.Path(IMAGEFILENAME).name)
                logging.debug('image moved to pending folder')

            logging.debug('destroying camera object')
            

            if config['shutdown']:
                break
            else:
                time.sleep(config['camera.interval'])

    except Exception as e:
        logging.error("SavePhoto() failed.")
        logging.error(e)


try:
    logging.info('In savePhotos.py')
    if config['shutdown']:
        logging.info('Setting failsafe power off for 2 minutes 30 seconds from now.')
        pj.power.SetPowerOff(150)   # Fail safe turn the thing off

    while True:
        savePhotos()
        logging.warning("Bailed out of savePhotos() - let's pause to catch our breath...")
        # If we get here something went wrong. Let's pause for a bit and try again.
        time.sleep(30)
except Exception as e:
    logging.error("Catastrophic failure.")
    logging.error(e)
