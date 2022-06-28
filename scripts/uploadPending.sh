pushd /home/pi/dev/timelapse/scripts
set -x
echo `date` >> startup.sh.out
echo "Uploading telemetry and images..." >> startup.sh.out
echo >> uploadPending.py.out
date >> uploadPending.py.out
/usr/bin/python3 uploadPending.py >> uploadPending.py.out

popd
