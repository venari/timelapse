import subprocess
import json
import pijuice
import os
import time
import datetime
import logging
# from logging.handlers import TimedRotatingFileHandler
from logging.handlers import SocketHandler
import glob
import pathlib

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X

config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
logFilePath = config["logFilePath"]
intentLogFilePath = logFilePath.replace("timelapse.log", "intent.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
# handler = TimedRotatingFileHandler(logFilePath, 
#                                    when='midnight',
#                                    backupCount=10)
handler = SocketHandler('localhost', 8000)
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
workingImageFolder = outputImageFolder + 'working/'
pendingImageFolder = outputImageFolder + 'pending/'
uploadedImageFolder = outputImageFolder + 'uploaded/'

outputTelemetryFolder = str(pathlib.Path(__file__).parent / '../output/telemetry/')
pendingTelemetryFolder = outputTelemetryFolder + 'pending/'
uploadedTelemetryFolder = outputTelemetryFolder + 'uploaded/'

# pijuice
time.sleep(10)
pj = pijuice.PiJuice(1, 0x14)

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

            logger.debug('rtcAlarm.GetControlStatus(): ' + str(pj.rtcAlarm.GetControlStatus()))
            logger.debug('rtcAlarm.GetTime(): ' + str(pj.rtcAlarm.GetTime()))
            loggerIntent.debug('rtcAlarm.GetControlStatus(): ' + str(pj.rtcAlarm.GetControlStatus()))
            loggerIntent.debug('rtcAlarm.GetTime(): ' + str(pj.rtcAlarm.GetTime()))
            loggerIntent.debug('power.GetWatchdog(): ' + str(pj.power.GetWatchdog()))
        
            logger.info('Setting System Power Switch to Off:')
            pj.power.SetSystemPowerSwitch(0)
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
