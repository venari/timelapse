from picamera import PiCamera
import logging
import os
import datetime
import time

logFilePath = "./timelapse-camera.log"
outputImageFolder = './output/images/'
pendingImageFolder = outputImageFolder + 'pending/'
uploadedImageFolder = outputImageFolder + 'uploaded/'

outputTelemetryFolder = './output/telemetry/'
pendingTelemetryFolder = outputTelemetryFolder + 'pending/'
uploadedTelemetryFolder = outputTelemetryFolder + 'uploaded/'

os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

logging.basicConfig(filename=logFilePath,
                    format='%(asctime)s %(levelname)s: %(message)s',
                    level = logging.DEBUG
                    # datefmt='%d/%m/%Y %I:%M:%S %p'
                    # encoding='utf-8'
                    )
# log = logging.getLogger()
logging.info("Starting up timelapse-camera main.py...")


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
print("serialNumber: " + serialNumber)
logging.debug("serialNumber: " + serialNumber)

print('API_URL')
apiUrl = os.environ['API_URL']
print(apiUrl)


print('CAMERA_INTERVAL')
print(os.environ['CAMERA_INTERVAL'])
print('CAMERA_VFLIP')
print(os.environ['CAMERA_VFLIP'])
print('CAMERA_HFLIP')
print(os.environ['CAMERA_HFLIP'])
print('CAMERA_ROTATION')
print(os.environ['CAMERA_ROTATION'])
print('CAMERA_RESOLUTION_WIDTH')
print(os.environ['CAMERA_RESOLUTION_WIDTH'])
print('CAMERA_RESOLUTION_HEIGHT')
print(os.environ['CAMERA_RESOLUTION_HEIGHT'])


def savePhotos():
    os.makedirs(outputImageFolder, exist_ok = True)
    os.makedirs(pendingImageFolder, exist_ok = True)
    # txtTime = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')

    try:
        logging.debug('creating camera object...')
        with PiCamera() as camera:

            camera.vflip = os.environ['CAMERA_VFLIP']
            camera.hflip = os.environ['CAMERA_HFLIP']
            camera.resolution = (os.environ['CAMERA_RESOLUTION_WIDTH'], os.environ['CAMERA_RESOLUTION_HEIGHT'])
            camera.rotation = os.environ['CAMERA_ROTATION']

            while True:
                logging.debug('beginning capture')
                camera.start_preview()
                # Camera warm-up time
                logging.debug('warming up camera...')
                time.sleep(5)
                logging.debug('ready')

                IMAGEFILENAME = pendingImageFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.jpg')
                camera.capture(IMAGEFILENAME)
                logging.debug('image saved')

                # saveTelemetry()
                # scheduleShutdown()
                # if config['shutdown']:
                #     break
                # else:
                time.sleep(os.environ['CAMERA_INTERVAL'])

    except Exception as e:
        logging.error("SavePhoto() failed.")
        logging.error(e)

# Initial variables
# i = 0

savePhotos()

# while True:

    # savePhotos()

    # # i = i + 1
    # sleep(60)
