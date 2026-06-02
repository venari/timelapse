import json
import logging
import sys
import socket
from SIM7600X import turnOnNDIS, sendSMS, receiveSMS, deleteAllSMS, powerUpSIM7600X
import time
# from pvpi import PvPiClient
# from pvpi.client import PvPiChargeState
import os
import pathlib

from helpers import internet

config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = logging.StreamHandler(sys.stderr)
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
    if isinstance(line, bytes):
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

            # pvpiClient = None
            # try:
            #     pvpiClient = PvPiClient()
            # except Exception:
            #     pass

            bCharging = False
            batteryPercent = 0
            temperatureC = 0
            # if pvpiClient is not None:
            #     try:
            #         charging_states = (
            #             PvPiChargeState.TrickleCharge,
            #             PvPiChargeState.PreCharge,
            #             PvPiChargeState.FastCharge,
            #             PvPiChargeState.TaperCharge,
            #             PvPiChargeState.TopOffTimerCharge,
            #         )
            #         bCharging = pvpiClient.get_charge_state_code() in charging_states
            #         batteryPercent = pvpiClient.estimated_soc()
            #         temperatureC = pvpiClient.get_board_temp()
            #     except Exception:
            #         pass

            outputImageFolder = str(pathlib.Path(__file__).parent / '../output/images/')
            workingImageFolder = os.path.join(outputImageFolder , 'working/')
            pendingImageFolder = os.path.join(outputImageFolder , 'pending/')
            uploadedImageFolder = os.path.join(outputImageFolder , 'uploaded/')

            outputTelemetryFolder = str(pathlib.Path(__file__).parent / '../output/telemetry/')
            pendingTelemetryFolder = os.path.join(outputTelemetryFolder , 'pending/')
            uploadedTelemetryFolder = os.path.join(outputTelemetryFolder , 'uploaded/')

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
