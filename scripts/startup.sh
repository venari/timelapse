pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> startup.sh.out
echo >> coreScript.py.out
date >> coreScript.py.out
# /usr/bin/python3 coreScript.py 2& >> coreScript.py.out
/usr/bin/python3 coreScript.py >> coreScript.py.out

echo `date` >> startup.sh.out
echo "startup script finished" >> startup.sh.out
popd
