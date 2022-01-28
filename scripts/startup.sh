pushd /home/pi/dev/timelapse/scripts
/usr/bin/python3 test.py
/usr/bin/python3 log.py
/usr/bin/git pull >> gitPull.log
popd
