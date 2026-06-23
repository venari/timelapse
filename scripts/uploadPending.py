import subprocess
import json
# import pijuice
import os
import time
import shutil
import datetime
import sys
import requests
import logging
import glob
import pathlib
import socket

from helpers import internet

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X, turnOnNDIS, forceTo4GConnection

config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
logFilePath = config["logFilePath"]
intentLogFilePath = logFilePath.replace("timelapse.log", "intent.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = logging.StreamHandler(sys.stderr)
handler.setFormatter(formatter)
logger = logging.getLogger("uploadPending")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

handlerIntent = logging.FileHandler(intentLogFilePath)
handlerIntent.setFormatter(formatter)
loggerIntent = logging.getLogger("intent")
loggerIntent.addHandler(handlerIntent)
loggerIntent.setLevel(logging.DEBUG)

logger.info("******************************************************************************")
logger.info("")
logger.info("Starting up uploadPending.py...")
logger.info("")
logger.info("******************************************************************************")
# os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.

loggerIntent.info("Starting up uploadPending.py...")

outputImageFolder = str(pathlib.Path(__file__).parent / '../output/images/')
workingImageFolder = os.path.join(outputImageFolder , 'working/')
pendingImageFolder = os.path.join(outputImageFolder , 'pending/')
uploadedImageFolder = os.path.join(outputImageFolder , 'uploaded/')

outputTelemetryFolder = str(pathlib.Path(__file__).parent / '../output/telemetry/')
pendingTelemetryFolder = os.path.join(outputTelemetryFolder , 'pending/')
uploadedTelemetryFolder = os.path.join(outputTelemetryFolder , 'uploaded/')
holdTelemetryFolder = os.path.join(outputTelemetryFolder , 'hold/')

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

# time.sleep(10)
# pj = pijuice.PiJuice(1, 0x14)
# logger.info("Starting up uploadPending.py 2...")


def updateConfigFromResponse(response):
    """
    Check API response for config changes and update local config if needed.
    
    Args:
        response: requests.Response object from API call
        
    Returns:
        None
    """
    try:
        responseData = json.loads(response.text)
        
        if 'device' not in responseData:
            return
            
        device = responseData['device']
        configChanged = False
        
        # Check supportMode
        if 'supportMode' in device and device['supportMode'] != config['supportMode']:
            logger.info('Support mode changed to ' + str(device['supportMode']))
            loggerIntent.info('Support mode changed to ' + str(device['supportMode']))
            config['supportMode'] = device['supportMode']
            configChanged = True
        
        # Check monitoringMode
        if 'monitoringMode' in device and device['monitoringMode'] != config['monitoringMode']:
            logger.info('Monitoring mode changed to ' + str(device['monitoringMode']))
            loggerIntent.info('Monitoring mode changed to ' + str(device['monitoringMode']))
            config['monitoringMode'] = device['monitoringMode']
            configChanged = True
        
        # Check hibernateMode
        if 'hibernateMode' in device and device['hibernateMode'] != config['hibernateMode']:
            logger.info('Hibernate mode changed to ' + str(device['hibernateMode']))
            loggerIntent.info('Hibernate mode changed to ' + str(device['hibernateMode']))
            config['hibernateMode'] = device['hibernateMode']
            configChanged = True
        
        # Check powerOff
        if 'powerOff' in device and device['powerOff'] != config['powerOff']:
            logger.info('Power Off changed to ' + str(device['powerOff']))
            loggerIntent.info('Power Off changed to ' + str(device['powerOff']))
            config['powerOff'] = device['powerOff']
            configChanged = True
        
        # Save config if anything changed
        if configChanged:
            json.dump(config, open(pathlib.Path(__file__).parent / 'config.json', 'w'), indent=4)
            
    except json.decoder.JSONDecodeError:
        logger.debug(response.text)
    except KeyError as e:
        logger.debug(f'Missing key in response: {e}')
    except Exception as e:
        logger.error(f'updateConfigFromResponse() failed: {e}')


def uploadPhoto(imageFilename):
    """
    Upload a single photo file to the API.
    
    Args:
        imageFilename: Path to the image file to upload
        
    Returns:
        bool: True if upload was successful, False otherwise
    """
    try:
        logger.info(' uploading ' + imageFilename)

        imageTimestamp = datetime.datetime.strptime(pathlib.Path(imageFilename).stem, '%Y-%m-%d_%H%M%S')
        logger.debug('imageTimestamp:')
        logger.debug(imageTimestamp)

        if os.stat(imageFilename).st_size == 0:
            logger.error('Empty file - deleting')
            os.remove(imageFilename)
            return False

        files = {
            'File': open(imageFilename, 'rb'),
        }

        data = {
            'SerialNumber': serialNumber,
            'Timestamp': imageTimestamp.astimezone().isoformat()
        }

        logger.debug('data:')
        logger.debug(data)

        session = requests.Session()
        logger.debug('Posting image to API...')
        response = session.post(config['apiUrl'] + 'Image', files=files, data=data, timeout=config['upload.image.timeout'])

        logger.debug(f'Response code: {response.status_code}')
        if response.status_code == 200:
            logger.debug(f'Image uploaded successfully')
            shutil.move(imageFilename, uploadedImageFolder + pathlib.Path(imageFilename).name)
            success = True
        else:
            logger.error(f'Image upload failed')
            success = False

        logger.debug(f'Response text:')
        logger.debug(json.dumps(json.loads(response.text), indent = 4) if response.text else 'Empty response')
        
        # Update config from API response
        updateConfigFromResponse(response)
        
        return success
        
    except Exception as e:
        logger.error(f'uploadPhoto() failed for {imageFilename}')
        logger.error(e)
        return False


def uploadPendingPhotos():
    try:
        global bInSupportWindow
        os.makedirs(pendingImageFolder, exist_ok = True)
        os.makedirs(uploadedImageFolder, exist_ok = True)

        mostRecentPendingFiles = sorted(glob.iglob(pendingImageFolder + "/*.*"), key=os.path.getctime, reverse=True)

        connectToInternet()

        pendingFilesProcessed=0
        for IMAGEFILENAME in mostRecentPendingFiles:
            
            # Process in batches of 10:
            pendingFilesProcessed+=1
            if pendingFilesProcessed > 10:
                break

            uploadPhoto(IMAGEFILENAME)

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
                        logger.info('Opening support window...')
                        bInSupportWindow = True

                else:
                    if bInSupportWindow:
                        logger.info('Closing support window...')
                        bInSupportWindow = False
                    disconnectFromInternet()
                    logger.info('Sleeping for ' + str(power_interval) + ' seconds...')
                    time.sleep(power_interval)

    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " uploadPendingPhotos() failed.")
        logger.error(e)

def connectToInternet(retries = 3):
    try:
        logger.info('Connecting to internet...')

        if(internet()):
            logger.info('Already connected to internet.')
            return

        # loggerIntent.info('Connecting to internet...')

        if(config['modem.type']=="thumb"):
            turnOnUSBPorts()
        else:
            powerUpSIM7600X()

        logger.info('Waiting for network....')
        # Call Internet function to wait for network, for a max of 2 minutes
        waitCounter = 0
        while not internet() and waitCounter < 12:
            time.sleep(10)
            logger.info('Still waiting for network....')
            waitCounter=waitCounter+1
        
        if internet():
            logger.info('Network connection established.')
       
        else:
            logger.warning('Could not establish network connection after 2 minutes.')

            if(config['modem.type']=="SIM7600X"):
                logger.info('forcing 4G Connection...')
                forceTo4GConnection()
                logger.info('Attempting to turn on NDIS...')
                turnOnNDIS()

            if retries > 0:
                logger.info('Retrying to establish network connection...')
                connectToInternet(retries-1)
            else:
                logger.warning('Retries exhausted - giving up.')
                disconnectFromInternet()
                return

    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " connectToInternet() failed.")
        logger.error(e)

def disconnectFromInternet():
    logger.info('Not really disconnecting from internet...')
    try:
        logger.info('Disconnecting from internet...')
        # loggerIntent.info('Disconnecting from internet...')
        if(config['modem.type']=="thumb"):
            turnOffUSBPorts()
        else:
            powerDownSIM7600X()
    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " disconnectFromInternet() failed.")
        logger.error(e)
        
def turnOnUSBPorts():
    try:
        # Check if we're on a Raspberry Pi 5
        try:
            with open('/proc/device-tree/model', 'r') as f:
                model = f.read().strip('\0')
                if 'Raspberry Pi 5' not in model:
                    logger.info(f'Not a Raspberry Pi 5 (detected: {model}) - skipping USB power-up.')
                    return
        except FileNotFoundError:
            logger.warning('Could not determine Pi model - /proc/device-tree/model not found.')
            return
        
        logger.info('Raspberry Pi 5 detected - powering up USB ports...')
        
        # On Pi 5, power up USB ports using uhubctl
        # Power up USB hub 4
        result1 = subprocess.run(['sudo', 'uhubctl', '-l', '4', '-a', '1'], 
                               capture_output=True, text=True, timeout=10)
        
        # Power up USB hub 2
        result2 = subprocess.run(['sudo', 'uhubctl', '-l', '2', '-a', '1'], 
                               capture_output=True, text=True, timeout=10)
        
        if result1.returncode == 0 and result2.returncode == 0:
            logger.info('USB ports powered up successfully.')
        else:
            if result1.returncode != 0:
                logger.error(f'Failed to power up USB hub 4: {result1.stderr}')
            if result2.returncode != 0:
                logger.error(f'Failed to power up USB hub 2: {result2.stderr}')
            
    except subprocess.TimeoutExpired:
        logger.error('Timeout while trying to power up USB ports.')
    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " turnOnUSBPorts() failed.")
        logger.error(e)

def turnOffUSBPorts():
    try:
        # Check if we're on a Raspberry Pi 5
        try:
            with open('/proc/device-tree/model', 'r') as f:
                model = f.read().strip('\0')
                if 'Raspberry Pi 5' not in model:
                    logger.info(f'Not a Raspberry Pi 5 (detected: {model}) - skipping USB power-down.')
                    return
        except FileNotFoundError:
            logger.warning('Could not determine Pi model - /proc/device-tree/model not found.')
            return
        
        logger.info('Raspberry Pi 5 detected - powering down USB ports...')
        
        # On Pi 5, power down USB ports using uhubctl
        # Power down USB hub 4
        result1 = subprocess.run(['sudo', 'uhubctl', '-l', '4', '-a', '0'], 
                               capture_output=True, text=True, timeout=10)
        
        # Power down USB hub 2
        result2 = subprocess.run(['sudo', 'uhubctl', '-l', '2', '-a', '0'], 
                               capture_output=True, text=True, timeout=10)
        
        if result1.returncode == 0 and result2.returncode == 0:
            logger.info('USB ports powered down successfully.')
        else:
            if result1.returncode != 0:
                logger.error(f'Failed to power down USB hub 4: {result1.stderr}')
            if result2.returncode != 0:
                logger.error(f'Failed to power down USB hub 2: {result2.stderr}')
            
    except subprocess.TimeoutExpired:
        logger.error('Timeout while trying to power down USB ports.')
    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " turnOffUSBPorts() failed.")
        logger.error(e)

def uploadTelemetry(telemetryFilename, session):
    """
    Upload a single telemetry file to the API.
    
    Args:
        telemetryFilename: Path to the telemetry file to upload
        session: requests.Session object to use for the upload
        
    Returns:
        bool: True if upload was successful, False otherwise
    """
    try:
        logger.info(' uploading ' + telemetryFilename)

        telemetryTimestamp = datetime.datetime.strptime(pathlib.Path(telemetryFilename).stem, '%Y-%m-%d_%H%M%S')
        logger.debug('telemetryTimestamp:')
        logger.debug(telemetryTimestamp)

        if os.stat(telemetryFilename).st_size == 0:
            os.remove(telemetryFilename)
            # empty file, will throw JSONDecodeError
            return False

        api_data = json.load(open(telemetryFilename, 'rb'))

        api_data['Timestamp'] = telemetryTimestamp.astimezone().isoformat()

        logger.debug(api_data)

        response = session.post(config['apiUrl'] + 'Telemetry',data=api_data, timeout=config['upload.telemetry.timeout'])
        logger.debug(response)
        assert response.status_code == 200, "API returned error code"
        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

        if response.status_code == 200:
            # flashLED(pj, 'D2', 0, 0, 255, 1, .1)
            logger.debug(f'Telemetry uploaded successfully')
            shutil.move(telemetryFilename, uploadedTelemetryFolder + pathlib.Path(telemetryFilename).name)
            logger.debug('Logged to API.')
            success = True
        else:
            # flashLED(pj, 'D2', 255, 0, 0, 1, 1)
            logger.error(f'Telemetry upload failed')
            success = False

        logger.debug(json.dumps(json.loads(response.text), indent = 4) if response.text else 'Empty response')
        
        # Update config from API response
        updateConfigFromResponse(response)
        
        return success
        
    except Exception as e:
        logger.error(f'uploadTelemetry() failed for {telemetryFilename}')
        logger.error(e)
        raise  # Re-raise to allow caller to handle


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

            uploadTelemetry(telemetryFilename, session)


    except requests.exceptions.ConnectionError as e:
        # flashLED(pj, 'D2', 255, 0, 255, 1, 1)
        logger.error(str(datetime.datetime.now()) + " uploadPendingTelemetry() failed - connection error. Leave in place.")
        logger.error(e)
    except Exception as e:
        # flashLED(pj, 'D2', 255, 0, 255, 1, 1)
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
    
    connectToInternet()

    while True:

      config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
      deleteOldUploadedImagesAndTelemetry()
      uploadPendingTelemetry()
      uploadPendingPhotos()
      time.sleep(30)

except Exception as e:
    logger.error(e)
