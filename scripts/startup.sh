pushd /home/pi/dev/timelapse/scripts
/usr/bin/python3 test.py > test.py.out 2>test.py.err
/usr/bin/python3 log.py > log.py.out 2>log.py.err
/usr/bin/git pull >> gitPull.log
popd
