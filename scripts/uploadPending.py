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
import glob
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
logging.info("Starting up uploadPending.py...")

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
logging.info("Starting up uploadPending.py 2...")


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


            logging.info(' uploading ' + IMAGEFILENAME)

            imageTimestamp = datetime.datetime.strptime(pathlib.Path(IMAGEFILENAME).stem, '%Y-%m-%d_%H%M%S')
            logging.debug('imageTimestamp:')
            logging.debug(imageTimestamp)

            if os.stat(IMAGEFILENAME).st_size == 0:
              logging.error('Empty file - deleting')
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

            logging.debug('data:')
            logging.debug(data)

            session = requests.Session()
            logging.debug('Posting image to API...')
            response = session.post(config['apiUrl'] + 'Image', files=files, data=data)

            logging.debug(f'Response code: {response.status_code}')
            if response.status_code == 200:
                logging.debug(f'Image uploaded successfully')
                shutil.move(IMAGEFILENAME, uploadedImageFolder + pathlib.Path(IMAGEFILENAME).name)

            else:
                logging.error(f'Image upload failed')

            logging.debug(f'Response text:')
            try:
                logging.debug(json.dumps(json.loads(response.text), indent = 4))
            except json.decoder.JSONDecodeError:
                logging.debug(response.text)

        if pendingFilesProcessed < 10:
            logging.info('No more pending images to upload.')
            power_interval = config['modem.power_interval']
            if power_interval > 0:
                logging.info('Current System Power Switch:')
                logging.info(pj.power.GetSystemPowerSwitch())
                logging.info('Setting System Power Switch to Off:')
                pj.power.SetSystemPowerSwitch(0)
                logging.info('Sleeping for ' + str(power_interval) + ' seconds...')
                time.sleep(power_interval)
                logging.info('Setting System Power Switch to 500:')
                pj.power.SetSystemPowerSwitch(500)
                logging.info('System Power Switch set to 500.')
                # Delay for 5 seconds to allow modem to power down

    except Exception as e:
        logging.error(str(datetime.datetime.now()) + " uploadPendingPhotos() failed.")
        logging.error(e)

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

            logging.info(' uploading ' + telemetryFilename)

            telemetryTimestamp = datetime.datetime.strptime(pathlib.Path(telemetryFilename).stem, '%Y-%m-%d_%H%M%S')
            logging.debug('telemetryTimestamp:')
            logging.debug(telemetryTimestamp)

            if os.stat(telemetryFilename).st_size == 0:
                os.remove(telemetryFilename)
                # empty file, will throw JSONDecodeError
                continue

            api_data = json.load(open(telemetryFilename, 'rb'))

            api_data['Timestamp'] = telemetryTimestamp.astimezone().isoformat()

            logging.debug(api_data)

            postResponse = session.post(config['apiUrl'] + 'Telemetry',data=api_data)
            logging.debug(postResponse)
            assert postResponse.status_code == 200, "API returned error code"
            #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

            if postResponse.status_code == 200:
                logging.debug(f'Telemetry uploaded successfully')
                shutil.move(telemetryFilename, uploadedTelemetryFolder + pathlib.Path(telemetryFilename).name)
                logging.debug('Logged to API.')

    except Exception as e:
        logging.error(str(datetime.datetime.now()) + " uploadPendingTelemetry() failed.")
        logging.error(e)

def deleteOldUploadedImagesAndTelemetry():

    try:
      now = time.time()
      os.makedirs(uploadedImageFolder, exist_ok = True)

      for uploadedImageFilename in os.listdir(uploadedImageFolder):
        uploadedImageFilename = os.path.join(uploadedImageFolder, uploadedImageFilename)
        if os.stat(uploadedImageFilename).st_mtime < now - 1 * 86400:
          if os.path.isfile(uploadedImageFilename):
            logging.info(' deleting old uploaded image ' + uploadedImageFilename)
            os.remove(uploadedImageFilename)

      os.makedirs(uploadedTelemetryFolder, exist_ok = True)

      for uploadedTelemetryFilename in os.listdir(uploadedTelemetryFolder):
        uploadedTelemetryFilename = os.path.join(uploadedTelemetryFolder, uploadedTelemetryFilename)
        if os.stat(uploadedTelemetryFilename).st_mtime < now - 1 * 86400:
          if os.path.isfile(uploadedTelemetryFilename):
            logging.info(' deleting old uploaded telemetry ' + uploadedTelemetryFilename)
            os.remove(uploadedTelemetryFilename)

    except Exception as e:
        logging.error(str(datetime.datetime.now()) + " deleteOldUploadedImagesAndTelemetry() failed.")
        logging.error(e)


try:
    while True:

      config = json.load(open('config.json'))
      deleteOldUploadedImagesAndTelemetry()
      uploadPendingTelemetry()
      uploadPendingPhotos()
      time.sleep(10)

except Exception as e:
    logging.error(e)
