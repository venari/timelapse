import json
import os
import time
import logging
# from logging.handlers import TimedRotatingFileHandler
from logging.handlers import SocketHandler
import serial
import pathlib

import RPi.GPIO as GPIO
from time import sleep
# import serial

GPIO_Power_Key = 6
rec_buff = ''

ser = None

config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
logFilePath = config["logFilePath"]
# logFilePath = logFilePath.replace(".log", ".SIM7600X.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)
# os.chmod(os.path.dirname(logFilePath), 0o777) # Make sure pijuice user scrip can write to log file.


formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
# handler = TimedRotatingFileHandler(logFilePath, 
#                                    when='midnight',
#                                    backupCount=10)
handler = SocketHandler('localhost', 8000)
handler.setFormatter(formatter)
logger = logging.getLogger("SIM7600X")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

# logger.info("Starting up SIM7600X.py...")
# os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.


# PowerUp/Down code copied from https://forum.core-electronics.com.au/t/guide-by-tim-4g-and-gps-hat-for-raspberry-pi-waveshare-sim7600x/14357/88

def powerUpSIM7600X():
    try:

        if(config['modem.type']=="thumb"):
            return

        logger.debug('Powering up SIM7600X...')
        GPIO.setmode(GPIO.BCM)

        GPIO.setwarnings(False)
        GPIO.setup(GPIO_Power_Key, GPIO.OUT)
        sleep(0.1)
        GPIO.output(GPIO_Power_Key, GPIO.HIGH)
        sleep(2)
        GPIO.output(GPIO_Power_Key, GPIO.LOW)


        logger.debug('Waiting 20s...')

        sleep(20)
        logger.debug('SIM7600X should be powered up...')
    
    except Exception as e:
        logger.error("powerUpSIM7600X() failed.")
        logger.error(e)



def powerDownSIM7600X():
    try:

        if(config['modem.type']=="thumb"):
            return

        logger.debug('Powering down SIM7600X...')
        GPIO.setmode(GPIO.BCM)

        GPIO.setwarnings(False)
        GPIO.setup(GPIO_Power_Key, GPIO.OUT)
        sleep(0.1)
        GPIO.output(GPIO_Power_Key, GPIO.HIGH)
        sleep(3)
        GPIO.output(GPIO_Power_Key, GPIO.LOW)
        sleep(18)
        logger.debug('SIM7600X powered down...')
    
    except Exception as e:
        logger.error("powerDownSIM7600X() failed.")
        logger.error(e)

def turnOnNDIS():
    try:
        if(config['modem.type']=="thumb"):
            return

        logger.debug('Turrning on NDIS...')

        global ser
        ser = serial.Serial(config["SIM7600X_port"],115200)
        ser.flushInput()

        logger.debug('Sending AT+CUSBPIDSWITCH=9011,1,1...')

        send_at('AT+CUSBPIDSWITCH=9011,1,1','OK',1)

    except Exception as e:
        logger.error("turnOnNDIS() failed.")
        logger.error(e)

def sendSMS(phone_number,text_message):
    if(config['modem.type']=="thumb"):
        return

    global ser
    ser = serial.Serial(config["SIM7600X_port"],115200)
    ser.flushInput()

    print("Setting SMS mode...")
    send_at("AT+CMGF=1","OK",1)
    print("Sending Short Message")
    print(phone_number)
    print(text_message)
    answer = send_at("AT+CMGS=\""+phone_number+"\"",">",2)
    if 1 == answer:
        ser.write(text_message.encode())
        ser.write(b'\x1A')
        answer = send_at('','OK',20)
        if 1 == answer:
            print('send successfully')
        else:
            print('error')
    else:
        print('error%d'%answer)

def receiveSMS():
    if(config['modem.type']=="thumb"):
        return

    global ser, rec_buff
    ser = serial.Serial(config["SIM7600X_port"],115200)
    ser.flushInput()

    rec_buff = ''
    print('Setting SMS mode...')
    send_at('AT+CMGF=1','OK',1)
    send_at('AT+CPMS=\"SM\",\"SM\",\"SM\"', 'OK', 1)
    # answer = send_at('AT+CMGR=1','+CMGR:',2)
    # answer = send_at('AT+CMGL="REC UNREAD"','+CMGL:',2)
    answer = send_at('AT+CMGL="ALL"','+CMGL:',2)
    if 1 != answer:
        print('error%d'%answer)
        return 'error%d'%answer
    return rec_buff

def deleteAllSMS():
    if(config['modem.type']=="thumb"):
        return

    global ser, rec_buff
    ser = serial.Serial(config["SIM7600X_port"],115200)
    ser.flushInput()

    rec_buff = ''
    print('Setting SMS mode...')
    send_at('AT+CMGF=1','OK',1)
    # send_at('AT+CPMS=\"SM\",\"SM\",\"SM\"', 'OK', 1)
    answer = send_at('AT+CMGD=0,1','OK',2)
    if 1 != answer:
    #     answer = 0
    #     if 'OK' in rec_buff.decode():
    #         answer = 1
    # else:
        return False
    return True


def send_at(command,back,timeout):
    if(config['modem.type']=="thumb"):
        return

    global ser, rec_buff
    rec_buff = ''
    ser.write((command+'\r\n').encode())
    time.sleep(timeout)
    if ser.inWaiting():
        time.sleep(0.01 )
        rec_buff = ser.read(ser.inWaiting())
    if back not in rec_buff.decode():
        print(command + ' ERROR')
        print(command + ' back:\t' + rec_buff.decode())
        return 0
    else:
        print(rec_buff.decode())
        return 1




# def send_at(command,back,timeout):
# 	rec_buff = ''
# 	ser.write((command+'\r\n').encode())
# 	time.sleep(timeout)
# 	if ser.inWaiting():
# 		time.sleep(0.01 )
# 		rec_buff = ser.read(ser.inWaiting())
# 	if back not in rec_buff.decode():
# 		logger.info(command + ' ERROR')
# 		logger.info(command + ' back:\t' + rec_buff.decode())
# 		return 0
# 	else:
# 		logger.info(rec_buff.decode())
# 		return 1

    

# try:
#     logger.info('In powerOnSIM7600.py')

#     powerUpSIM7600X()

#     logger.info("SIM7600X is powered up.")
#     logger.info("Waiting 2 mins...")
#     time.sleep(120)

#     config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
#     if config['supportMode'] == False:
#         logger.info("Powering off....")
#         powerDownSIM7600X()
#     else:
#         logger.info("In support mode - not powering off....")

# except Exception as e:
#     # if ser != None:
#     #     ser.close()
#     GPIO.cleanup()
#     logger.error("Catastrophic failure.")
#     logger.error(e)
