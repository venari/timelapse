import threading
import logging
import os
import json
from logging.handlers import TimedRotatingFileHandler

os.chdir('dev/timelapse/scripts')

import savePhotos
import saveTelemetry
import uploadPending

config = json.load(open('config.json'))
logFilePath = config["logFilePath"]
formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = TimedRotatingFileHandler(logFilePath, 
                                   when='midnight',
                                   backupCount=10)
handler.setFormatter(formatter)
logger = logging.getLogger(__name__)
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

logger.info('starting module threads')

threading.Thread(target=savePhotos.main).start()
threading.Thread(target=saveTelemetry.main).start()
threading.Thread(target=uploadPending.main).start()