import subprocess
import json
import pijuice
import os
import time
import shutil
import datetime
import sys
import requests
import logging
from logging.handlers import TimedRotatingFileHandler
import glob
import pathlib

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = TimedRotatingFileHandler(logFilePath, 
                                   when='midnight',
                                   backupCount=10)
handler.setFormatter(formatter)
logger = logging.getLogger("uploadPending")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info("Starting up uploadPending.py...")

outputImageFolder = '../output/images/'
pendingImageFolder = outputImageFolder + 'pending/'
uploadedImageFolder = outputImageFolder + 'uploaded/'

outputTelemetryFolder = '../output/telemetry/'
pendingTelemetryFolder = outputTelemetryFolder + 'pending/'
uploadedTelemetryFolder = outputTelemetryFolder + 'uploaded/'

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

time.sleep(10)
pj = pijuice.PiJuice(1, 0x14)
logger.info("Starting up uploadPending.py 2...")


def uploadPendingPhotos():
    try:
        os.makedirs(pendingImageFolder, exist_ok = True)
        os.makedirs(uploadedImageFolder, exist_ok = True)

        mostRecentPendingFiles = sorted(glob.iglob(pendingImageFolder + "/*.*"), key=os.path.getctime, reverse=True)

        pendingFilesProcessed=0
        for IMAGEFILENAME in mostRecentPendingFiles:
            
            # Process in batches of 10:
            pendingFilesProcessed+=1
            if pendingFilesProcessed > 10:
                break


            logger.info(' uploading ' + IMAGEFILENAME)

            imageTimestamp = datetime.datetime.strptime(pathlib.Path(IMAGEFILENAME).stem, '%Y-%m-%d_%H%M%S')
            logger.debug('imageTimestamp:')
            logger.debug(imageTimestamp)

            if os.stat(IMAGEFILENAME).st_size == 0:
              logger.error('Empty file - deleting')
              os.remove(IMAGEFILENAME)
              continue

            files = {
                'File': open(IMAGEFILENAME, 'rb'),
            }

            data = {
                'SerialNumber': serialNumber,
                # 'Timestamp': (datetime.datetime.utcfromtimestamp(imageTimestamp.timestamp)).strftime('%Y-%m-%d %H:%M:%S')
                'Timestamp': imageTimestamp.astimezone().isoformat()
            }

            logger.debug('data:')
            logger.debug(data)

            session = requests.Session()
            logger.debug('Posting image to API...')
            response = session.post(config['apiUrl'] + 'Image', files=files, data=data)

            logger.debug(f'Response code: {response.status_code}')
            if response.status_code == 200:
                logger.debug(f'Image uploaded successfully')
                shutil.move(IMAGEFILENAME, uploadedImageFolder + pathlib.Path(IMAGEFILENAME).name)

            else:
                logger.error(f'Image upload failed')

            logger.debug(f'Response text:')
            try:
                logger.debug(json.dumps(json.loads(response.text), indent = 4))
            except json.decoder.JSONDecodeError:
                logger.debug(response.text)

        if pendingFilesProcessed < 10:
            logger.info('No more pending images to upload.')
            power_interval = config['modem.power_interval']
            if power_interval > 0:
                logger.info('Current System Power Switch:')
                logger.info(pj.power.GetSystemPowerSwitch())
                logger.info('Setting System Power Switch to Off:')
                pj.power.SetSystemPowerSwitch(0)
                logger.info('Sleeping for ' + str(power_interval) + ' seconds...')
                time.sleep(power_interval)
                sysVoltage = pj.status.GetBatteryVoltage()['data']
                if sysVoltage < 3.2:  # 3.2V is the minimum voltage for the XL6009
                    logger.info('Battery voltage too low for XL6009 - not powering up modem.')
                    return
                logger.info('System Voltage looks good at ' + str(sysVoltage) + 'V')
                logger.info('Setting System Power Switch to 2100mA:')
                pj.power.SetSystemPowerSwitch(2100)
                logger.info('System Power Switch set to 2100mA.')
                # Delay for 30 seconds to allow modem to power up and connect to network

    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " uploadPendingPhotos() failed.")
        logger.error(e)

def uploadPendingTelemetry():

    try:
        os.makedirs(pendingTelemetryFolder, exist_ok = True)
        os.makedirs(uploadedTelemetryFolder, exist_ok = True)
        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)
        session = requests.Session()

        mostRecentTelemetryFiles = sorted(glob.iglob(pendingTelemetryFolder + "/*.json"), key=os.path.getctime, reverse=True)

        pendingFilesProcessed=0
        for telemetryFilename in mostRecentTelemetryFiles:
            
            # Process in batches of 100:
            pendingFilesProcessed+=1
            if pendingFilesProcessed > 100:
                break

            logger.info(' uploading ' + telemetryFilename)

            telemetryTimestamp = datetime.datetime.strptime(pathlib.Path(telemetryFilename).stem, '%Y-%m-%d_%H%M%S')
            logger.debug('telemetryTimestamp:')
            logger.debug(telemetryTimestamp)

            if os.stat(telemetryFilename).st_size == 0:
                os.remove(telemetryFilename)
                # empty file, will throw JSONDecodeError
                continue

            api_data = json.load(open(telemetryFilename, 'rb'))

            api_data['Timestamp'] = telemetryTimestamp.astimezone().isoformat()

            logger.debug(api_data)

            postResponse = session.post(config['apiUrl'] + 'Telemetry',data=api_data)
            logger.debug(postResponse)
            assert postResponse.status_code == 200, "API returned error code"
            #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

            if postResponse.status_code == 200:
                logger.debug(f'Telemetry uploaded successfully')
                shutil.move(telemetryFilename, uploadedTelemetryFolder + pathlib.Path(telemetryFilename).name)
                logger.debug('Logged to API.')

    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " uploadPendingTelemetry() failed.")
        logger.error(e)

def deleteOldUploadedImagesAndTelemetry():

    try:
      now = time.time()
      os.makedirs(uploadedImageFolder, exist_ok = True)

      for uploadedImageFilename in os.listdir(uploadedImageFolder):
        uploadedImageFilename = os.path.join(uploadedImageFolder, uploadedImageFilename)
        if os.stat(uploadedImageFilename).st_mtime < now - 1 * 86400:
          if os.path.isfile(uploadedImageFilename):
            logger.info(' deleting old uploaded image ' + uploadedImageFilename)
            os.remove(uploadedImageFilename)

      os.makedirs(uploadedTelemetryFolder, exist_ok = True)

      for uploadedTelemetryFilename in os.listdir(uploadedTelemetryFolder):
        uploadedTelemetryFilename = os.path.join(uploadedTelemetryFolder, uploadedTelemetryFilename)
        if os.stat(uploadedTelemetryFilename).st_mtime < now - 1 * 86400:
          if os.path.isfile(uploadedTelemetryFilename):
            logger.info(' deleting old uploaded telemetry ' + uploadedTelemetryFilename)
            os.remove(uploadedTelemetryFilename)

    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " deleteOldUploadedImagesAndTelemetry() failed.")
        logger.error(e)


try:
    while True:

      config = json.load(open('config.json'))
      deleteOldUploadedImagesAndTelemetry()
      uploadPendingTelemetry()
      uploadPendingPhotos()
      time.sleep(10)

except Exception as e:
    logger.error(e)
