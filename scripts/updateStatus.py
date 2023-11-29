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


try:
    

    if config['supportMode'] == False:
        # Let's not update e-paper if we're not in Support mode.
        exit()

    #epd = epd1in54b_V2.EPD()
    epd = epaper.epaper('epd1in54b').EPD()
    logger.info("init and Clear")
    epd.init()
    epd.Clear()
    
    # # Clear paper every 5 minutes in case it gets corrupted.
    # if(datetime.datetime.now().minute % 5 == 0):
        # epd.Clear()

    #time.sleep(1)
    
    # Drawing on the image
    logger.info("1.Drawing on the image...")
    blackimage = Image.new('1', (epd.width, epd.height), 255)  # 255: clear the frame
    redimage = Image.new('1', (epd.width, epd.height), 255)  # 255: clear the frame

    if config['monitoringMode']:
    
        mostRecentUploadedFiles = sorted(glob.iglob(uploadedImageFolder + "/*.*"), key=os.path.getctime, reverse=True)
        mostRecentPendingFiles = sorted(glob.iglob(pendingImageFolder + "/*.*"), key=os.path.getctime, reverse=True)

        if len(mostRecentUploadedFiles) > 0:
            shutil.copy(mostRecentUploadedFiles[0], mostRecentUploadedImage)

        if len(mostRecentPendingFiles) > 0:
            shutil.copy(mostRecentPendingFiles[0], mostRecentPendingImage)
        
        # Determine the most recent of the two above files
        # Check each file exists
        # If not, use the other file.
        if os.path.exists(mostRecentUploadedImage) and os.path.exists(mostRecentPendingImage):
            if os.stat(mostRecentUploadedImage).st_size > os.stat(mostRecentPendingImage).st_size:
                mostRecentImage = mostRecentUploadedImage
            else:
                mostRecentImage = mostRecentPendingImage
        elif os.path.exists(mostRecentUploadedImage):
            mostRecentImage = mostRecentUploadedImage
        elif os.path.exists(mostRecentPendingImage):
            mostRecentImage = mostRecentPendingImage
        else:
            logger.error('Couldn\'t find any images to display.')
        
        mostRecentImage = mostRecentUploadedImage

        if(os.path.exists(mostRecentImage)):
            # resize image
            cmd = 'convert ' + mostRecentPendingFiles[0] + ' -resize 200x200 -background white -gravity center -extent 200x200 ' + imageMonitoringPreview
            subprocess.call(cmd, shell=True)
            
            blackimage = Image.open(imageMonitoringPreview)

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

        latestStatusUpdate['internet'] = str(internet())

        pj = pijuice.PiJuice(1, 0x14)
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



    epd.display(epd.getbuffer(blackimage),epd.getbuffer(redimage))
    
    logger.info("Goto Sleep...")
    epd.sleep()
        
except IOError as e:
    logger.error(e)
    
# except KeyboardInterrupt:    
#     logging.info("ctrl + c:")
#     epd1in54b_V2.epdconfig.module_exit()
#     exit()


