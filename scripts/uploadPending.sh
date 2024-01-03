pushd /home/pi/dev/timelapse/scripts
set -x
echo `date` >> startup.sh.out
echo "Uploading telemetry and images..." >> startup.sh.out
/usr/bin/python3 uploadPending.py &>> startup.sh.out

popd
