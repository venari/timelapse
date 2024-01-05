pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> startup.sh.out
echo "Starting loggingSocketServer..." >> startup.sh.out
/usr/bin/python3 loggingSocketServer.py &>> startup.sh.out

popd
