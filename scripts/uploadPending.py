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

def uploadPendingPhotos():
    try:
        os.makedirs(pendingImageFolder, exist_ok = True)
        os.makedirs(uploadedImageFolder, exist_ok = True)
        for IMAGEFILENAME in os.listdir(pendingImageFolder):
            logging.info(' uploading ' + IMAGEFILENAME)

            imageTimestamp = datetime.datetime.strptime(IMAGEFILENAME, '%Y-%m-%d_%H%M%S.jpg')
            logging.debug('imageTimestamp:')
            logging.debug(imageTimestamp)

            files = {
                'File': open(pendingImageFolder + IMAGEFILENAME, 'rb'),
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
                shutil.move(pendingImageFolder + IMAGEFILENAME, uploadedImageFolder + IMAGEFILENAME)

            else:
                logging.error(f'Image upload failed')

            logging.debug(f'Response text:')
            try:
                logging.debug(json.dumps(json.loads(response.text), indent = 4))
            except json.decoder.JSONDecodeError:
                logging.debug(response.text)
    except Exception as e:
        logging.error(str(datetime.datetime.now()) + " uploadPendingPhotos() failed.")
        logging.error(e)

def uploadPendingTelemetry():

    try:
        os.makedirs(pendingTelemetryFolder, exist_ok = True)
        os.makedirs(uploadedTelemetryFolder, exist_ok = True)
        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)
        session = requests.Session()
        for telemetryFilename in os.listdir(pendingTelemetryFolder):
            logging.info(' uploading ' + telemetryFilename)

            telemetryTimestamp = datetime.datetime.strptime(telemetryFilename, '%Y-%m-%d_%H%M%S.json')
            logging.debug('telemetryTimestamp:')
            logging.debug(telemetryTimestamp)

            api_data = json.load(open(pendingTelemetryFolder + telemetryFilename, 'rb'))

            api_data.Timestamp = telemetryTimestamp.astimezone().isoformat()

            logging.debug(api_data)

            postResponse = session.post(config['apiUrl'] + 'Telemetry',data=api_data)
            logging.debug(postResponse)
            assert postResponse.status_code == 200, "API returned error code"
            #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

            if postResponse.status_code == 200:
                logging.debug(f'Telemetry uploaded successfully')
                shutil.move(pendingTelemetryFolder + telemetryFilename, uploadedTelemetryFolder + telemetryFilename)
                logging.debug('Logged to API.')

    except Exception as e:
        logging.error(str(datetime.datetime.now()) + " uploadPendingTelemetry() failed.")
        logging.error(e)

def deleteOldUploadedImages():

    try:
      now = time.time()
      os.makedirs(uploadedImageFolder, exist_ok = True)

      for uploadedImageFilename in os.listdir(uploadedImageFolder):
        uploadedImageFilename = os.path.join(uploadedImageFolder, uploadedImageFilename)
        if os.stat(uploadedImageFilename).st_mtime < now - 1 * 86400:
          if os.path.isfile(uploadedImageFilename):
            logging.info(' deleting old uploaded file ' + uploadedImageFilename)
            os.remove(uploadedImageFilename)

    except Exception as e:
        logging.error(str(datetime.datetime.now()) + " deleteOldUploadedImages() failed.")
        logging.error(e)


try:
    while True:

      deleteOldUploadedImages()
      uploadPendingTelemetry()
      uploadPendingPhotos()
      time.sleep(10)

except Exception as e:
    logging.error(e)
