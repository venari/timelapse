pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> startup.sh.out
echo "Starting savePhotos..." >> startup.sh.out
LIBCAMERA_LOG_LEVELS=WARN /usr/bin/python3 savePhotos.py 2>> savePhotos.sh.out

popd
