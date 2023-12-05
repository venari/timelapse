pushd /home/pi/dev/timelapse/scripts
set -x
echo `date` >> startup.sh.out
echo "Uploading telemetry and images..." >> startup.sh.out
/usr/bin/python3 uploadPending.py 2>> uploadPending.sh.out

popd
