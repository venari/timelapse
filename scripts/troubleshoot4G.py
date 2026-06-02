import subprocess
import json
import os
import time
import shutil
import datetime
import sys
import requests
import logging
import glob
import pathlib
import socket

from helpers import internet

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X, turnOnNDIS, forceTo4GConnection

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = logging.StreamHandler(sys.stderr)
handler.setFormatter(formatter)
logger = logging.getLogger("troubleshoot4G")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info("******************************************************************************")
logger.info("")
logger.info("Starting up troubleshoot4G.py...")
logger.info("")
logger.info("******************************************************************************")

def troubleshoot4GConnection():
    try:
        logger.info('Troubleshooting 4G connection...')

        if(internet()):
            logger.info('Already connected to internet.')

        powerUpSIM7600X()

        logger.info('Waiting for network....')
        # Call Internet function to wait for network, for a max of 2 minutes
        waitCounter = 0
        while not internet() and waitCounter < 12:
            time.sleep(10)
            logger.info('Still waiting for network....')
            waitCounter=waitCounter+1
        
        if internet():
            logger.info('Network connection established.')

        forceTo4GConnection()


    except Exception as e:
        logger.error(str(datetime.datetime.now()) + " troubleshoot4GConnection() failed.")
        logger.error(e)

try:
    
    troubleshoot4GConnection()

except Exception as e:
    logger.error(e)
