import json
import pathlib
import socket
import time
from datetime import datetime, timedelta
from zoneinfo import ZoneInfo
from suncalc import get_times


config = json.load(open(pathlib.Path(__file__).parent / 'config.json'))
# Load the local config if it exists
try:
    with open(pathlib.Path(__file__).parent / 'config.local.json', 'r') as f:
        local_config = json.load(f)
    # Update the primary config with overrides from the local config
    config.update(local_config)
except FileNotFoundError:
    print("config.local.json not found. Using default config.")


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



def flashLED(pj, led='D2', R=0, G=0, B=255, flashCount=3, flashDelay=0.5):
    for i in range(0, flashCount):
        pj.status.SetLedState(led, [R, G, B])
        time.sleep(flashDelay)
        pj.status.SetLedState(led, [0, 0, 0])
        time.sleep(flashDelay)

def currentPhase():
    if(config["location.lon"] and config["location.lat"]):

        timezone = now.astimezone().tzinfo
        # print (f"timezone: {timezone}")
        # print (f"now: {now.astimezone()}")
        solar_times = get_times(now, config["location.lon"], config["location.lat"])
        solar_times_tomorrow = get_times(now + timedelta(days=1), config["location.lon"], config["location.lat"])

        # Convert solar times to the specified timezone
        for key in solar_times:
            solar_times[key] = solar_times[key].astimezone(timezone)

        for key in solar_times_tomorrow:
            solar_times_tomorrow[key] = solar_times_tomorrow[key].astimezone(timezone)

        # print (f"\nsolar_times: {solar_times}")

        # Solar phases in order
        phases = {
            "night_end": solar_times["night_end"],
            "nautical_dawn": solar_times["nautical_dawn"],
            "dawn": solar_times["dawn"],
            "sunrise": solar_times["sunrise"],
            "solar_noon": solar_times["solar_noon"],
            "sunset": solar_times["sunset"],
            "dusk": solar_times["dusk"],
            "nautical_dusk": solar_times["nautical_dusk"],
            "night": solar_times["night"],

            "night_end_tomorrow": solar_times_tomorrow["night_end"],
            "nautical_dawn_tomorrow": solar_times_tomorrow["nautical_dawn"],
            "dawn_tomorrow": solar_times_tomorrow["dawn"],
            "sunrise_tomorrow": solar_times_tomorrow["sunrise"],
            "solar_noon_tomorrow": solar_times_tomorrow["solar_noon"],
            "sunset_tomorrow": solar_times_tomorrow["sunset"],
            "dusk_tomorrow": solar_times_tomorrow["dusk"],
            "nautical_dusk_tomorrow": solar_times_tomorrow["nautical_dusk"],
            "night_tomorrow": solar_times_tomorrow["night"]
        }

        # print (f"\nphases: {phases}")

        # Determine the current solar phase
        current_phase = None
        for phase, time in sorted(phases.items(), key=lambda x: x[1]):
            if now.astimezone(timezone) < time.astimezone(timezone):
                break
            current_phase = phase

        if("_tomorrow" in current_phase):
            current_phase = current_phase.replace("_tomorrow", "")

        # Map phases to human-readable names
        phase_names = {
            "night_end": "Night (before dawn)",
            "nautical_dawn": "Nautical Dawn",
            "dawn": "Dawn",
            "sunrise": "Sunrise",
            "solar_noon": "Daytime",
            "sunset": "Sunset",
            "dusk": "Dusk",
            "nautical_dusk": "Nautical Dusk",
            "night": "Night (after dusk)"
        }

        # # Print the current phase
        # if current_phase:
        #     print(f"The current solar phase is: {phase_names[current_phase]}")
        # else:
        #     print("Could not determine the current solar phase.")

        return current_phase
    else:
        print("Location not set in config.json")
        return None