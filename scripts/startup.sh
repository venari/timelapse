pushd /home/pi/dev/timelapse/scripts
set -x
# rm *.out

# Give up to 1 minutes to get network - if it doesn't by then, just fall through so we can at least take the picture.

tries=0
while ! ping -c 1 -W 1 venari.co.nz; do
    echo `date` >> startup.sh.out
    echo "Waiting for venari.co.nz - network interface might be down..." >> startup.sh.out
    sleep 1
    tries=$(( $tries + 1 ))
    if [[ $tries -gt 30 ]]; then
        echo "Giving up waiting" >> startup.sh.out
        break
    fi
done
echo `date` >> startup.sh.out
echo "network interface either up or we gave up" >> startup.sh.out
echo >> coreScript.py.out
date >> coreScript.py.out
# /usr/bin/python3 coreScript.py 2& >> coreScript.py.out
/usr/bin/python3 coreScript.py >> coreScript.py.out
#echo >> gitPull.log
#date >> gitPull.log
#echo "> git pull" >> gitPull.log
#/usr/bin/git pull >> gitPull.log

echo `date` >> startup.sh.out
echo "startup script finished" >> startup.sh.out
popd
