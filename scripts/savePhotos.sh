pushd /home/pi/dev/timelapse/scripts
set -x

echo `date` >> savePhotos.sh.out
echo "Starting savePhotos..." >> savePhotos.sh.out
/usr/bin/python3 savePhotos.py

popd
