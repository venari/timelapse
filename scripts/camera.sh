#!/bin/bash
DATE=$(date +"%Y-%m-%d_%H%M")
LABEL=$(date +"%d/%m/%Y %H:%M")

# 1 photo every 10 seconds
#raspistill --nopreview --timeout 55000 --timelapse 10000 -o /home/pi/timelapse/$DATE.%04d.jpg
#raspistill -vf -hf --nopreview --timeout 55000 --timelapse 10000 -o /home/pi/timelapse/$DATE.%04d.jpg
#raspistill -vf -hf --nopreview --timeout 45000 --timelapse 10000 -o /home/pi/timelapse/$DATE.%04d.jpg
#raspistill -vf -hf --nopreview --timeout 40000 --timelapse 10000 -o /home/pi/timelapse/$DATE.%04d.jpg


# 1 picture/min
# raspistill --nopreview -o /home/pi/timelapse/$DATE.jpg

raspistill -vf -hf --nopreview -o /home/pi/timelapse/$DATE.jpg




#raspistill --annotate 1 "$LABEL"  --nopreview -o /home/pi/timelapse/$DATE.annotate.jpg
#echo $LABEL
#echo "$LABEL"
#echo ''"$LABEL"''
#echo '123'"$LABEL"'456'

#raspistill --nopreview --awb cloud -o /home/pi/timelapse/$DATE.cloud.jpg
#raspistill --nopreview --awb sun -o /home/pi/timelapse/$DATE.sun.jpg
#raspistill --nopreview --awb horizon -o /home/pi/timelapse/$DATE.horizon.jpg
#raspistill --annotate 'Kāinga Ora TEST' --nopreview -o /home/pi/timelapse/$DATE.annotate1.jpg
#raspistill --annotate 12 --nopreview -o /home/pi/timelapse/$DATE.annotate3.jpg
#raspistill -o /home/pi/timelapse/$DATE.jpg


