pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> saveTelemetry.sh.out
echo "Starting saveTelemetry..." >> saveTelemetry.sh.out
/usr/bin/python3 saveTelemetry.py

popd
