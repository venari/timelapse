Following entries for crontab:

```
@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh

#*/10 * * * * /usr/bin/python3 /home/pi/dev/timelapse/scripts/log.py
* * * * * /usr/bin/python3 /home/pi/dev/timelapse/scripts/log.py
```
