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
import socket

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
logFilePath = logFilePath.Replace(".log", "uploadTelemetry.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)
# os.chmod(os.path.dirname(logFilePath), 0o777) # Make sure pijuice user scrip can write to log file.

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = TimedRotatingFileHandler(logFilePath, 
                                   when='midnight',
                                   backupCount=10)
handler.setFormatter(formatter)
logger = logging.getLogger("uploadPending")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info("******************************************************************************")
logger.info("")
logger.info("Starting up uploadPending.py...")
logger.info("")
logger.info("******************************************************************************")
os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.

outputImageFolder = '../output/images/'
pendingImageFolder = outputImageFolder + 'pending/'
uploadedImageFolder = outputImageFolder + 'uploaded/'

outputTelemetryFolder = '../output/telemetry/'
pendingTelemetryFolder = outputTelemetryFolder + 'pending/'
uploadedTelemetryFolder = outputTelemetryFolder + 'uploaded/'
holdTelemetryFolder = outputTelemetryFolder + 'hold/'

bInSupportWindow = False

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
        global bInSupportWindow
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
            response = session.post(config['apiUrl'] + 'Image', files=files, data=data, timeout=30)

            logger.debug(f'Response code: {response.status_code}')
            if response.status_code == 200:
                logger.debug(f'Image uploaded successfully')
                shutil.move(IMAGEFILENAME, uploadedImageFolder + pathlib.Path(IMAGEFILENAME).name)

            else:
                logger.error(f'Image upload failed')

            logger.debug(f'Response text:')
            try:
                logger.debug(json.dumps(json.loads(response.text), indent = 4))

                if json.loads(response.text)['device']['supportMode'] != config['supportMode']:
                    logger.info('Support mode changed to ' + str(json.loads(response.text)['device']['supportMode']))
                    config['supportMode'] = json.loads(response.text)['device']['supportMode']
                    json.dump(config, open('config.json', 'w'), indent=4)

                if json.loads(response.text)['device']['monitoringMode'] != config['monitoringMode']:
                    logger.info('Monitoring mode changed to ' + str(json.loads(response.text)['device']['monitoringMode']))
                    config['monitoringMode'] = json.loads(response.text)['device']['monitoringMode']
                    json.dump(config, open('config.json', 'w'), indent=4)

                   
            except json.decoder.JSONDecodeError:
                logger.debug(response.text)

        if pendingFilesProcessed < 10:
            logger.info('No more pending images to upload.')
            power_interval = config['modem.power_interval']
            if power_interval > 0:

                # Give the telemetry an opportunity to upload before powering off modem
                uploadPendingTelemetry()

                # If it's 9am or 12pm or 5pm, don't turn the modem off for 15 minutes, 
                # or config['supportMode'] == True
                if ((datetime.datetime.now().hour == 9 or datetime.datetime.now().hour == 12 or datetime.datetime.now().hour == 17) and datetime.datetime.now().minute < 15) or config['supportMode']==True:
                    if not bInSupportWindow:
                        logger.info('Opening minute support window...')
                        bInSupportWindow = True

                else:
                    if bInSupportWindow:
                        logger.info('Closing support window...')
                        bInSupportWindow = False
                    logger.info('Current System Power Switch:')
                    logger.info(pj.power.GetSystemPowerSwitch())
                    logger.info('Setting System Power Switch to Off:')
                    pj.power.SetSystemPowerSwitch(0)
                    logger.info('Sleeping for ' + str(power_interval) + ' seconds...')
                    time.sleep(power_interval)
                    turnOnSystemPowerSwitch()

    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " uploadPendingPhotos() failed.")
        logger.error(e)

# https://stackoverflow.com/questions/3764291/how-can-i-see-if-theres-an-available-and-active-network-connection-in-python
def internet(host="8.8.8.8", port=53, timeout=3):
    """
    Host: 8.8.8.8 (google-public-dns-a.google.com)
    OpenPort: 53/tcp
    Service: domain (DNS/TCP)
    """
    try:
        socket.setdefaulttimeout(timeout)
        socket.socket(socket.AF_INET, socket.SOCK_STREAM).connect((host, port))
        return True
    except socket.error as ex:
        logger.warning(e)
        return False

def turnOnSystemPowerSwitch(retries = 3):
    try:
        sysVoltage = pj.status.GetBatteryVoltage()['data']
        # if sysVoltage < 3.2:  # 3.2V is the minimum voltage for the XL6009
        #     logger.info('Battery voltage too low for XL6009 - not powering up modem.')
        #     return
        if sysVoltage < 3.2:  # 3.0V is a bit on the low side
            logger.info('Battery voltage too low - not powering up modem.')
            return
        logger.info('System Voltage looks good at ' + str(sysVoltage) + 'mV')

        modemPower = config['modem.power']
        if modemPower <= 0:
            logger.info('Modem power is disabled in config.')
            return
        
        logger.info('Current System Power Switch:')
        logger.info(pj.power.GetSystemPowerSwitch())
        logger.info('Setting System Power Switch to ' + str(modemPower) + ':')
        pj.power.SetSystemPowerSwitch(modemPower)

        # logger.info('Waiting 50s for modem to warm up...')
        # time.sleep(50)

        logger.info('Waiting for network....')
        # Call Internet function to wait for network, for a max of 2 minutes
        waitCounter = 0
        while not internet() and waitCounter < 12:
            time.sleep(10)
            logger.info('Still waiting for network....')
            waitCounter=waitCounter+1
        
        if waitCounter < 120:
            logger.info('Network connection established.')
       
        else:
            logger.warning('Could not establish network connection after 2 minutes.')
            if retries > 0:
                logger.info('Retrying to establish network connection...')
                turnOnSystemPowerSwitch(retries-1)
            else:
                logger.warning('Retries exhausted - giving up.')
                logger.info('Setting System Power Switch to Off')
                pj.power.SetSystemPowerSwitch(0)
                return
    
    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " turnOnSystemPowerSwitch() failed.")
        logger.error(e)

def uploadPendingTelemetry():

    try:
        os.makedirs(pendingTelemetryFolder, exist_ok = True)
        os.makedirs(uploadedTelemetryFolder, exist_ok = True)
        os.makedirs(holdTelemetryFolder, exist_ok = True)
        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)
        session = requests.Session()

        mostRecentTelemetryFiles = sorted(glob.iglob(pendingTelemetryFolder + "/*.json"), key=os.path.getctime, reverse=True)

        pendingFilesProcessed=0
        lastAttemptedFilename = ''
        for telemetryFilename in mostRecentTelemetryFiles:
            
            lastAttemptedFilename = telemetryFilename
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

            postResponse = session.post(config['apiUrl'] + 'Telemetry',data=api_data, timeout=5)
            logger.debug(postResponse)
            assert postResponse.status_code == 200, "API returned error code"
            #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

            if postResponse.status_code == 200:
                logger.debug(f'Telemetry uploaded successfully')
                shutil.move(telemetryFilename, uploadedTelemetryFolder + pathlib.Path(telemetryFilename).name)
                logger.debug('Logged to API.')

    except requests.exceptions.ConnectionError as e:
        logger.error(str(datetime.datetime.now()) + " uploadPendingTelemetry() failed - connection error. Leave in place.")
        logger.error(e)
    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " uploadPendingTelemetry() failed.")
        logger.error(e)
        if lastAttemptedFilename!="":          
          logger.error("lastAttemptedFilename: " + lastAttemptedFilename)
          # os.remove(telemetryFilename)
          shutil.move(lastAttemptedFilename, holdTelemetryFolder + pathlib.Path(lastAttemptedFilename).name)
          logger.info("Moved " + lastAttemptedFilename + " to " + holdTelemetryFolder)

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
    
    turnOnSystemPowerSwitch()

    while True:

      config = json.load(open('config.json'))
      deleteOldUploadedImagesAndTelemetry()
      uploadPendingTelemetry()
      uploadPendingPhotos()
      time.sleep(30)

except Exception as e:
    logger.error(e)
