#!/bin/bash

echo Updating....
sudo apt-get update

echo Upgrading...
sudo apt-get upgrade -y

echo Installing...
sudo apt-get install git pijuice-base python3-pip -y
sudo apt install -y python3-picamera2 --no-install-recommends
sudo apt-get install vim byobu -y

byobu-enable

echo Setting timezone...
sudo timedatectl set-timezone Pacific/Auckland


echo Cloning repo...
# Check if dev folder exists
if [ ! -d "/home/pi/dev/timelapse" ]; then
    mkdir -p dev
    cd dev
    git clone https://github.com/venari/timelapse.git
    cd timelapse
    git config pull.rebase false
    git checkout development
else
    cd dev/timelapse
    git checkout development
    git pull
fi

echo Checking RTC module is enabled in boot/config.txt
grep -qxF 'dtoverlay=i2c-rtc,ds1307=1' /boot/config.txt || echo 'dtoverlay=i2c-rtc,ds1307=1' | sudo tee -a /boot/config.txt

echo Installing crontab entries...
# (crontab -l 2>/dev/null; echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh")| crontab -
(echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/saveTelemetry.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/savePhotos.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot sleep 60 && /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh")| crontab -

echo Overwriting pijuice config...
sudo mv /var/lib/pijuice/pijuice_config.JSON /var/lib/pijuice/pijuice_config.JSON.bak
sudo curl -fsSL -o /var/lib/pijuice/pijuice_config.JSON https://raw.githubusercontent.com/venari/timelapse/development/pijuice_config.JSON
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
read -n 1 -s
sudo reboot
