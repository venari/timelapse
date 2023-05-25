#!/bin/bash
pushd /home/pi/dev/timelapse/scripts
set -x

/usr/bin/python3 indicateStatus.py

popd
