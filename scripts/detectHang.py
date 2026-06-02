import subprocess
import json
from pvpi import PvPiClient
import os
import sys
import time
import datetime
import logging
import glob
import pathlib

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X

config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
logFilePath = config["logFilePath"]
intentLogFilePath = logFilePath.replace("timelapse.log", "intent.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = logging.StreamHandler(sys.stderr)
handler.setFormatter(formatter)
logger = logging.getLogger("detectHang")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

handlerIntent = logging.FileHandler(intentLogFilePath)
handlerIntent.setFormatter(formatter)
loggerIntent = logging.getLogger("intent")
loggerIntent.addHandler(handlerIntent)
loggerIntent.setLevel(logging.DEBUG)

# logger.info("Starting up detectHang.py...")
# loggerIntent.info("Starting up detectHang.py...")
# os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.

outputImageFolder = str(pathlib.Path(__file__).parent / '../output/images/')
workingImageFolder = os.path.join(outputImageFolder , 'working/')
pendingImageFolder = os.path.join(outputImageFolder , 'pending/')
uploadedImageFolder = os.path.join(outputImageFolder , 'uploaded/')

outputTelemetryFolder = str(pathlib.Path(__file__).parent / '../output/telemetry/')
pendingTelemetryFolder = os.path.join(outputTelemetryFolder , 'pending/')
uploadedTelemetryFolder = os.path.join(outputTelemetryFolder , 'uploaded/')

# pvpi
time.sleep(10)
pvpiClient = None
try:
    pvpiClient = PvPiClient()
except:
    logger.error("PvPi not connected - PvPi functionality will not be available")

def pj_is_alive():
    try:
        return pvpiClient is not None and pvpiClient.get_alive()
    except Exception:
        return False

def detectHang():
    try:

        config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))

        hung = False

        uptimeSeconds = int(time.clock_gettime(time.CLOCK_BOOTTIME))



        # If we've been up for more than 45 minutes, and the most recently captured image is older than 10 minutes, or the most recently uploaded image is older than 30 minutes, 
        # either network is out, or we can't get a cellular signal, DNS is messing around, or camera isn't capturing, and the saveTelemetry script hasn't caught it - it may have hung.
        # Let's bounce to give everything a chance to settle.
        
        if uptimeSeconds > 45 * 60:
            mostRecentUploadedFiles = sorted(glob.iglob(uploadedImageFolder + "/*.*"), key=os.path.getctime, reverse=True)
            mostRecentPendingFiles = sorted(glob.iglob(pendingImageFolder + "/*.*"), key=os.path.getctime, reverse=True)

            secondsSinceLastUpload = -1
            secondsSinceLastImageCapture = -1

            if len(mostRecentPendingFiles) > 0:
                latestImageCapturedFilename = max(mostRecentPendingFiles, key=os.path.getctime)
                secondsSinceLastImageCapture = (datetime.datetime.now() - datetime.datetime.fromtimestamp(os.path.getctime(latestImageCapturedFilename))).total_seconds()
                # logger.debug("secondsSinceLastImageCapture: " + str(secondsSinceLastImageCapture))

            if len(mostRecentUploadedFiles) > 0:

                latestUploadedFilename = max(mostRecentUploadedFiles, key=os.path.getctime)
                # logger.debug("latestUploadedFilename: " + str(latestUploadedFilename))

                secondsSinceLastUpload = (datetime.datetime.now() - datetime.datetime.fromtimestamp(os.path.getctime(latestUploadedFilename))).total_seconds()
                # logger.debug("secondsSinceLastUpload: " + str(secondsSinceLastUpload))


            # Most recent image captured (may also be in uploaded folder) is older than 10 minutes
            if secondsSinceLastImageCapture > 600 and secondsSinceLastUpload > 600:
                logger.warning('detectHang - Most recent captured image is ' + str(secondsSinceLastImageCapture) + 'seconds old, and uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                loggerIntent.warning('detectHang - Most recent captured image is ' + str(secondsSinceLastImageCapture) + 'seconds old, and uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                hung = True

            if secondsSinceLastUpload > 1800:
                logger.warning('detectHang - Most recent uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                loggerIntent.warning('detectHang - Most recent uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                hung = True

            if len(mostRecentPendingFiles) == 0 and len(mostRecentUploadedFiles) == 0:
                logger.debug("detectHang - No uploaded or captured images found - restarting...")
                loggerIntent.debug("detectHang - No uploaded or captured images found - restarting...")
                hung = True


        if hung == True:
            logger.info("detectHang - we're bouncing...")
            loggerIntent.info("detectHang - we're bouncing...")

            if pj_is_alive():
                logger.debug('get_mcu_time(): ' + str(pvpiClient.get_mcu_time()))
                loggerIntent.debug('get_mcu_time(): ' + str(pvpiClient.get_mcu_time()))
        
            # logger.info('Setting System Power Switch to Off:')
            # pj.power.SetSystemPowerSwitch(0)
            powerDownSIM7600X()
            logger.info('detectHang - Restarting after hang now...')
            loggerIntent.info('detectHang - Restarting after hang now...')
            subprocess.call(['sudo', 'shutdown', '-r', 'now'])

    except Exception as e:
        logger.error("detectHang() failed.")
        logger.error(e)




# Simpler catch all script that detects other scripts have hung, and restarts the Pi if they have.
# Called by Cron every 15 minutes.

try:
    # logger.info('In detectHang.py')

    detectHang()
except Exception as e:
    logger.error("Catastrophic failure.")
    logger.error(e)
