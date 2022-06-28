import subprocess
import json
import pijuice
import os
import time
import shutil
import datetime
import sys
import requests

config = json.load(open('config.json'))

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
            print(str(datetime.datetime.now()) + ' uploading ' + IMAGEFILENAME)

            imageTimestamp = datetime.datetime.strptime(IMAGEFILENAME, '%Y-%m-%d_%H%M%S.jpg')
            print('imageTimestamp:')
            print(imageTimestamp)

            files = {
                'File': open(pendingImageFolder + IMAGEFILENAME, 'rb'),
            }

            data = {
                'SerialNumber': serialNumber,
                # 'Timestamp': (datetime.datetime.utcfromtimestamp(imageTimestamp.timestamp)).strftime('%Y-%m-%d %H:%M:%S')
                'Timestamp': imageTimestamp.astimezone().isoformat()
            }

            print('data:')
            print(data)

            session = requests.Session()
            print('Posting image to API...')
            response = session.post(config['apiUrl'] + 'Image', files=files, data=data)

            print(f'Response code: {response.status_code}')
            if response.status_code == 200:
                print(f'Image uploaded successfully')
                shutil.move(pendingImageFolder + IMAGEFILENAME, uploadedImageFolder + IMAGEFILENAME)

            else:
                print(f'Image upload failed')

            print(f'Response text:')
            try:
                print(json.dumps(json.loads(response.text), indent = 4))
            except json.decoder.JSONDecodeError:
                print(response.text)
    except Exception as e:
        print(str(datetime.datetime.now()) + " uploadPendingPhotos() failed.")
        print(e)

def uploadPendingTelemetry():

    try:
        os.makedirs(pendingTelemetryFolder, exist_ok = True)
        os.makedirs(uploadedTelemetryFolder, exist_ok = True)
        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)
        session = requests.Session()
        for telemetryFilename in os.listdir(pendingTelemetryFolder):
            print(str(datetime.datetime.now()) + ' uploading ' + telemetryFilename)

            telemetryTimestamp = datetime.datetime.strptime(telemetryFilename, '%Y-%m-%d_%H%M%S.json')
            print('telemetryTimestamp:')
            print(telemetryTimestamp)

            api_data = json.load(open(pendingTelemetryFolder + telemetryFilename, 'rb'))

            print(api_data)

            postResponse = session.post(config['apiUrl'] + 'Telemetry',data=api_data)
            print(postResponse)
            assert postResponse.status_code == 200, "API returned error code"
            #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

            if postResponse.status_code == 200:
                print(f'Telemetry uploaded successfully')
                shutil.move(pendingTelemetryFolder + telemetryFilename, uploadedTelemetryFolder + telemetryFilename)
                print(str(datetime.datetime.now()) + ' Logged to API.')

    except Exception as e:
        print(str(datetime.datetime.now()) + " uploadPendingTelemetry() failed.")
        print(e)

def deleteOldUploadedImages():

    try:
      now = time.time()
      os.makedirs(uploadedImageFolder, exist_ok = True)
      for uploadedImageFilename in os.listdir(uploadedImageFolder):
        if os.stat(uploadedImageFilename).st_mtime < now - 7 * 86400:
          if os.path.isfile(uploadedImageFilename):
            print(str(datetime.datetime.now()) + ' deleting old uploaded file ' + uploadedImageFilename)
            os.remove(os.path.join(uploadedImageFolder, uploadedImageFilename))

    except Exception as e:
        print(str(datetime.datetime.now()) + " deleteOldUploadedImages() failed.")
        print(e)


try:
    while True:

      deleteOldUploadedImages()
      uploadPendingTelemetry()
      uploadPendingPhotos()
      time.sleep(10)
      
except Exception as e:
    print(e)
