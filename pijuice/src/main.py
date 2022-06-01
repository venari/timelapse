import shutil
from time import sleep
import datetime
import sys, getopt, os
import time
sys.path.append('/usr/lib/python3.5/dist-packages') # temporary hack to import the piJuice module
from pijuice import PiJuice
from balena import Balena
from twilio.rest import Client
import requests

# Start the SDK
balena = Balena()
balena.auth.login_with_token(os.environ['BALENA_API_KEY'])

def getSerialNumber():
    if (os.environ.get('BALENA') != None):
        return os.environ['BALENA_DEVICE_UUID']

    # Extract serial from cpuinfo file
    cpuserial = "0000000000000000"
    try:
        f = open('/proc/cpuinfo','r')
        for line in f:
            if line[0:6]=='Serial':
                    cpuserial = line[10:26]
            f.close()
    except:
        cpuserial = "ERROR000000000"

    return cpuserial

serialNumber = getSerialNumber()
print("serialNumber: " + serialNumber)

print('API_URL')
apiUrl = os.environ['API_URL']
print(apiUrl)


print('CAMERA_RESOLUTION_WIDTH')
print(os.environ['CAMERA_RESOLUTION_WIDTH'])
print('CAMERA_RESOLUTION_HEIGHT')
print(os.environ['CAMERA_RESOLUTION_HEIGHT'])


# Wait for device I2C device to start
while not os.path.exists('/dev/i2c-1'):
    print ("Waiting to identify PiJuice")
    time.sleep(0.1)

# Initiate PiJuice
pijuice = PiJuice(1,0x14)

# Get all parameters and return as a dictionary
def get_battery_paremeters(pijuice):

    juice = {}

    batteryPercent = pijuice.status.GetChargeLevel()
    juice['batteryPercent'] = batteryPercent['data'] if batteryPercent['error'] == 'NO_ERROR' else -1 #batteryPercent['error']

    # Temperature [C]
    temperatureC =  pijuice.status.GetBatteryTemperature()
    juice['temperatureC'] = temperatureC['data'] if temperatureC['error'] == 'NO_ERROR' else -1 #temperatureC['error']

    # Battery voltage  [V]
    vbat = pijuice.status.GetBatteryVoltage()
    juice['vbat'] = vbat['data']/1000 if vbat['error'] == 'NO_ERROR' else vbat['error']

    # Barrery current [A]
    ibat = pijuice.status.GetBatteryCurrent()
    juice['ibat'] = ibat['data']/1000 if ibat['error'] == 'NO_ERROR' else ibat['error']

    # I/O coltage [V]
    vio =  pijuice.status.GetIoVoltage()
    juice['vio'] = vio['data']/1000 if vio['error'] == 'NO_ERROR' else vio['error']

    # I/O current [A]
    iio = pijuice.status.GetIoCurrent()
    juice['iio'] = iio['data']/1000 if iio['error'] == 'NO_ERROR' else iio['error']

    # Get power input (if power connected to the PiJuice board)
    status = pijuice.status.GetStatus()
    juice['power_input'] = status['data']['powerInput'] if status['error'] == 'NO_ERROR' else status['error']

    # Get power input (if power connected to the Raspberry Pi board)
    status = pijuice.status.GetStatus()
    juice['power_input_board'] = status['data']['powerInput5vIo'] if status['error'] == 'NO_ERROR' else status['error']

    return juice

def update_tag(tag, variable):
    # update device tags
    balena.models.tag.device.set(os.environ['BALENA_DEVICE_UUID'], str(tag), str(variable))

def send_sms(to_number, from_number, message, client):
    msg = client.messages.create(to=twilio_number, from_=twilio_from_number,body=message)
    print(msg.sid)


def uploadTelemetry():
    try:
        # warningTemp = 50
        api_data = {
                    'batteryPercent': pijuice.status.GetChargeLevel()['data'],
                    'temperatureC': pijuice.status.GetBatteryTemperature()['data'],
                    'diskSpaceFree': shutil.disk_usage('/')[2] // (1024**3), # shutil.disk_usage returns tuple of (total, used, free), converted to int gb
                    'uptimeSeconds': int(time.clock_gettime(time.CLOCK_BOOTTIME)),
                    'status': str({ 'status': pijuice.status.GetStatus()['data'],
                                'batteryVoltage': pijuice.status.GetBatteryVoltage()['data'],
                                'batteryCurrent': pijuice.status.GetBatteryCurrent()['data'],
                                'ioVoltage': pijuice.status.GetIoVoltage()['data'],
                                'ioCurrent': pijuice.status.GetIoCurrent()['data']
                            }),
                    'SerialNumber': serialNumber
                }

        # if api_data['temperatureC'] > warningTemp:
        #     print(f'WARNING: temperature is {api_data["temperatureC"]}C')
        #     with open('tempWarning.log', 'a') as f:
        #         f.write(f'{datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")}: {api_data["temperatureC"]}C\n')

        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)
        session = requests.Session()

        print(api_data)

        postResponse = session.post(apiUrl + 'Telemetry',data=api_data)
        print(postResponse)
        #assert postResponse.status_code == 200, "API returned error code"
        #requests.post(config['apiUrl'] + '/Telemetry', json=api_data)

        print(str(datetime.datetime.now()) + ' Logged to API.')

    except Exception as e:
        print(str(datetime.datetime.now()) + " uploadTelemetry() failed.")
        print(e)



# Change start tag
start_time = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
update_tag("START_TIME", start_time)

# ======================[ TWILIO CODE ]================================
# The idea here is to send user one message every hour in case a device
if( os.environ.get('TWILIO_SID') != None and os.environ.get('TWILIO_TOKEN') != None and os.environ.get('TWILIO_NUMBER') != None and os.environ.get('TWILIO_FROM_NUMBER') != None):
    twilio_sid = os.environ['TWILIO_SID']
    twilio_token = os.environ['TWILIO_TOKEN']
    twilio_number = os.environ['TWILIO_NUMBER']
    twilio_from_number = os.environ['TWILIO_FROM_NUMBER']

    # Initiate twilio client
    client = Client(twilio_sid, twilio_token)
    twillio_active = True
    twillio_last_message = datetime.datetime.now()
# =====================================================================

# Initial variables
i = 0

while True:

    #Read battery data
    battery_data = get_battery_paremeters(pijuice)
    # Uncomment the line to display battery status on long
    # print(battery_data)

    # Case power is disconnedted, send twilio text message if twilio alarm is set to true
    if (os.environ.get('TWILIO_ALARM') != None):
        if (os.environ['TWILIO_ALARM'].lower() == "true" and battery_data['power_input'] == "NOT_PRESENT" and battery_data['power_input_board'] == "NOT_PRESENT"):
            # check if last message was over one hour from the last message
            time_difference = (datetime.datetime.now() - twillio_last_message ).total_seconds() / 3600
            if(time_difference >= 1):
                send_sms(twilio_number, twilio_from_number,"Your device just lost power...", client)
                twillio_last_message = datetime.datetime.now()

    # Change tags every minute
    if(i%12==0):

        update_time = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        update_tag("UPDATE_TIME", update_time)
        update_tag("BALENA_DEVICE_NAME_AT_INIT", os.environ['BALENA_DEVICE_NAME_AT_INIT'])
        update_tag("DISK_FREE", shutil.disk_usage('/')[2] // (1024**3)) # shutil.disk_usage returns tuple of (total, used, free), converted to int gb)
        update_tag("UPTIME_SECONDS", int(time.clock_gettime(time.CLOCK_BOOTTIME)))

        # Update tags
        for key, value in battery_data.items():
            update_tag(key, value)

        uploadTelemetry()

    i = i + 1
    sleep(5)
