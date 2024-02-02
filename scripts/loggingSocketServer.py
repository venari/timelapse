# adapted from https://davis.lbl.gov/Manuals/PYTHON-2.4.3/lib/network-logging.html

import pickle
import logging
import logging.handlers
import struct
import socket
import json
import threading

# this logic taken from saveTelemetry.py
config = json.load(open('config.json'))
logFilePath = config["logFilePath"]

logger = logging.getLogger('loggingSocketServer')
handler = logging.handlers.TimedRotatingFileHandler(logFilePath,
                                                    when='midnight',
                                                    backupCount=10)

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler.setFormatter(formatter)

logger.addHandler(handler)

logger.setLevel(logging.DEBUG)



def handle(conn: socket.socket):
    # """
    # Handle multiple requests - each expected to be a 4-byte length,
    # followed by the LogRecord in pickle format. Logs the record
    # according to whatever policy is configured locally.
    # """
    # ^ not what we're doing now
    logger.debug('handling connection')
    try:
        while 1:
            chunk = conn.recv(4)
            if len(chunk) < 4:
                logger.debug('conn gave empty chunk, maybe disconnected socket?')
                conn.close()
                return
            
            slen = struct.unpack(">L", chunk)[0]
            chunk = conn.recv(slen)
            while len(chunk) < slen:
                chunk = chunk + conn.recv(slen - len(chunk))
                
            obj = unPickle(chunk)
            record = logging.makeLogRecord(obj)
            handleLogRecord(record)
            
    except Exception as e:
        logger.exception(e)
        # fails softly

def unPickle(data: bytes):
    return pickle.loads(data)

def handleLogRecord(record: logging.LogRecord):
    # # if a name is specified, we use the named logger rather than the one
    # # implied by the record.
    # if self.server.logname is not None:
    #     name = self.server.logname
    # else:
        # name = record.name
    # logger = logging.getLogger(name)
    # N.B. EVERY record gets logged. This is because Logger.handle
    # is normally called AFTER logger-level filtering. If you want
    # to do filtering, do it at the client end to save wasting
    # cycles and network bandwidth!
    
    # below code prints name of originating file in the logs (all that we need it to do)
    logger.handle(record)




def serve_until_stopped():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('localhost', 8000))
    s.listen(10) # 10 is arbitrary, increase it if there will be more than 10 sockets connecting to the logger at once
    logger.debug('listening on port 8000')
    while 1:
        try:
            conn, addr = s.accept()
            threading.Thread(target=handle, args=[conn], daemon=True).start() # daemon so that keyboard interrupt stops all threads
            
        except Exception as e:
            logger.exception(e)
            # fails softly


serve_until_stopped()