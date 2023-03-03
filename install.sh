sudo apt-get update
sudo apt-get upgrade
sudo apt-get install git pijuice-base python3-pip -y
sudo apt install -y python3-picamera2 --no-install-recommends

curl -fsSL https://tailscale.com/install.sh | sh

sudo timedatectl set-timezone Pacific/Auckland

mkdir -p dev
cd dev
git clone https://github.com/venari/timelapse.git
cd timelapse
git config pull.rebase false

(crontab -l 2>/dev/null; echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh")| crontab -
(crontab -l 2>/dev/null; echo "@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh")| crontab -


curl -fsSL -o /var/lib/pijuice/pijuice_config.JSON https://raw.githubusercontent.com/venari/timelapse/feature/raspberry-pi-camera-v3/install.sh/pijuice_config.JSON
