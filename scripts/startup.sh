pushd /home/pi/dev/timelapse/scripts
set -x
rm *.err
rm *.out
/usr/bin/python3 test.py > test.py.out 2> test.py.err
/usr/bin/python3 log.py > log.py.out 2> log.py.err
date >> gitPull.log
echo "> git pull" >> gitPull.log
/usr/bin/git pull >> gitPull.log
echo >> gitPull.log
popd
