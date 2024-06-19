# adapted from https://davis.lbl.gov/Manuals/PYTHON-2.4.3/lib/network-logging.html

import pickle
import logging
import logging.handlers
import struct
import socket
import json
import threading
import os

# this logic taken from saveTelemetry.py
config = json.load(open(os.path.relpath('config.json')))
logFilePath = config["logFilePath"]
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)

logger = logging.getLogger('loggingSocketServer')
handler = logging.handlers.TimedRotatingFileHandler(logFilePath,
                                                    when='midnight',
                                                    backupCount=30)

formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler.setFormatter(formatter)

logger.addHandler(handler)

logger.setLevel(logging.DEBUG)

logger.debug('Starting up loggingSocketServer.py...')
os.chmod(logFilePath, 0o777) # Make sure pijuice user script can write to log file.



def handle(conn: socket.socket):
    # """
    # Handle multiple requests - each expected to be a 4-byte length,
    # followed by the LogRecord in pickle format. Logs the record
    # according to whatever policy is configured locally.
    # """
    # ^ not what we're doing now
    # logger.debug('handling connection') # excessive now that we're disconnecting after each log record is sent
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
            # logger.debug('end connection (no error)') # excessive now that we're disconnectting after each log record is sent
            conn.settimeout(1.00) # if connection goes more than 1 second without log record, throw a timeout error and wait for the next connection

    except socket.timeout:
        pass
        # socket.timeout is intentionally caused by the connection timeout
        # if the socket times out, we want to move on to the next connection
        # and because it happens a lot we don't want to clutter the logs
            
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
    logger.handle(record)




def serve_until_stopped():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(('localhost', 8000))
    s.listen(10) # 10 is arbitrary
    logger.debug('listening on port 8000')
    while 1:
        try:
            conn, addr = s.accept()
            conn.settimeout
            handle(conn)
            
        except Exception as e:
            logger.exception(e)
            # fails softly


serve_until_stopped()