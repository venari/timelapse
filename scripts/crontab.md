Following entries for crontab:

```bash
@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/saveTelemetry.sh
@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/savePhotos.sh
@reboot sleep 60 && /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh
*/5 * * * * /usr/bin/bash /home/pi/dev/timelapse/scripts/handleSMS.sh
```
