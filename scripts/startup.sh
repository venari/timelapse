pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> startup.sh.out
echo "Starting coreScript..." >> startup.sh.out
# /usr/bin/python3 coreScript.py 2& >> coreScript.py.out
# /usr/bin/python3 coreScript.py >> coreScript.py.out
/usr/bin/python3 coreScript.py

popd
