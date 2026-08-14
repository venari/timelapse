import subprocess
import json
from pvpi import PvPiClient
from pvpi.client import PvPiChargeState
import os
import time
import shutil
import datetime
import sys
import logging
import pathlib
import glob
import requests

from helpers import internet

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X
from isConnectedToWifi import is_connected_to_wifi_linux, wifiSSID
from uploadPending import uploadTelemetry, check_usb_power_status

config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
logFilePath = config["logFilePath"]
intentLogFilePath = logFilePath.replace("timelapse.log", "intent.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)


formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
handler = logging.StreamHandler(sys.stderr)
handler.setFormatter(formatter)
logger = logging.getLogger("saveTelemetry")
logger.addHandler(handler)
logger.setLevel(logging.DEBUG)

handlerIntent = logging.FileHandler(intentLogFilePath)
handlerIntent.setFormatter(formatter)
loggerIntent = logging.getLogger("intent")
loggerIntent.addHandler(handlerIntent)
loggerIntent.setLevel(logging.DEBUG)

logger.info("Starting up saveTelemetry.py...")
loggerIntent.info("Starting up saveTelemetry.py...")

## clock
#while not os.path.exists('/dev/i2c-1'):
#    logger.info("dev i2c-1 doesn't exist")
#    time.sleep(0.1)

outputImageFolder = str(pathlib.Path(__file__).parent / '../output/images/')
workingImageFolder = os.path.join(outputImageFolder , 'working/')
pendingImageFolder = os.path.join(outputImageFolder , 'pending/')
uploadedImageFolder = os.path.join(outputImageFolder , 'uploaded/')

outputTelemetryFolder = str(pathlib.Path(__file__).parent / '../output/telemetry/')
pendingTelemetryFolder = os.path.join(outputTelemetryFolder , 'pending/')
uploadedTelemetryFolder = os.path.join(outputTelemetryFolder , 'uploaded/')

# pvpi
time.sleep(10)
pvpiClient = None
try:
    pvpiClient = PvPiClient()
except:
    logger.error("PvPi not connected - PvPi functionality will not be available")

def pj_is_alive():
    try:
        return pvpiClient is not None and pvpiClient.get_alive()
    except Exception:
        return False

if not pj_is_alive():
    logger.error('PvPi not connected')
    loggerIntent.error('PvPi not connected')
else:
    logger.info('PvPi is connected')
    loggerIntent.info('PvPi is connected')


logger.info("Starting up saveTelemetry.py 3b...")

def getSerialNumber():
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

_CHARGING_STATES = (
    PvPiChargeState.TrickleCharge,
    PvPiChargeState.PreCharge,
    PvPiChargeState.FastCharge,
    PvPiChargeState.TaperCharge,
    PvPiChargeState.TopOffTimerCharge,
)

# Track consecutive low voltage readings
low_voltage_count = 0
LOW_VOLTAGE_THRESHOLD = 5

def scheduleShutdown():
    global low_voltage_count
    
    try:
        if not pj_is_alive():
            logger.error('PvPi not connected')
            loggerIntent.error('PvPi not connected')
            return

        alarm_time = None

        # print(str(datetime.datetime.now()) + ' scheduleShutdown')
        logger.debug('scheduleShutdown')
        logger.debug('get_mcu_time(): ' + str(pvpiClient.get_mcu_time()))

        setAlarm = False        # Alarm for waking up later than one hour from now
        triggerRestart = False  # watchdog appears more reliable but can only be used for max 60 minutes

        config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))

        uptimeSeconds = int(time.clock_gettime(time.CLOCK_BOOTTIME))

        bCharging = pvpiClient.get_charge_state_code() in _CHARGING_STATES

        if config['sleep_during_night'] == True and (datetime.datetime.now().hour >= config['daytime_ends_at_h'] or datetime.datetime.now().hour < config['daytime_starts_at_h']):
            if config['supportMode'] == True:
                logger.warning("Night time - we would have scheduled shutdown, but we're in support mode.")

            if bCharging:
                logger.info("Night time - but we're charging/powered, so we'll stay on.")

        # Has MCU RTC been reset? (year 2000 = factory default)
        if pvpiClient.get_mcu_time().year <= 2025:
                logger.warning('Looks like MCU RTC has been reset - going into support mode until we reconnect.')
                loggerIntent.warning('Looks like MCU RTC has been reset - going into support mode until we reconnect.')
                config['supportMode'] = True
                json.dump(config, open(pathlib.Path(__file__).parent / 'config.json', 'w'), indent=4)


        # Hibernate mode?
        if config['hibernateMode']:
            # If we've awoken from hibernate - let's check it's within 5 minutes of the hour, or if hour is other than 0, 6, 12, or 18.
            # If not, user may have pressed button - let's switch out of hibernate mode.
            #
            mcu_time = pvpiClient.get_mcu_time()
            if uptimeSeconds < 300 and (mcu_time.minute > 5 or mcu_time.hour % 6 != 0):
                logger.info('hibernate mode - but looks like we have been woken by user - switching out of hibernate mode.')
                loggerIntent.info('hibernate mode - but looks like we have been woken by user - switching out of hibernate mode, and into support mode.')
                config['hibernateMode'] = False
                config['supportMode'] = True
                json.dump(config, open(pathlib.Path(__file__).parent / 'config.json', 'w'), indent=4)


        # Power Off mode?
        if config['powerOff']:
            # If we've awoken from Power Off, let's switch out of Power Off
            #
            if uptimeSeconds < 300 :
                logger.info('Power Off - but looks like we have been woken by user - switching out of Power Off.')
                loggerIntent.info('Power Off - but looks like we have been woken by user - switching out of Power Off.')
                config['hibernateMode'] = False
                config['powerOff'] = False
                config['supportMode'] = True
                json.dump(config, open(pathlib.Path(__file__).parent / 'config.json', 'w'), indent=4)

            else:

                # Otherwise, let's cancel watchdog and power down.
                logger.info('Power Off - cancelling watchdog and powering down.')
                loggerIntent.info('Power Off - cancelling watchdog and powering down.')
                pvpiClient.stop_watchdog()

                loggerIntent.info('Power off scheduled for 1 min from now')
                pvpiClient.power_off(60)
                powerDownSIM7600X()
                logger.info('Shutting down now...')
                loggerIntent.info('Shutting down now...')
                subprocess.call(['sudo', 'shutdown', '-h', 'now'])

                # Exit to make sure we don't than go and undo the power off!
                return


        # If hibernating, wake up at next 6 hourly interval
        # e.g. midnight, 6am, 12pm, 6pm (UTC)
        utcnow = datetime.datetime.now(datetime.timezone.utc)
        hibernateHoursToWakeAfter = 6 - (utcnow.hour % 6)
        hibernateHourToWakeAt = utcnow.hour + hibernateHoursToWakeAfter

        if hibernateHourToWakeAt >= 24:
            hibernateHourToWakeAt = hibernateHourToWakeAt - 24


        # Hibernate mode? Lets have 10 minutes to give it a chance to check again before hibernating.
        if config['hibernateMode']:
            logger.info('hibernate mode - stay awake for 10 mins')
            loggerIntent.info('hibernate mode - stay awake for 10 mins')
            if uptimeSeconds > 600:
                logger.info('hibernate mode - sleeping for 6 hours...')
                loggerIntent.info('hibernate mode - sleeping for 6 hours...')

                alarm_time = datetime.time(hibernateHourToWakeAt, 0, 0)
                setAlarm = True

        else:

            if config['sleep_during_night'] == True \
                and (datetime.datetime.now().hour >= config['daytime_ends_at_h'] or datetime.datetime.now().hour < config['daytime_starts_at_h']) \
                and config['supportMode'] == False \
                and datetime.datetime.now().minute >= 10 \
                and bCharging == False:
                logger.info("Night time so we're scheduling shutdown")
                loggerIntent.info("Night time so we're scheduling shutdown")

                next_hour = datetime.datetime.now() + datetime.timedelta(hours=1)
                alarm_time = datetime.time(next_hour.hour, 0, 0)
                setAlarm = True

            else:

                # low_battery_voltage - at this battery voltage, we go to sleep and wake up every 10 minutes.
                # ideally 100mV above the PvPi low_bat_volatge of 12.9

                # Also let's give it a chance to upload once an hour to catch up and avoid anxiety that camera has been stolen

                # Check if voltage is currently low
                voltage_is_low = (config['low_battery_voltage'] > 0 
                                  and round(pvpiClient.get_battery_voltage()*1000) <= config['low_battery_voltage'] 
                                  and datetime.datetime.now().minute >= 10 
                                  and bCharging == False)

                if voltage_is_low:
                    low_voltage_count += 1
                    logger.debug(f"Low voltage detected (count: {low_voltage_count}/{LOW_VOLTAGE_THRESHOLD})")
                    loggerIntent.debug(f"Low voltage detected (count: {low_voltage_count}/{LOW_VOLTAGE_THRESHOLD})")
                else:
                    if low_voltage_count > 0:
                        logger.debug(f"Voltage returned to normal, resetting count from {low_voltage_count}")
                        loggerIntent.debug(f"Voltage returned to normal, resetting count from {low_voltage_count}")
                    low_voltage_count = 0

                # Only take action after 5 consecutive low voltage readings
                if low_voltage_count >= LOW_VOLTAGE_THRESHOLD:

                    # if round(pvpiClient.get_battery_voltage()*1000) > config['pvpi_low_battery_voltage']:
                    logger.info(f'scheduling 10 minute sleep due to {LOW_VOLTAGE_THRESHOLD} consecutive low battery readings')
                    loggerIntent.info(f'scheduling 10 minute sleep due to {LOW_VOLTAGE_THRESHOLD} consecutive low battery readings')
                    logger.info(f"Battery voltage: {round(pvpiClient.get_battery_voltage()*1000)} mV")
                    logger.info(f"State of charge: {pvpiClient.estimated_soc()}%")
                    logger.info(f"Charge state: {pvpiClient.get_charge_state()}")

                    time.sleep(30)

                    wake_time = datetime.datetime.now() + datetime.timedelta(minutes=10)
                    alarm_time = datetime.time(wake_time.hour, wake_time.minute, 0)

                    # # If we're down at hibernate level, let's just hibernate.
                    # else:
                    #     logger.info(f'Hibernating due to {LOW_VOLTAGE_THRESHOLD} consecutive very low battery readings')
                    #     loggerIntent.info(f'Hibernating due to {LOW_VOLTAGE_THRESHOLD} consecutive very low battery readings')
                    #     logger.info(f"Battery voltage: {round(pvpiClient.get_battery_voltage()*1000)} mV")
                    #     logger.info(f"State of charge: {pvpiClient.estimated_soc()}%")
                    #     logger.info(f"Charge state: {pvpiClient.get_charge_state()}")
                    #     alarm_time = datetime.time(hibernateHourToWakeAt, 0, 0)

                    setAlarm = True

                if not voltage_is_low or low_voltage_count < LOW_VOLTAGE_THRESHOLD:

                    # If we've been up for more than 2 modem cycles or 30 minutes, and the most recently captured image is older than 10 minutes, or the most recently uploaded image is older than 30 minutes,
                    # either network is out, or we can't get a cellular signal, DNS is messing around, or camera isn't capturing.
                    # Let's shutdown, power down, and wake up again in 3 mins to see if that fixes it.
                    power_interval = config['modem.power_interval']

                    if uptimeSeconds > power_interval * 2 and uptimeSeconds > 1800:
                        mostRecentUploadedFiles = sorted(glob.iglob(uploadedImageFolder + "/*.*"), key=os.path.getctime, reverse=True)
                        mostRecentPendingFiles = sorted(glob.iglob(pendingImageFolder + "/*.*"), key=os.path.getctime, reverse=True)

                        secondsSinceLastUpload = -1
                        secondsSinceLastImageCapture = -1

                        if len(mostRecentPendingFiles) > 0:
                            latestImageCapturedFilename = max(mostRecentPendingFiles, key=os.path.getctime)
                            secondsSinceLastImageCapture = (datetime.datetime.now() - datetime.datetime.fromtimestamp(os.path.getctime(latestImageCapturedFilename))).total_seconds()
                            logger.debug("secondsSinceLastImageCapture: " + str(secondsSinceLastImageCapture))

                        if len(mostRecentUploadedFiles) > 0:

                            latestUploadedFilename = max(mostRecentUploadedFiles, key=os.path.getctime)

                            secondsSinceLastUpload = (datetime.datetime.now() - datetime.datetime.fromtimestamp(os.path.getctime(latestUploadedFilename))).total_seconds()
                            logger.debug("secondsSinceLastUpload: " + str(secondsSinceLastUpload))


                        # Most recent image captured (may also be in uploaded folder) is older than 10 minutes
                        if secondsSinceLastImageCapture > 600 and secondsSinceLastUpload > 600:
                            logger.warning('Most recent captured image is ' + str(secondsSinceLastImageCapture) + 'seconds old, and uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            loggerIntent.warning('Most recent captured image is ' + str(secondsSinceLastImageCapture) + 'seconds old, and uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            triggerRestart = True

                        if secondsSinceLastUpload > 1800:
                            logger.warning('Most recent uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            loggerIntent.warning('Most recent uploaded image is ' + str(secondsSinceLastUpload) + ' seconds old - restarting...')
                            triggerRestart = True

                        if len(mostRecentPendingFiles) == 0 and len(mostRecentUploadedFiles) == 0:
                            logger.debug("No uploaded or captured images found - restarting...")
                            loggerIntent.debug("No uploaded or captured images found - restarting...")
                            triggerRestart = True

                            # wake_time = datetime.datetime.now() + datetime.timedelta(minutes=3)
                            # alarm_time = datetime.time(wake_time.hour, wake_time.minute, 0)
                            # setAlarm = True

        if(setAlarm == True and triggerRestart == True):
            logger.error("scheduleShutdown - we've set both setAlarm and triggerRestart - ignore and treat as restart")
            setAlarm = False

        if(setAlarm == True or triggerRestart == True):
            logger.info("scheduleShutdown - we're going to sleep soon...")
            loggerIntent.info("scheduleShutdown - we're going to sleep soon...")

            logger.debug('get_mcu_time(): ' + str(pvpiClient.get_mcu_time()))
            loggerIntent.debug('get_mcu_time(): ' + str(pvpiClient.get_mcu_time()))

            if triggerRestart:
                SetWatchdog();

            if setAlarm == True:
                SetAlarm(alarm_time)

            logger.info('Power off scheduled for 10 seconds from now')
            loggerIntent.info('Power off scheduled for 10 seconds from now')
            pvpiClient.power_off(10)
            powerDownSIM7600X()
            logger.info('Shutting down now...')
            loggerIntent.info('Shutting down now...')
            subprocess.call(['sudo', 'shutdown', '-h', 'now'])

    except Exception as e:
        logger.error("scheduleShutdown() failed.")
        logger.error(e)

def SetWatchdog(timeout = 3):
    try:
        if not pj_is_alive():
            logger.error('PvPi not connected')
            loggerIntent.error('PvPi not connected')
            return

        if(timeout == 0):
            pvpiClient.stop_watchdog()
            logger.debug('Watchdog stopped')
            loggerIntent.debug('Watchdog stopped')
            return

        # pvpi watchdog accepts 1-60 mins
        timeout = min(timeout, 60)

        logger.debug('Setting Watchdog...')
        loggerIntent.debug('Setting Watchdog...')

        watchdogSet = False
        while watchdogSet == False:
            try:
                pvpiClient.set_watchdog(timeout)
                logger.debug('Watchdog set for ' + str(timeout) + ' mins')
                loggerIntent.debug('Watchdog set for ' + str(timeout) + ' mins')
                watchdogSet = True
            except Exception as e:
                logger.error('Cannot set watchdog: ' + str(e))
                logger.info('Sleeping and retrying...\n')
                time.sleep(10)

    except Exception as e:
        logger.error("SetWatchdog() failed.")
        logger.error(e)

def SetAlarm(wakeTime: time):
    try:
        if not pj_is_alive():
            logger.error('PvPi not connected')
            loggerIntent.error('PvPi not connected')
            return

        now = datetime.datetime.now()
        wake_dt = datetime.datetime.combine(now.date(), wakeTime)
        if wake_dt <= now:
            wake_dt += datetime.timedelta(days=1)
        minutes_until = max(1, int((wake_dt - now).total_seconds() / 60))

        if minutes_until <= 60:
            logger.info(f'Alarm is {minutes_until} min away - using watchdog instead of RTC alarm (more reliable at low voltage)')
            loggerIntent.info(f'Alarm is {minutes_until} min away - using watchdog instead of RTC alarm (more reliable at low voltage)')
            SetWatchdog(minutes_until)
            return

        logger.debug('Setting Alarm...')
        loggerIntent.debug('Setting Alarm...')

        alarmSet = False
        while alarmSet == False:
            try:
                SetWatchdog(0) # stop watchdog if we're using RTC alarm
                pvpiClient.set_alarm(wakeTime)
                logger.debug('Alarm set for ' + str(wakeTime))
                loggerIntent.debug('Alarm set for ' + str(wakeTime))
                alarmSet = True

            except Exception as e:
                logger.error('Cannot set alarm: ' + str(e))
                logger.info('Sleeping and retrying...\n')
                time.sleep(10)

    except Exception as e:
        logger.error("SetAlarm() failed.")
        logger.error(e)

def saveTelemetry():
    try:

        api_data = {
                    'diskSpaceFree': shutil.disk_usage('/')[2] // (1024**3), # shutil.disk_usage returns tuple of (total, used, free), converted to int gb
                    'pendingImages': len(os.listdir(pendingImageFolder)),
                    'uploadedImages': len(os.listdir(uploadedImageFolder)),
                    'pendingTelemetry': len(os.listdir(pendingTelemetryFolder)),
                    'uploadedTelemetry': len(os.listdir(uploadedTelemetryFolder)),
                    'uptimeSeconds': int(time.clock_gettime(time.CLOCK_BOOTTIME)),
                    'SerialNumber': serialNumber
                }

        if not pj_is_alive():
            api_data['batteryPercent'] = 0
            api_data['temperatureC'] = 0
            api_data['status']= str({ 'status': {
                                    'chargeState': 'UNKNOWN', 
                                    'faults': [],
                                    'battery': 'UNKNOWN',
                                    'powerInput': 'UNKNOWN',
                                },
                                'batteryVoltage': 0,
                                'batteryCurrent': 0,
                                'ioVoltage': 0,
                                'ioCurrent': 0,
                                'connectedToWirelessNetwork': is_connected_to_wifi_linux(),
                                'wirelessSSID': wifiSSID(),
                                'connectedToInternet': internet(),
                                'powerSwitch': check_usb_power_status(),
                            })
        else:
            api_data['batteryPercent'] = round(pvpiClient.estimated_soc())
            api_data['temperatureC'] = pvpiClient.get_board_temp()
            api_data['status']= str({ 'status': {
                                    'chargeState': pvpiClient.get_charge_state(),
                                    'faults': pvpiClient.get_fault_states(),
                                    'battery': 'UNKNOWN',
                                    'powerInput': 'UNKNOWN',
                                },
                                'batteryVoltage': round(pvpiClient.get_battery_voltage()*1000),
                                'batteryCurrent': round(pvpiClient.get_battery_current()*1000),
                                'ioVoltage': round(pvpiClient.get_pv_voltage()*1000),
                                'ioCurrent': round(pvpiClient.get_pv_current()*1000),
                                'connectedToWirelessNetwork': is_connected_to_wifi_linux(),
                                'wirelessSSID': wifiSSID(),
                                'connectedToInternet': internet(),
                                'powerSwitch': check_usb_power_status(),
                            })

        telemetryFilename = pendingTelemetryFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.json')
        with open(telemetryFilename, 'w') as outfile:
            json.dump(api_data, outfile)
            logger.debug('telemetry saved')
        
        # Try to upload immediately if connected to internet
        if internet():
            logger.debug('connected to internet - attempting immediate upload')
            try:
                session = requests.Session()
                uploadTelemetry(telemetryFilename, session)
            except Exception as e:
                logger.warning(f'immediate upload failed: {e}')
                logger.debug('will retry via uploadPending.py later')

    except Exception as e:
        logger.error("saveTelemetry() failed.")
        logger.error(e)

def syncClocks():
    """Synchronize system clock and PvPi MCU clock if needed."""
    if pj_is_alive():
        try:
            mcu_time = pvpiClient.get_mcu_time()
            loggerIntent.info('pvpi MCU time: ' + str(mcu_time))
            loggerIntent.info('system time: ' + str(datetime.datetime.now()))

            if(abs((mcu_time - datetime.datetime.now()).total_seconds()) < 5):
                loggerIntent.info('pvpi MCU time and system time are within 5 seconds of each other, so we will not update either clock.')
            else:

                if(mcu_time > datetime.datetime.now()):
                    loggerIntent.info('setting sys clock from pvpi MCU time...')
                    if mcu_time.year <= 2025:
                        loggerIntent.warning("MCU time looks wrong, so we're not setting system clock from it.")
                        loggerIntent.warning("MCU time looks wrong, so we're not setting system clock from it.")
                    else:
                        subprocess.call(['sudo', 'date', '-s', mcu_time.strftime('%Y-%m-%d %H:%M:%S')])
                else:
                    loggerIntent.info('pvpi MCU time is behind system time, so we will set mcu from sys clock.')
                    pvpiClient.set_mcu_time(datetime.datetime.now())

        except Exception as e:
            loggerIntent.error("Failed to set sys clock from pvpi MCU time")
            loggerIntent.error(e)


def main():
    """Main entry point for saveTelemetry script."""
    syncClocks()
    
    try:
        logger.info('In saveTelemetry.py')

        SetWatchdog()

        while True:
            saveTelemetry()
            time.sleep(60)
            scheduleShutdown()
    except Exception as e:
        logger.error("Catastrophic failure.")
        scheduleShutdown()
        logger.error(e)


if __name__ == "__main__":
    main()
