pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> startup.sh.out
echo "Starting saveTelemetry..." >> startup.sh.out
/usr/bin/python3 saveTelemetry.py 2>> saveTelemetry.sh.out

popd
