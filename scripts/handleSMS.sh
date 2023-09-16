pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> handleSMS.sh.out
echo "Starting handleSMS..." >> handleSMS.sh.out
/usr/bin/python3 handleSMS.py

popd
