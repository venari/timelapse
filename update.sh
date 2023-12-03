#!/bin/bash

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
    # git checkout development
    git pull
fi

echo Checking RTC module is enabled in boot/config.txt
grep -qxF 'dtoverlay=i2c-rtc,ds1307=1' /boot/config.txt || echo 'dtoverlay=i2c-rtc,ds1307=1' | sudo tee -a /boot/config.txt

echo Checking static domain_name_servers entry etc/dhcpcd.conf
grep -qxF 'static domain_name_servers=8.8.4.4 8.8.8.8' /etc/dhcpcd.conf || echo 'static domain_name_servers=8.8.4.4 8.8.8.8' | sudo tee -a /etc/dhcpcd.conf

echo Installing crontab entries...
# (crontab -l 2>/dev/null; echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh")| crontab -
(echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/saveTelemetry.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot sleep 10 && /usr/bin/bash /home/pi/dev/timelapse/scripts/savePhotos.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot sleep 20 && /usr/bin/bash /home/pi/dev/timelapse/scripts/updateStatus.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot sleep 60 && /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh")| crontab -
(crontab -l 2>/dev/null; echo "* * * * * /usr/bin/bash /home/pi/dev/timelapse/scripts/updateStatus.sh")| crontab -

echo Overwriting pijuice config...
sudo mv /var/lib/pijuice/pijuice_config.JSON /var/lib/pijuice/pijuice_config.JSON.bak
sudo curl -fsSL -o /var/lib/pijuice/pijuice_config.JSON https://raw.githubusercontent.com/venari/timelapse/main/pijuice_config.JSON
sudo chown pijuice:pijuice /var/lib/pijuice/pijuice_config.JSON

# echo Installing Tailscale...
# curl -fsSL https://tailscale.com/install.sh | sh
# sudo tailscale up

# # Query user for hostname, provide a default value
# read -p "Current hostname is $(hostname) - would you like to change it?" yn
# case $yn in 
#     [Yy]* ) echo "Changing hostname";
#         read -p "Enter new hostname if desired: " -i sediment-pi- -e hostname
#         echo Setting hostname to $hostname
#         sudo hostnamectl set-hostname $hostname;;

#     [Nn]* ) echo "Skipping hostname change";;
#     * ) echo "Please answer yes or no.";;
# esac

echo We need to reboot to kick off cron jobs
echo "Press any key to reboot"
read -n 1 -s
sudo reboot
