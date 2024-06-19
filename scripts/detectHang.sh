pushd /home/pi/dev/timelapse/scripts
set -x

# echo `date` >> startup.sh.out
# echo "Starting detectHang..." >> startup.sh.out

/usr/bin/python3 detectHang.py &>> startup.sh.out

popd