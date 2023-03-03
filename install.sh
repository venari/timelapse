echo Updating....
sudo apt-get update

echo Upgrading...
sudo apt-get upgrade

echo Installing...
sudo apt-get install git pijuice-base python3-pip -y
sudo apt install -y python3-picamera2 --no-install-recommends
sudo apt-get install vim byobu -y

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
    git checkout feature/raspberry-pi-camera-v3
fi

echo Installing crontab entries...
# (crontab -l 2>/dev/null; echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh")| crontab -
# (crontab -l 2>/dev/null; echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh")| crontab -
(echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh")| crontab -
(echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh")| crontab -

echo Overwriting pijuice config...
sudo mv /var/lib/pijuice/pijuice_config.JSON /var/lib/pijuice/pijuice_config.JSON.bak
sudo curl -fsSL -o /var/lib/pijuice/pijuice_config.JSON https://raw.githubusercontent.com/venari/timelapse/feature/raspberry-pi-camera-v3/pijuice_config.JSON

echo Installing Tailscale...
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up