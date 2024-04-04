#!/bin/bash

# Ask user if we have a waveshare modem
read -p "Does this camera have a waveshare SIM7600X modem? (y/n)" waveshare

read -p "Would you like to update the operating system software using apt-get update/upgrade? (y/n)" updateApt

if [ $updateApt == "y" ]; then
    echo Updating....
    sudo apt-get update

    echo Upgrading...
    sudo apt-get upgrade -y
fi

echo Installing packages...
sudo apt-get install -y git pijuice-base python3-pip
sudo apt-get install -y python3-picamera2 --no-install-recommends
sudo apt-get install -y vim\
                        byobu\
                        python3-pil\
                        python3-RPi.GPIO\
                        python3-serial\

# If not bookworm - install waveshare-epaper library with pip3
# sudo pip3 install waveshare-epaper
# sudo apt-get install python3-waveshare-epaper -y
if ! grep -q "bookworm" /etc/os-release; then
    pip3 install waveshare-epaper
fi

byobu-enable

echo Setting timezone...
sudo timedatectl set-timezone Pacific/Auckland


if [ $waveshare == "y" ]; then
    echo "Installing waveshare modem"
    # Waveshare stuff
    
    # Enable Serial Communication
    sudo raspi-config nonint do_serial 2        # Disable serial login shell and enable serial port hardware

    # Check if folder SIM7600X-4G-HAT-Demo exists:
    if [ ! -d "/home/pi/SIM7600X-4G-HAT-Demo" ]; then
        #https://core-electronics.com.au/guides/raspberry-pi/raspberry-pi-4g-gps-hat/
        wget https://www.waveshare.com/w/upload/2/29/SIM7600X-4G-HAT-Demo.7z
        sudo apt-get install p7zip-full
        7z x SIM7600X-4G-HAT-Demo.7z -r -o/home/pi
        sudo chmod 777 -R /home/pi/SIM7600X-4G-HAT-Demo


        cd /home/pi/SIM7600X-4G-HAT-Demo/Raspberry/c/bcm2835
        chmod +x configure && ./configure && sudo make && sudo make install
    fi


    # sed -e '$i \sh /home/pi/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init\n' /etc/rc.local
    grep -qxF 'sh /home/pi/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init' /etc/rc.local || sudo sed -i -e '$i \sh /home/pi/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init\n' /etc/rc.local
    ###################
fi

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
    git checkout main
else
    echo Updating repo...
    cd dev/timelapse
    # git checkout development
    git fetch
    git stash
    git checkout main
    git pull
    git stash pop
fi

# If using thumbdrive, not waveshare modem, update 'modem.type' in dev/timelapse/scripts/config.json to 'thumb'
if [ $waveshare == "n" ]; then
    sed -i 's/"modem.type": "SIM7600X"/"modem.type": "thumb"/g' /home/pi/dev/timelapse/scripts/config.json
else
    sed -i 's/"modem.type": "thumb"/"modem.type": "SIM7600X"/g' /home/pi/dev/timelapse/scripts/config.json
fi

echo Checking RTC module is enabled in config.txt
if [ -e /boot/firmware/config.txt ] ; then
  FIRMWARE=/firmware
else
  FIRMWARE=
fi
CONFIG=/boot${FIRMWARE}/config.txt

grep -qxF 'dtoverlay=i2c-rtc,ds1307=1' $CONFIG || echo 'dtoverlay=i2c-rtc,ds1307=1' | sudo tee -a $CONFIG
grep -qxF 'dtparam=i2c_arm=on' $CONFIG || echo 'dtparam=i2c_arm=on' | sudo tee -a $CONFIG

echo Checking static domain_name_servers entry etc/dhcpcd.conf
grep -qxF 'static domain_name_servers=8.8.4.4 8.8.8.8' /etc/dhcpcd.conf || echo 'static domain_name_servers=8.8.4.4 8.8.8.8' | sudo tee -a /etc/dhcpcd.conf




echo Installing systemd services...
sudo cp /home/pi/systemd/system/*.* /etc/systemd/system/
sudo systemctl enable envirocam-logging.service
sudo systemctl enable envirocam-telemetry.service
sudo systemctl enable envirocam-photos.service
sudo systemctl enable envirocam-upload.service
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
