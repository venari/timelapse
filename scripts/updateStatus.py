#!/usr/bin/python
# -*- coding:utf-8 -*-
# from logging.handlers import TimedRotatingFileHandler
from logging.handlers import SocketHandler
import sys
import os
import json

import logging
import epaper
import time
import datetime
from PIL import Image,ImageDraw,ImageFont
import traceback
import glob
import subprocess
import pijuice
import socket
import shutil
import pathlib

from helpers import internet, flashLED

outputImageFolder = pathlib.Path(__file__).parent / '../output/images/'
imageMonitoringPreview = outputImageFolder / 'monitoringPreview.jpg'
mostRecentUploadedImage = outputImageFolder / 'monitoringPreviewMostRecentUploaded.jpg'
mostRecentPendingImage = outputImageFolder / 'monitoringPreviewMostRecentPending.jpg'
pendingImageFolder = outputImageFolder / 'pending/'
uploadedImageFolder = outputImageFolder / 'uploaded/'

config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))

logFilePath = config["logFilePath"]
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

logFolder = os.path.dirname(logFilePath)

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
# handler = TimedRotatingFileHandler(logFilePath, 
#                                    when='midnight',
#                                    backupCount=10)
handler = SocketHandler('localhost', 8000)
handler.setFormatter(formatter)
logger = logging.getLogger("updateStatus")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info("Starting up updateStatus.py...")
# os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.

pj = pijuice.PiJuice(1, 0x14)




def updateEInkDisplay():

    try:

        logger.info("updateEInkDisplay()")

        if(config['eink.DisplayType'] == "" or config['eink.DisplayType'] == None):
            logger.info("eink.DisplayType not configured. updateEInkDisplay exiting")
            return

        if config['supportMode'] == False:
            # Let's not update e-paper if we're not in Support mode.
            logger.info("Not in support mode - updateEInkDisplay exiting")
            return

        logger.info("updateEInkDisplay - we're going to update the e-ink display")

        #epd = epd1in54b_V2.EPD()
        epd = epaper.epaper(config['eink.DisplayType']).EPD()
        logger.info("init and Clear")
        epd.init()
        epd.Clear()
        
        # # Clear paper every 5 minutes in case it gets corrupted.
        # if(datetime.datetime.now().minute % 5 == 0):
            # epd.Clear()

        #time.sleep(1)
        
        # Drawing on the image
        blackimage = Image.new('1', (epd.width, epd.height), 255)  # 255: clear the frame
        redimage = Image.new('1', (epd.width, epd.height), 255)  # 255: clear the frame

        if config['monitoringMode'] and config['eink.PreviewImage'] == True:  # This is too slow to run every minute, and a bit slow to run every 5 minutes - move to bluetooth app I think?

            # Init and Clear 15s
            # Work out right file to use, copy it, 1s
            # Resize image - 40s
            # open and display buffer - 15s

            logger.info("Previewing image...")
        
            logger.info("Getting most recent uploaded and pending files...")
            mostRecentUploadedFiles = sorted(glob.iglob(uploadedImageFolder + "/*.*"), key=os.path.getctime, reverse=True)
            mostRecentPendingFiles = sorted(glob.iglob(pendingImageFolder + "/*.*"), key=os.path.getctime, reverse=True)

            if len(mostRecentUploadedFiles) > 0:
                shutil.copy(mostRecentUploadedFiles[0], mostRecentUploadedImage)

            if len(mostRecentPendingFiles) > 0:
                shutil.copy(mostRecentPendingFiles[0], mostRecentPendingImage)

            logger.info("Copied most recent uploaded and pending files")

            # Determine the most recent of the two above files
            # Check each file exists
            # If not, use the other file.
            if os.path.exists(mostRecentUploadedImage) and os.path.exists(mostRecentPendingImage):
                if os.stat(mostRecentUploadedImage).st_size > os.stat(mostRecentPendingImage).st_size:
                    mostRecentImage = mostRecentUploadedImage
                    logger.info('Using most recent uploaded image')
                else:
                    mostRecentImage = mostRecentPendingImage
                    logger.info('Using most recent pending image')
            elif os.path.exists(mostRecentUploadedImage):
                mostRecentImage = mostRecentUploadedImage
                logger.info('Using most recent uploaded image')
            elif os.path.exists(mostRecentPendingImage):
                mostRecentImage = mostRecentPendingImage
                logger.info('Using most recent pending image')
            else:
                logger.error('Couldn\'t find any images to display.')
            
            mostRecentImage = mostRecentUploadedImage

            if(os.path.exists(mostRecentImage)):
                # resize image
                logger.info("Resizing image...")
                cmd = 'convert ' + mostRecentPendingFiles[0] + ' -resize 200x200 -background white -gravity center -extent 200x200 ' + imageMonitoringPreview
                subprocess.call(cmd, shell=True)
                logger.info("Resized image")
                blackimage = Image.open(imageMonitoringPreview)
                logger.info("Opened image")

        else:
            fontSize = 14
            font = ImageFont.truetype('Font.ttc', fontSize)
            #font18 = ImageFont.truetype('Font.ttc', 18)

            statusUpdates = [{}]

            statusUpdatesJSONFilename = os.path.join(logFolder, 'statusUpdates.json')

            if(os.path.exists(statusUpdatesJSONFilename) and os.stat(statusUpdatesJSONFilename).st_size > 0):
                statusUpdates = json.load(open(statusUpdatesJSONFilename))

            #logger.info(statusUpdates)
            latestStatusUpdate = statusUpdates[len(statusUpdates) - 1]

            latestStatusUpdate['hibernateMode'] = config['hibernateMode']
            latestStatusUpdate['monitoringMode'] = config['monitoringMode']
            latestStatusUpdate['supportMode'] = config['supportMode']
            latestStatusUpdate['uptime(s)'] = int(time.clock_gettime(time.CLOCK_BOOTTIME))
            latestStatusUpdate['pendingImages'] = str(len(os.listdir(pendingImageFolder)))

            latestStatusUpdate['date'] = time.strftime("%d/%m/%Y", time.localtime())
            latestStatusUpdate['time'] = time.strftime("%H:%M:%S", time.localtime())

            latestStatusUpdate['internet'] = str(internet())

            latestStatusUpdate['sysVoltage'] = str(pj.status.GetBatteryVoltage()['data']) + "mV"
            
            alarm = pj.rtcAlarm.GetAlarm()

            if(alarm['data']['minute']=="EVERY_MINUTE"):
                alarm['data']['minute']="*"

            if(alarm['data']['hour']=="EVERY_HOUR"):
                alarm['data']['hour']="*"

            if(alarm['data']['day']=="EVERY_DAY"):
                alarm['data']['day']="*"

            latestStatusUpdate['alarm'] = "H " + str(alarm['data']['hour']) + " M " + str(alarm['data']['minute']) + " S " + str(alarm['data']['second']) + ' D ' + str(alarm['data']['day'])

            controlStatus = pj.rtcAlarm.GetControlStatus()
            latestStatusUpdate['status'] = "W: " + str(controlStatus['data']['alarm_wakeup_enabled']) + " F: " + str(controlStatus['data']['alarm_flag'])

            latestStatusUpdate['battery'] = str(pj.status.GetChargeLevel()['data']) + "%"  + " " + pj.status.GetStatus()['data']['battery']
            latestStatusUpdate['power'] = pj.status.GetStatus()['data']['powerInput']
            latestStatusUpdate['temp'] = str(pj.status.GetBatteryTemperature()['data']) + "C"

                        
            drawblack = ImageDraw.Draw(blackimage)
            drawred = ImageDraw.Draw(redimage)

            yPosition = 0
            for key in latestStatusUpdate.keys():
                drawblack.text((8, yPosition), key + ': ' + str(latestStatusUpdate[key]), font = font, fill = 0)
                yPosition = yPosition + fontSize


        logger.info("Displaying buffer")
        epd.display(epd.getbuffer(blackimage),epd.getbuffer(redimage))
        
        logger.info("eink display going to Sleep...")
        epd.sleep()
            
    except IOError as e:
        logger.error(e)
    


if config['supportMode'] == False:
    logger.debug("Not in support mode - not updating Status")
    pj.status.SetLedState('D2', [0, 0, 0])

else:

    flashLED(pj, 'D2', 0, 0, 255, 5, 0.1)   # Flash blue - we're on
    if internet():
        logger.info("We've got internet")
        flashLED(pj, 'D2', 0, 255, 0, 1, 2)
    else:
        logger.info("No internet")
        flashLED(pj, 'D2', 255, 0, 0, 1, 2)

    # Only update eink every 5 minutes
    if(datetime.datetime.now().minute % 5 == 0):
        updateEInkDisplay()
