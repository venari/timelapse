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
# from logging.handlers import TimedRotatingFileHandler
from logging.handlers import SocketHandler
import pathlib
import glob

from helpers import internet

from SIM7600X import powerUpSIM7600X, powerDownSIM7600X
from isConnectedToWifi import is_connected_to_wifi_linux, wifiSSID

config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
logFilePath = config["logFilePath"]
intentLogFilePath = logFilePath.replace("timelapse.log", "intent.log")
# logFilePath = logFilePath.replace(".log", ".saveTelemetry.log")
os.makedirs(os.path.dirname(logFilePath), exist_ok=True)


formatter = logging.Formatter('%(asctime)s %(name)s %(levelname)s %(message)s')
# handler = TimedRotatingFileHandler(logFilePath,
#                                    when='midnight',
#                                    backupCount=10)
handler = SocketHandler('localhost', 8000)
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

# clock
while not os.path.exists('/dev/i2c-1'):
    logger.info("dev i2c-1 doesn't exist")
    time.sleep(0.1)

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
    logger.info('PvPi not connected')
else:
    logger.info('PvPi is connected')


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

def scheduleShutdown():
    try:
        if not pj_is_alive():
            logger.info('PvPi not connected')
            return

        alarm_time = None

        # print(str(datetime.datetime.now()) + ' scheduleShutdown')
        logger.debug('scheduleShutdown')
        logger.debug('get_mcu_time(): ' + str(pvpiClient.get_mcu_time()))

        setAlarm = False
        triggerRestart = False

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
        hibernateHoursToWakeAfter = 6 - (datetime.datetime.utcnow().hour % 6)
        hibernateHourToWakeAt = datetime.datetime.utcnow().hour + hibernateHoursToWakeAfter

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

                # # Set watchdog to 60 mins (pvpi max) as safety backup for wakeup alarm failure
                # SetWatchdog(60)
                # Max watchdog period for pvpi is 60 minutes, which is shorter than our hibernate period, so we'll rely on the RTC alarm to wake us up

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

                # Set Watchdog to 60 mins (pvpi max) to catch wakeup failure
                SetWatchdog(60)

            else:

                # sleep_at_battery_percent - at this battery percentage, we go to sleep and wake up every 10 minutes.
                # pijuice_config.JSON.system_task.min_charge.threshold - at this battery percentage, the min_charge setting
                # puts us to sleep until battery recovers, so we pre-empt it by 5% and shut down more gracefully.

                # Also let's give it a chance to upload once an hour to catch up and avoid anxiety that camera has been stolen

                if config['sleep_at_battery_percent'] > 0 and config['pijuice_config.JSON.system_task.min_charge.threshold'] > 0 \
                and pvpiClient.estimated_soc() <= config['sleep_at_battery_percent'] \
                and datetime.datetime.now().minute >= 10 \
                and config['supportMode'] == False \
                and bCharging == False:

                    if pvpiClient.estimated_soc() > config['pijuice_config.JSON.system_task.min_charge.threshold'] + 5:
                        logger.info('scheduling 10 minute sleep due to low battery')
                        loggerIntent.info('scheduling 10 minute sleep due to low battery')
                        logger.info(pvpiClient.estimated_soc())
                        logger.info(pvpiClient.get_charge_state())

                        time.sleep(30)

                        wake_time = datetime.datetime.now() + datetime.timedelta(minutes=10)
                        alarm_time = datetime.time(wake_time.hour, wake_time.minute, 0)

                        # Set watchdog to 15 mins to catch wakeup alarm failure
                        SetWatchdog(15)

                    # If we're down at hibernate level, let's just hibernate.
                    else:
                        logger.info('Hibernating due to very low battery')
                        loggerIntent.info('Hibernating due to very low battery')
                        logger.info(pvpiClient.estimated_soc())
                        logger.info(pvpiClient.get_charge_state())
                        alarm_time = datetime.time(hibernateHourToWakeAt, 0, 0)
                        # Set watchdog to 60 mins (pvpi max) to catch wakeup alarm failure
                        SetWatchdog(60)

                    setAlarm = True

                else:

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

                        if triggerRestart:
                            wake_time = datetime.datetime.now() + datetime.timedelta(minutes=3)
                            alarm_time = datetime.time(wake_time.hour, wake_time.minute, 0)
                            setAlarm = True
                            SetWatchdog(5)

        if setAlarm == True:
            logger.info("scheduleShutdown - we're setting the shutdown...")
            loggerIntent.info("scheduleShutdown - we're setting the shutdown...")

            alarmSet = False
            while alarmSet == False:
                try:
                    pvpiClient.set_alarm(alarm_time)
                    logger.debug('Alarm set for ' + str(alarm_time))
                    loggerIntent.debug('Alarm set for ' + str(alarm_time))
                    alarmSet = True
                except Exception as e:
                    logger.error('Cannot set alarm: ' + str(e))
                    logger.info('Sleeping and retrying...\n')
                    time.sleep(10)

            logger.debug('get_mcu_time(): ' + str(pvpiClient.get_mcu_time()))
            loggerIntent.debug('get_mcu_time(): ' + str(pvpiClient.get_mcu_time()))

            if triggerRestart:
                logger.info('Restart scheduled for 3 minutes from now')
                logger.info("So we'll skip the power off.")
            else:
                logger.info('Power off scheduled for 1 min from now')
                loggerIntent.info('Power off scheduled for 1 min from now')
                pvpiClient.power_off(60)

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
            logger.info('PvPi not connected')
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
            api_data['status']= str({ 'status': {'chargeState': 'UNKNOWN', 'faults': []},
                                'batteryVoltage': 0,
                                'batteryCurrent': 0,
                                'ioVoltage': 0,
                                'ioCurrent': 0,
                                'connectedToWirelessNetwork': is_connected_to_wifi_linux(),
                                'wirelessSSID': wifiSSID(),
                                'connectedToInternet': internet(),
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
                            })

        telemetryFilename = pendingTelemetryFolder + datetime.datetime.now().strftime('%Y-%m-%d_%H%M%S.json')
        with open(telemetryFilename, 'w') as outfile:
            json.dump(api_data, outfile)
            logger.debug('telemetry saved')

    except Exception as e:
        logger.error("saveTelemetry() failed.")
        logger.error(e)

if pj_is_alive():
    try:
        logger.debug('setting sys clock from pvpi MCU time...')
        loggerIntent.debug('setting sys clock from pvpi MCU time...')
        mcu_time = pvpiClient.get_mcu_time()
        subprocess.call(['sudo', 'date', '-s', mcu_time.strftime('%Y-%m-%d %H:%M:%S')])
        logger.debug("sys clock set from pvpi MCU time")
    except Exception as e:
        logger.error("Failed to set sys clock from pvpi MCU time")
        logger.error(e)


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
