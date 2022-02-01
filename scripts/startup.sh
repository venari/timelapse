pushd /home/pi/dev/timelapse/scripts
set -x
rm *.out
/usr/bin/python3 coreScript.py 2&> coreScript.py.out
echo >> gitPull.log
date >> gitPull.log
echo "> git pull" >> gitPull.log
/usr/bin/git pull 2&>> gitPull.log
popd
