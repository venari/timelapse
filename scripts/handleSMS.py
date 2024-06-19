import json
import logging
# from logging.handlers import TimedRotatingFileHandler
from logging.handlers import SocketHandler
import socket
from SIM7600X import turnOnNDIS, sendSMS, receiveSMS, deleteAllSMS, powerUpSIM7600X
import time
import pijuice
import os

from helpers import internet

config = json.load(open(os.path.relpath('config.json')))
logFilePath = config["logFilePath"]

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
# handler = TimedRotatingFileHandler(logFilePath, when='midnight', backupCount=10)
handler = SocketHandler('localhost', 8000)
handler.setFormatter(formatter)
logger = logging.getLogger("handleSMS")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)


#try:
#powerUpSIM7600X()
#sendSMS('+64xxxxxxxxx','Testing tesing')

logger.debug('About to call receiveSMS()...')

rec_buff = receiveSMS()
logger.debug('Returned from receiveSMS()')
logger.debug(rec_buff)
# print('Printed rec_buff')

rec_lines = rec_buff.splitlines()
# print('Split rec_buff into rec_lines')
logger.debug(rec_lines)

phone_number=''

for line in rec_lines:
    line = line.decode()
    logger.info(line)
    if(line.startswith("+CMGL:")):
        # Header
        comma_buff = line.split(',')
        message_index = comma_buff[0]
        message_status = comma_buff[1].removeprefix('"').removesuffix('"')
        phone_number = comma_buff[2].removeprefix('"').removesuffix('"')
        address_text = comma_buff[3]
        timestamp_date = comma_buff[4].removeprefix('"').removesuffix('"')
        timestamp_time = comma_buff[5].removeprefix('"').removesuffix('"')
        # print(message_index)
        # print(message_status)
        # print(phone_number)
        # print(timestamp_date)
        # print(timestamp_time)
    else:
        # logger.debug(line)

        if line.upper() == "STATUS?":
            logger.info("Status query")

            statusMessage = ""
            uptimeSeconds = int(time.clock_gettime(time.CLOCK_BOOTTIME))

            pj = pijuice.PiJuice(1, 0x14)

            bCharging = False
            if (
                (pj.status.GetStatus()['data']['battery'] == 'CHARGING_FROM_IN' 
                or pj.status.GetStatus()['data']['battery'] == 'CHARGING_FROM_5V_IO' )
                and  pj.status.GetStatus()['data']['powerInput'] == 'PRESENT'
            ):
                bCharging = True


            outputImageFolder = '../output/images/'
            workingImageFolder = outputImageFolder + 'working/'
            pendingImageFolder = outputImageFolder + 'pending/'
            uploadedImageFolder = outputImageFolder + 'uploaded/'

            outputTelemetryFolder = '../output/telemetry/'
            pendingTelemetryFolder = outputTelemetryFolder + 'pending/'
            uploadedTelemetryFolder = outputTelemetryFolder + 'uploaded/'

            batteryPercent = pj.status.GetChargeLevel()['data']
            temperatureC = pj.status.GetBatteryTemperature()['data']
            pendingImages = len(os.listdir(pendingImageFolder))
            pendingTelemetry = len(os.listdir(pendingTelemetryFolder))

            statusMessage += "Uptime: " + str(uptimeSeconds) + " s\n"
            statusMessage += "Internet: " + str(internet()) + " \n"
            statusMessage += "Charging: " + str(bCharging) + "\n"
            statusMessage += "Battery %: " + str(batteryPercent) + "\n"
            statusMessage += "Temp : " + str(temperatureC) + "C\n"
            statusMessage += "Pending Images: " + str(pendingImages) + "\n"
            statusMessage += "Pending Telemetry: " + str(pendingTelemetry) + "\n"

            sendSMS(phone_number, statusMessage)


        if line.upper() == "HELLO":
            logger.info("Hello")
            sendSMS(phone_number, "Hello")

        # Body


deleteAllSMS()
