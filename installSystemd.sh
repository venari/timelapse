#!/bin/bash

# bash <(curl -fsSL "https://github.com/venari/timelapse/raw/feature/systemd-merge/installSystemd.sh?$RANDOM")

# Ask user if we have a waveshare modem
read -p "Does this camera have a waveshare SIM7600X modem? (y/n)" waveshare

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
    git checkout feature/systemd-merge
else
    echo Updating repo...
    cd dev/timelapse
    # git checkout development
    git fetch
    git stash
    git checkout feature/systemd-merge
    git pull
    git stash pop
fi


# Clear out any old crontab entries
# (echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/loggingSocketServer.sh")| crontab -
crontab -r

echo Installing systemd services...
sudo cp /home/pi/dev/timelapse/systemd/system/*.* /etc/systemd/system/
sudo chmod u+x /etc/systemd/system/enviro*.*
sudo systemctl enable envirocam-logging.service
sudo systemctl enable envirocam-telemetry.service
sudo systemctl enable envirocam-photos.timer
sudo systemctl enable envirocam-upload.timer
sudo systemctl enable envirocam-detect-hang.timer

# If not bookworm - don't have epaper library yet
if ! grep -q "bookworm" /etc/os-release; then
    sudo cp /home/pi/dev/timelapse/scripts/envirocam-status.timer /etc/systemd/system/
    sudo systemctl enable envirocam-status.timer
fi

# If not waveshare, we can't access SMS messages
if [ $waveshare == "y" ]; then
    sudo cp /home/pi/dev/timelapse/scripts/envirocam-sms.timer /etc/systemd/system/
    sudo systemctl enable envirocam-sms.timer
fi




echo We need to reboot to kick off cron jobs
echo "Press any key to reboot"

echo ===========================================
echo Please check battery profile in pijuice_cli
echo ===========================================

read -n 1 -s
sudo reboot
