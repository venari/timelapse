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
from logging.handlers import TimedRotatingFileHandler
import pathlib

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
logFilePath = logFilePath.replace(".log", ".savePhotos.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)
# os.chmod(os.path.dirname(logFilePath), 0o777) # Make sure pijuice user scrip can write to log file.

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = TimedRotatingFileHandler(logFilePath, when='midnight', backupCount=10)
handler.setFormatter(formatter)
logger = logging.getLogger("savePhotos")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info("Starting up savePhotos.py...")
os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.

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
            logger.debug('creating camera object...')
            with Picamera2() as camera:

                config = json.load(open('config.json'))
                #camera_config = camera.create_preview_configuration()
                camera_config = camera.create_still_configuration()
                camera_config["transform"] = Transform(vflip = config['camera.vflip'], hflip = config['camera.hflip'])
                camera_config["size"] = (config['camera.resolution.width'], config['camera.resolution.height'])
                logger.debug(camera_config["size"])

                focus_m = config['camera.focus_m']

                logger.debug('focus_m is ' + str(focus_m))

                if(focus_m < 0.1):
                    focus_m = 0.1
                
                if(focus_m > 100):
                    focus_m = 100

                lensposition = 1/focus_m

                logger.debug('setting lens position to ' + str(lensposition))

                # camera.rotation = config['camera.rotation']
                camera.configure(camera_config)

                if(config['camera.long_exposure_mode']):
                    camera.set_controls({"AfMode": controls.AfModeEnum.Manual, "LensPosition": lensposition, "AeEnable": False, "ExposureTime": config['camera.long_exposure_time'], "AnalogueGain": config['camera.analogue_gain']}) #, "ColourGains": (2, 1.81)})
                else:
                    camera.set_controls({"AfMode": controls.AfModeEnum.Manual, "LensPosition": lensposition})
                    
                camera.options["quality"] = config['camera.quality']

                logger.debug('beginning capture')
                #camera.start_preview(Preview.DRM)
                camera.start()
                # Camera warm-up time
                logger.debug('warming up camera...')
                time.sleep(5)
                logger.debug('ready')

                IMAGEFILENAME = workingImageFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.jpg')
                camera.capture_file(IMAGEFILENAME)
                logger.debug('image saved to working folder')
                shutil.move(IMAGEFILENAME, pendingImageFolder + pathlib.Path(IMAGEFILENAME).name)
                logger.debug('image moved to pending folder')

            logger.debug('destroying camera object')
            

            if config['shutdown']:
                break
            else:
                if config['monitoringMode']:
                    logger.debug('monitoring mode is true, so no capture delay....')
                else:
                    time.sleep(config['camera.interval'])

    except Exception as e:
        logger.error("SavePhoto() failed.")
        logger.error(e)


try:
    logger.info('In savePhotos.py')

    while True:
        savePhotos()

        if not config['shutdown']:
            logger.warning("Bailed out of savePhotos() - let's pause to catch our breath...")
            # If we get here something went wrong. Let's pause for a bit and try again.
            time.sleep(30)
            
except Exception as e:
    logger.error("Catastrophic failure.")
    logger.error(e)
