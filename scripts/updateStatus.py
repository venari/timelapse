#!/usr/bin/python
# -*- coding:utf-8 -*-
from logging.handlers import TimedRotatingFileHandler
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

outputImageFolder = '../output/images/'
imageMonitoringPreview = outputImageFolder + 'monitoringPreview.jpg'
mostRecentUploadedImage = outputImageFolder + 'monitoringPreviewMostRecentUploaded.jpg'
mostRecentPendingImage = outputImageFolder + 'monitoringPreviewMostRecentPending.jpg'
pendingImageFolder = outputImageFolder + 'pending/'
uploadedImageFolder = outputImageFolder + 'uploaded/'

config = json.load(open('config.json'))

logFilePath = config["logFilePath"]

logFolder = os.path.dirname(logFilePath)

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = TimedRotatingFileHandler(logFilePath, 
                                   when='midnight',
                                   backupCount=10)
handler.setFormatter(formatter)
logger = logging.getLogger("updateStatus")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info("Starting up updateStatus.py...")
os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.

pj = pijuice.PiJuice(1, 0x14)

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
        return False




def flashLED(led='D2', R=0, G=0, B=255, flashCount=3, flashDelay=0.5):
    for i in range(0, flashCount):
        pj.status.SetLedState(led, [R, G, B])
        time.sleep(flashDelay)
        pj.status.SetLedState(led, [0, 0, 0])
        time.sleep(flashDelay)



def updateEInkDisplay():

    try:

        logger.info("updateEInkDisplay()")

        if(config['eink.DisplayType'] == "" or config['eink.DisplayType'] == None):
            logger.info("eink.DisplayType not configured. updateEInkDisplay exiting")
            exit()

        if config['supportMode'] == False:
            # Let's not update e-paper if we're not in Support mode.
            logger.info("Not in support mode - updateEInkDisplay exiting")
            exit()

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
            latestStatusUpdate['date'] = time.strftime("%d/%m/%Y", time.localtime())
            latestStatusUpdate['time'] = time.strftime("%H:%M:%S", time.localtime())

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
        
        logger.info("Goto Sleep...")
        epd.sleep()
            
    except IOError as e:
        logger.error(e)
    




flashLED('D2', 0, 0, 255, 5, 0.5)   # Flash blue - we're on
if internet():
    logger.info("We've got internet")
    flashLED('D2', 0, 255, 0, 1, 2)
else:
    logger.info("No internet")
    flashLED('D2', 255, 0, 0, 1, 2)

# Only update eink every 5 minutes 
if(datetime.datetime.now().minute % 5 == 0):
    updateEInkDisplay()