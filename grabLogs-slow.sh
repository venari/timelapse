#!/bin/bash

#Display usage if no arguments are provided
if [ $# -eq 0 ]; then
    echo "Usage: $0 <camera_name>"
    echo "Example: $0 envirocam-a"
    exit 1
fi

# Take first argument as camera name
CAMERA_NAME=$1
KEY_PATH=~/.ssh/id_macbook_air_rsa
TIMEOUT=15
RETRIES=240
LIMIT=100

cd ./logs/$CAMERA_NAME
if [ $? -ne 0 ]; then
    echo "Error: Could not change to logs directory for camera $CAMERA_NAME"
    exit 1
fi


TIMESTAMP=$(date +"%Y%m%d%H%M%S")

# Grab Pijuice event Log

echo "Grabbing Pijuice event log for camera $CAMERA_NAME..."
ssh -o ConnectTimeout=$TIMEOUT -o ConnectionAttempts=$RETRIES -i $KEY_PATH pi@$CAMERA_NAME 'python3 /usr/bin/pijuice_log.py' > pijuice.$TIMESTAMP.log

if [ $? -ne 0 ]; then
    echo "Error: Could not grab Pijuice event log from camera $CAMERA_NAME"
fi

# Grab journalctl log
echo "Grabbing journalctl log for camera $CAMERA_NAME..."
ssh -o ConnectTimeout=$TIMEOUT -o ConnectionAttempts=$RETRIES -i $KEY_PATH pi@$CAMERA_NAME 'journalctl --since today' > journalctl.$TIMESTAMP.log

# Grab log files
echo "Grabbing log files for camera $CAMERA_NAME..."
scp -o ConnectTimeout=$TIMEOUT -o ConnectionAttempts=$RETRIES -l $LIMIT -i $KEY_PATH pi@$CAMERA_NAME:/home/pi/logs/*.* .