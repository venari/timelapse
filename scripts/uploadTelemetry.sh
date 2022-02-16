pushd /home/pi/dev/timelapse/scripts
set -x
echo `date` >> startup.sh.out
echo "Uploading telemetry..." >> startup.sh.out
echo >> uploadTelemetry.py.out
date >> uploadTelemetry.py.out
/usr/bin/python3 uploadTelemetry.py >> uploadTelemetry.py.out

popd
