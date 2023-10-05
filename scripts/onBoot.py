import threading
import logging
import os

import savePhotos
import saveTelemetry
import uploadPending

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = TimedRotatingFileHandler(logFilePath, 
                                   when='midnight',
                                   backupCount=10)
handler.setFormatter(formatter)
logger = logging.getLogger(__name__)
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

os.chdir('dev/timelapse/scripts')

logger.info('starting module threads')

threading.Thread(target=savePhotos.main).start()
threading.Thread(target=saveTelemetry.main).start()
threading.Thread(target=uploadPending.main).start()