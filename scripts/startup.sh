pushd /home/pi/dev/timelapse/scripts
set -x
# rm *.out
echo >> coreScript.py.out
date >> coreScript.py.out
/usr/bin/python3 coreScript.py 2& >> coreScript.py.out
echo >> gitPull.log
date >> gitPull.log
echo "> git pull" >> gitPull.log
/usr/bin/git pull >> gitPull.log
popd
