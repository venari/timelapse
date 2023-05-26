import pijuice
import time
import socket

pj = pijuice.PiJuice(1, 0x14)

def flashLED(led='D2', R=0, G=0, B=255, flashCount=3, flashDelay=0.5):
    for i in range(0, flashCount):
        pj.status.SetLedState(led, [R, G, B])
        time.sleep(flashDelay)
        pj.status.SetLedState(led, [0, 0, 0])
        time.sleep(flashDelay)

def cycleLEDs():
    flashLED('D2', 255, 0, 0, 1, 0.2)
    flashLED('D2', 0, 255, 0, 1, 0.2)
    flashLED('D2', 0, 0, 255, 1, 0.2)

def indicateStatus():

    # Green - internet OK, red no internet
    time.sleep(3)
    if internet():
        flashLED('D2', 0, 255, 0, 1, 2)
    else:
        flashLED('D2', 255, 0, 0, 1, 2)
    

def turnOnSystemPowerSwitch(retries = 3):

    pj.power.SetSystemPowerSwitch(500)

    waitCounter = 0
    while not internet() and waitCounter < 12:
        time.sleep(10)
        waitCounter=waitCounter+1

        # Flash yellow
        flashLED('D2', 255, 255, 0, 1, 1)
    
    if waitCounter < 12:
        # Solid blue
        flashLED('D2', 0, 0, 255, 1, 5)
    
    else:
        # Flash red
        flashLED('D2', 255, 0, 0, 10, 0.2)
        if retries > 0:
            turnOnSystemPowerSwitch(retries-1)
        else:
            pj.power.SetSystemPowerSwitch(0)
            flashLED('D2', 255, 0, 0, 3, 1)
            return
    

# https://stackoverflow.com/questions/3764291/how-can-i-see-if-theres-an-available-and-active-network-connection-in-python
def internet(host="8.8.8.8", port=53, timeout=3):
    """
    Host: 8.8.8.8 (google-public-dns-a.google.com)
    OpenPort: 53/tcp
    Service: domain (DNS/TCP)
    """
    try:
        socket.setdefaulttimeout(timeout)
        socket.socket(socket.AF_INET, socket.SOCK_STREAM).connect((host, port))
        return True
    except socket.error as ex:
        return False

def togglePowerSwitch():

    if pj.power.GetSystemPowerSwitch() == 0:
        turnOnSystemPowerSwitch()
    else:
        pj.power.SetSystemPowerSwitch(0)
        flashLED('D2', 255, 0, 0, 1, 10)


cycleLEDs()
# flashLED('D2', 0, 0, 255, 3, 0.1)

indicateStatus()
togglePowerSwitch()

# Flash to indicate end of status
cycleLEDs()
# flashLED('D2', 0, 0, 255, 3, 0.1)
