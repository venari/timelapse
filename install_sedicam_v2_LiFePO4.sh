#!/bin/bash

echo Updating....
sudo apt-get update

echo Upgrading...
sudo apt-get upgrade -y

echo Installing...
sudo apt-get install git pijuice-base python3-pip -y
sudo apt install -y python3-picamera2 --no-install-recommends
sudo apt-get install vim byobu -y
sudo apt-get install python3-pil -y
sudo pip3 install RPi.GPIO
sudo pip3 install waveshare-epaper

pip3 install pyserial

byobu-enable

echo Setting timezone...
sudo timedatectl set-timezone Pacific/Auckland


# # Waveshare stuff

# # Enable Serial Communication
# sudo raspi-config nonint do_serial 2        # Disable serial login shell and enable serial port hardware

# # Check if folder SIM7600X-4G-HAT-Demo exists:
# if [ ! -d "/home/pi/SIM7600X-4G-HAT-Demo" ]; then
#     #https://core-electronics.com.au/guides/raspberry-pi/raspberry-pi-4g-gps-hat/
#     wget https://www.waveshare.com/w/upload/2/29/SIM7600X-4G-HAT-Demo.7z
#     sudo apt-get install p7zip-full
#     7z x SIM7600X-4G-HAT-Demo.7z -r -o/home/pi
#     sudo chmod 777 -R /home/pi/SIM7600X-4G-HAT-Demo


#     cd /home/pi/SIM7600X-4G-HAT-Demo/Raspberry/c/bcm2835
#     chmod +x configure && ./configure && sudo make && sudo make install
# fi


# # sed -e '$i \sh /home/pi/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init\n' /etc/rc.local
# grep -qxF 'sh /home/pi/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init' /etc/rc.local || sudo sed -i -e '$i \sh /home/pi/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init\n' /etc/rc.local
# ###################

echo Cloning repo...
cd /home/pi
# Check if dev folder exists
if [ ! -d "/home/pi/dev/timelapse" ]; then
    mkdir -p dev
    cd dev
    git clone https://github.com/venari/timelapse.git
    cd timelapse
    git config pull.rebase false
    # git checkout development
    git checkout deployment/sedicam_v2_LiFePO4
else
    cd dev/timelapse
    # git checkout development
    git checkout deployment/sedicam_v2_LiFePO4
    git pull
fi

echo Checking RTC module is enabled in boot/config.txt
grep -qxF 'dtoverlay=i2c-rtc,ds1307=1' /boot/config.txt || echo 'dtoverlay=i2c-rtc,ds1307=1' | sudo tee -a /boot/config.txt

echo Checking static domain_name_servers entry etc/dhcpcd.conf
grep -qxF 'static domain_name_servers=8.8.4.4 8.8.8.8' /etc/dhcpcd.conf || echo 'static domain_name_servers=8.8.4.4 8.8.8.8' | sudo tee -a /etc/dhcpcd.conf

echo Installing crontab entries...

(echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/saveTelemetry.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot sleep 10 && /usr/bin/bash /home/pi/dev/timelapse/scripts/savePhotos.sh")| crontab -

(crontab -l 2>/dev/null; echo "@reboot sleep 60 && /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh")| crontab -

(crontab -l 2>/dev/null; echo "* * * * * /usr/bin/bash /home/pi/dev/timelapse/scripts/updateStatus.sh")| crontab -
# (crontab -l 2>/dev/null; echo "*/5 * * * * /usr/bin/bash /home/pi/dev/timelapse/scripts/handleSMS.sh")| crontab -

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
read -n 1 -s
sudo reboot
