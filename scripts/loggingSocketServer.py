# adapted from https://davis.lbl.gov/Manuals/PYTHON-2.4.3/lib/network-logging.html

import pickle
import logging
import logging.handlers
import struct
import socket


formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')

def handle(conn):
    """
    Handle multiple requests - each expected to be a 4-byte length,
    followed by the LogRecord in pickle format. Logs the record
    according to whatever policy is configured locally.
    """
    while 1:
        chunk = conn.recv(4)
        if len(chunk) < 4:
            break
        slen = struct.unpack(">L", chunk)[0]
        chunk = conn.recv(slen)
        while len(chunk) < slen:
            chunk = chunk + conn.recv(slen - len(chunk))
        obj = unPickle(chunk)
        record = logging.makeLogRecord(obj)
        handleLogRecord(record)

def unPickle(data):
    return pickle.loads(data)

def handleLogRecord(record):
    # # if a name is specified, we use the named logger rather than the one
    # # implied by the record.
    # if self.server.logname is not None:
    #     name = self.server.logname
    # else:
        # name = record.name
    name = record.name
    logger = logging.getLogger(name)
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
    while 1:
        conn, addr = s.accept()
        handle(conn)


serve_until_stopped()