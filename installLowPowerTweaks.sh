#!/bin/bash

# bash <(curl -fsSL "https://github.com/venari/timelapse/raw/feature/lowPowerTweaks/installLowPowerTweaks.sh?$RANDOM")


# Enable wakeup logging.
python3 /usr/bin/pijuice_log.py --enable WAKEUP_EVT


cd /home/pi
# Check if dev folder exists
if [ ! -d "/home/pi/dev/timelapse" ]; then
    echo Cloning repo...
    mkdir -p dev
    cd dev
    git clone https://github.com/venari/timelapse.git
    cd timelapse
    git config pull.rebase false
    # git checkout development
    git checkout feature/lowPowerTweaks
else
    echo Updating repo...
    cd dev/timelapse
    # git checkout development
    git fetch
    git stash
    git checkout feature/lowPowerTweaks
    git pull
    git stash pop
fi

echo Installing crontab entries...

(echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/loggingSocketServer.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/saveTelemetry.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot sleep 150 && /usr/bin/bash /home/pi/dev/timelapse/scripts/savePhotos.sh")| crontab -

(crontab -l 2>/dev/null; echo "@reboot sleep 60 && /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh")| crontab -

(crontab -l 2>/dev/null; echo "*/15 * * * * /usr/bin/bash /home/pi/dev/timelapse/scripts/detectHang.sh")| crontab -

echo Overwriting pijuice config...
sudo mv /var/lib/pijuice/pijuice_config.JSON /var/lib/pijuice/pijuice_config.JSON.bak
sudo curl -fsSL -o /var/lib/pijuice/pijuice_config.JSON https://raw.githubusercontent.com/venari/timelapse/feature/lowPowerTweaks/pijuice_config.JSON
sudo chown pijuice:pijuice /var/lib/pijuice/pijuice_config.JSON

echo We need to reboot to change PiJuice min_charge
echo "Press any key to reboot"

echo ===========================================
echo Please check battery profile in pijuice_cli
echo ===========================================

read -n 1 -s
sudo reboot
