Following entries for crontab:

```
@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh
* * * * * /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadTelemetry.sh 
```
