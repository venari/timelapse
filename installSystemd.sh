#!/bin/bash

# bash <(curl -fsSL "https://github.com/venari/timelapse/raw/feature/systemd-merge/installSystemd.sh?$RANDOM")

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










echo Overwriting pijuice config...
sudo mv /var/lib/pijuice/pijuice_config.JSON /var/lib/pijuice/pijuice_config.JSON.bak
sudo curl -fsSL -o /var/lib/pijuice/pijuice_config.JSON https://raw.githubusercontent.com/venari/timelapse/main/pijuice_config.JSON
sudo chown pijuice:pijuice /var/lib/pijuice/pijuice_config.JSON

echo Installing Tailscale...
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up

# Query user for hostname, provide a default value
read -p "Current hostname is $(hostname) - would you like to change it?" yn
case $yn in 
    [Yy]* ) echo "Changing hostname";
        read -p "Enter new hostname if desired: " -i sediment-pi- -e hostname
        echo Setting hostname to $hostname
        sudo hostnamectl set-hostname $hostname;;

    [Nn]* ) echo "Skipping hostname change";;
    * ) echo "Please answer yes or no.";;
esac

echo We need to reboot to kick off cron jobs
echo "Press any key to reboot"

echo ===========================================
echo Please check battery profile in pijuice_cli
echo ===========================================

read -n 1 -s
sudo reboot
