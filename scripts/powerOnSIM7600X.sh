pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> powerOnSIM7600X.sh.out
echo "powering On SIM7600X..." >> powerOnSIM7600X.sh.out
/usr/bin/python3 powerOnSIM7600X.py

popd
