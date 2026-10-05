#!/bin/bash

# Ask user if we have a PvPi
read -p "Does this camera have a PvPi BMS board? (y/n)" pvpi

# Ask user if we have a waveshare modem
read -p "Does this camera have a waveshare SIM7600X modem? (y/n)" waveshare

# Avoiding issue #89
read -p "Would you like to update the operating system software using apt-get update/upgrade? (y/n)" updateApt

if [ $updateApt == "y" ]; then

    read -p "Would you like to do a FULL UPGRADE? (y/n)" fullUpgradeApt

    echo Updating....
    sudo apt-get update

    if [ $fullUpgradeApt == "y" ]; then
        echo Performing FULL UPGRADE...
        sudo apt-get full-upgrade -y
    else
        echo Upgrading...
        sudo apt-get upgrade -y
    fi
fi

echo Installing packages...
sudo apt-get install -y git python3-pip
sudo apt-get install -y python3-picamera2 --no-install-recommends
sudo apt-get install -y vim\
                        byobu\
                        python3-pil\
                        python3-RPi.GPIO\
                        python3-serial\

# Pi 5 has ability to power off USB ports, which is useful for power cycling the modem, so we need uhubctl to do that
if sudo raspi-config nonint get_pi_type | grep -q "5"; then
  sudo apt-get install uhubctl -y
fi

# If bullseye - install waveshare-epaper library with pip3
# sudo pip3 install waveshare-epaper
# sudo apt-get install python3-waveshare-epaper -y
if grep -q "bullseye" /etc/os-release; then
    pip3 install waveshare-epaper
    pip3 install suncalc
    pip3 install psutil tabulate
    pip3 install pvpi --ignore-requires-python
else
    pip3 install suncalc --break-system-packages
    pip3 install psutil tabulate --break-system-packages
    pip3 install pvpi --break-system-packages --ignore-requires-python
fi

byobu-enable



# sudo apt-get install locales
# sudo locale-gen en_NZ.UTF-8
# sudo update-locale LANG=en
#sudo dpkg-reconfigure locales


echo Setting timezone...
sudo timedatectl set-timezone Pacific/Auckland

if [ $pvpi == "y" ]; then
    echo "Configuring PvPi serial hardware"
    
    # Detect if we're on a Pi 5
    if sudo raspi-config nonint get_pi_type | grep -q "5"; then
        echo "Pi 5 detected - disabling serial login shell and enabling serial port hardware"
        sudo raspi-config nonint do_serial_hw 0
        sudo raspi-config nonint do_serial_cons 1

        echo "Pi 5 - disabling sudo password"
        sudo raspi-config nonint do_sudo_pass 1

    else
        echo "Pi 0-4 detected - disabling serial login shell and enabling serial port hardware"
        sudo raspi-config nonint do_serial 2
    fi

fi

if [ $waveshare == "y" ]; then
    echo "Installing waveshare modem"
    # Waveshare stuff
    
    # Enable Serial Communication
    sudo raspi-config nonint do_serial 2        # Disable serial login shell and enable serial port hardware

    # Check if folder SIM7600X-4G-HAT-Demo exists:
    if [ ! -d "$HOME/SIM7600X-4G-HAT-Demo" ]; then
        #https://core-electronics.com.au/guides/raspberry-pi/raspberry-pi-4g-gps-hat/
        wget https://www.waveshare.com/w/upload/2/29/SIM7600X-4G-HAT-Demo.7z
        sudo apt-get install p7zip-full
        7z x SIM7600X-4G-HAT-Demo.7z -r -o$HOME
        sudo chmod 777 -R $HOME/SIM7600X-4G-HAT-Demo


        cd $HOME/SIM7600X-4G-HAT-Demo/Raspberry/c/bcm2835
        chmod +x configure && ./configure && sudo make && sudo make install
    fi


    # sed -e '$i \sh $HOME/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init\n' /etc/rc.local
    grep -qxF "sh $HOME/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init" /etc/rc.local || sudo sed -i -e "\$i \sh $HOME/SIM7600X-4G-HAT-Demo/Raspberry/c/sim7600_4G_hat_init\n" /etc/rc.local
    ###################
fi

cd $HOME
# Check if dev folder exists
if [ ! -d "$HOME/dev/timelapse" ]; then
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
    sed -i 's/"modem.type": "SIM7600X"/"modem.type": "thumb"/g' $HOME/dev/timelapse/scripts/config.json
else
    sed -i 's/"modem.type": "thumb"/"modem.type": "SIM7600X"/g' $HOME/dev/timelapse/scripts/config.json
fi

# Update logFilePath to use current user's home directory
sed -i "s|/home/pi/logs|$HOME/logs|g" $HOME/dev/timelapse/scripts/config.json

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


# Clear out any old crontab entries
crontab -r

echo Installing systemd services...
# Copy systemd files and replace 'pi' user with current user and %h with home directory
for file in $HOME/dev/timelapse/systemd/system/*.*; do
    filename=$(basename "$file")
    sed -e "s/User=pi/User=$USER/g" -e "s|%h|$HOME|g" "$file" | sudo tee /etc/systemd/system/"$filename" > /dev/null
done
sudo chmod u+x /etc/systemd/system/enviro*.*
sudo systemctl enable envirocam-logging.service
sudo systemctl enable envirocam-telemetry.service
sudo systemctl enable envirocam-photos.timer
sudo systemctl enable envirocam-upload.timer
sudo systemctl enable envirocam-detect-hang.timer

echo Starting systemd services...
sudo systemctl start envirocam-logging.service
sudo systemctl start envirocam-telemetry.service
sudo systemctl start envirocam-photos.service
sudo systemctl start envirocam-upload.service

sudo systemctl start envirocam-detect-hang.timer


# If not bookworm - don't have epaper library yet
if ! grep -q "bookworm" /etc/os-release; then
    sed -e "s/User=pi/User=$USER/g" -e "s|%h|$HOME|g" $HOME/dev/timelapse/systemd/system/envirocam-status.timer | sudo tee /etc/systemd/system/envirocam-status.timer > /dev/null
    sudo systemctl enable envirocam-status.timer
    sudo systemctl start envirocam-status.timer
fi

# If not waveshare, we can't access SMS messages
if [ $waveshare == "y" ]; then
    sed -e "s/User=pi/User=$USER/g" -e "s|%h|$HOME|g" $HOME/dev/timelapse/systemd/system/envirocam-sms.timer | sudo tee /etc/systemd/system/envirocam-sms.timer > /dev/null
    sudo systemctl enable envirocam-sms.timer
fi


echo Installing Tailscale...
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up

# Query user for hostname, provide a default value
read -p "Current hostname is $(hostname) - would you like to change it?" yn
case $yn in 
    [Yy]* ) echo "Changing hostname";
        read -p "Enter new hostname if desired: " -i envirocam- -e hostname
        echo Setting hostname to $hostname
        sudo hostnamectl set-hostname $hostname;;

    [Nn]* ) echo "Skipping hostname change";;
    * ) echo "Please answer yes or no.";;
esac


echo We need to reboot to clear out cron jobs if we\'re updating an old camera
read -p "Do you want to reboot? (y/n)" rebootNow
if [ "$rebootNow" == "y" ]; then
    echo "Rebooting now..."
    read -n 1 -s
    sudo reboot
else
    echo "Reboot skipped."
fi

