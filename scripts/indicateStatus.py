import pijuice
import time
import socket
import logging
# from logging.handlers import TimedRotatingFileHandler
from logging.handlers import SocketHandler
import pathlib
import json
from helpers import internet, flashLED

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X

config = json.load(open(os.path.relpath('config.json')))
logFilePath = config["logFilePath"]

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
# handler = TimedRotatingFileHandler(logFilePath, when='midnight', backupCount=10)
handler = SocketHandler('localhost', 8000)
handler.setFormatter(formatter)
logger = logging.getLogger("indicateStatus")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info("Starting up indicateStatus.py...")

pj = pijuice.PiJuice(1, 0x14)


def cycleLEDs():
    flashLED(pj, 'D2', 255, 0, 0, 1, 0.2)
    flashLED(pj, 'D2', 0, 255, 0, 1, 0.2)
    flashLED(pj, 'D2', 0, 0, 255, 1, 0.2)

def indicateStatus():

    # Green - internet OK, red no internet
    time.sleep(3)
    if internet():
        logger.info("We've got internet")
        flashLED(pj, 'D2', 0, 255, 0, 1, 2)
    else:
        logger.info("No internet")
        flashLED(pj, 'D2', 255, 0, 0, 1, 2)
    

def turnOnSystemPowerSwitch(retries = 3):

    modemPower = config['modem.power']
    if modemPower <= 0:
        logger.info('Modem power is disabled in config.')
        return
    
    pj.power.SetSystemPowerSwitch(modemPower)
    powerUpSIM7600X()

    waitCounter = 0
    while not internet() and waitCounter < 12:
        time.sleep(10)
        waitCounter=waitCounter+1

        # Flash yellow
        flashLED(pj, 'D2', 150, 200, 0, 1, 1)
    
    if waitCounter < 12:
        # Solid blue
        flashLED(pj, 'D2', 0, 0, 255, 1, 5)
    
    else:
        # Flash red
        flashLED(pj, 'D2', 255, 0, 0, 10, 0.2)
        if retries > 0:
            turnOnSystemPowerSwitch(retries-1)
        else:
            pj.power.SetSystemPowerSwitch(0)
            powerDownSIM7600X()
            flashLED(pj, 'D2', 255, 0, 0, 3, 1)
            return
    

def togglePowerSwitch():

    if pj.power.GetSystemPowerSwitch()['data'] == 0:
        logger.info("Toggling power switch ON")
        powerUpSIM7600X()
        turnOnSystemPowerSwitch()
    else:
        logger.info("Toggling power switch OFF")
        pj.power.SetSystemPowerSwitch(0)
        powerDownSIM7600X()
        flashLED(pj, 'D2', 255, 0, 0, 1, 5)


cycleLEDs()
# flashLED(pj, 'D2', 0, 0, 255, 3, 0.1)

indicateStatus()
togglePowerSwitch()

# Flash to indicate end of status
cycleLEDs()
# flashLED(pj, 'D2', 0, 0, 255, 3, 0.1)

logger.info("Completed indicateStatus.py")

