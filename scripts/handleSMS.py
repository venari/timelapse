import json
import logging
from logging.handlers import TimedRotatingFileHandler
from SIM7600X import turnOnNDIS, sendSMS, receiveSMS, deleteAllSMS, powerUpSIM7600X


config = json.load(open('config.json'))
logFilePath = config["logFilePath"]

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = TimedRotatingFileHandler(logFilePath, when='midnight', backupCount=10)
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
            sendSMS(phone_number, "Status query received.")

        if line.upper() == "HELLO":
            logger.info("Hello")
            sendSMS(phone_number, "Hello")

        # Body


deleteAllSMS()
