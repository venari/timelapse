pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> indicateStatus.sh.out
echo "Starting indicateStatus..." >> indicateStatus.sh.out
/usr/bin/python3 indicateStatus.py

popd
