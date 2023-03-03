# timelapse
A set of tools/scripts to automate the taking and creation of timelapse videos and videos with a Raspberry Pi

# PI Setup

For now, use Buster rather than Bullseye - owing to raspicam issues on Pi Zero.

Raspberry Pi OS Lite (Legacy) 
Debian version 10 - https://downloads.raspberrypi.org/raspios_oldstable_lite_armhf/images/raspios_oldstable_lite_armhf-2022-01-28/2022-01-28-raspios-buster-armhf-lite.zip


Raspberry Pi OS Lite (32 bit - Pi Zero W)
Debian version 11 (bullseye) - https://downloads.raspberrypi.org/raspios_lite_armhf/images/raspios_lite_armhf-2022-09-26/2022-09-22-raspios-bullseye-armhf-lite.img.xz

Raspberry Pi OS Lite (64 bit - Pi Zero 2 W
Debian version 11 - https://downloads.raspberrypi.org/raspios_lite_arm64/images/raspios_lite_arm64-2022-01-28/2022-01-28-raspios-bullseye-arm64-lite.zip

Burn using Pi Imager.

Set username and password, and authentication methods as desired.

Mount SD card
```
cp ~/wpa_supplicant.conf /Volumes/boot
touch /Volumes/boot/ssh
diskutil unmount /Volumes/boot
```


Turn on and find the pi
Pi Zero W 2:
```
arp -a | grep e4:5f
raspberrypi.lan (192.168.86.37) at e4:5f:1:5a:6e:b3 on en0 ifscope [ethernet]
```

Pi Zero W:
```
arp -a | grep b8:27
raspberrypi.lan (192.168.86.32) at b8:27:eb:94:ac:b1 on en0 ifscope [ethernet]

```

```
<!-- curl -fsSL https://raw.githubusercontent.com/venari/timelapse/main/install.sh | sh -->
curl -fsSL https://raw.githubusercontent.com/venari/timelapse/feature/raspberry-pi-camera-v3/install.sh | sh

```

```
sudo apt-get update
sudo apt-get upgrade
# Note - Camera module v3 won't work until you've done this update, and it will take 5-10 mins

sudo apt-get install git pijuice-base python3-pip -y
sudo apt install -y python3-picamera2 --no-install-recommends

#S Set hostname
<!-- sudo hostname timelapse-pi-zero-w-v1-A -->
sudo hostnamectl set-hostname timelapse-pi-zero-w-v1-A.local
sudo shutdown -r now


# Set timezone (if necessary)
sudo timedatectl set-timezone Pacific/Auckland

mkdir -p dev
cd dev
git clone https://github.com/venari/timelapse.git
git config pull.rebase false
cd timelapse
```

Add scheduled tasks:
```
crontab -e
```
```
@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh
@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadPending.sh 
```

# Raspberry Pi Camera Module v3

- 12MP sensor using IMX708
https://www.raspberrypi.com/documentation/computers/camera_software.html
Camera Module 3 (IMX708)
Ensure software is upgraded (above)

<!-- `camera.lensposition` - 1/distance in metres
- '0' - infinity
- '1': 1m
- `5`: 20cm
- `10`: 10cm -->

`camera.focus_m`: focus in metres

<!-- Add following to /boot/config.txt
```
dtoverlay=imx708
``` -->
# preview image over VNC
https://www.youtube.com/watch?v=dbBWyeHbGs0&ab_channel=WillyKjellstrom

Set screen VNC resolution:
sudo raspi-config
	-> 2 Display Options
		-> D5 VNC Resolution

To disable:
https://help.realvnc.com/hc/en-us/articles/5060068870813-Disabling-direct-capture-on-Raspberry-Pi-using-the-command-line
sudo sed -i '/CaptureTech/d' /root/.vnc/config.d/vncserver-x11
sudo vncserver-x11 -service -reload

Issues in Bullseye on Zero2? https://www.raspberrypi.com/news/bullseye-camera-system/

- related - possibly not - https://github.com/raspberrypi/libcamera-apps/issues/278
- 


# On board timelapse generation

```
ffmpeg -r 30 -f image2 -pattern_type glob -i "./<YYYY-MM-DD>*.jpg" -s 1014x760 -vcodec libx264 <YYYY-MM-DD>.mp4
```

# Other timelapse generation examples

```
ffmpeg -r 30 -f image2 -pattern_type glob -i  "*11_2023-01-03*.jpg" -s 3280x1844 -vcodec libx264 output.mp4
```

## Overlay lable and date/time in images:
```
mkdir -p mod

for filename in *.jpg; do 
    # Escape the colon so it doesn't confuse the text expansion for drawtext
    date_time="$(echo ${filename:3:10} ${filename:14:2}\\:${filename:16:2})"
    label="Forest Lodge Orchard"
    ffmpeg -i $filename -y -vf "drawtext=fontfile=/System/Library/Fonts/Avenir.ttc:text='$label':fontcolor=white:fontsize=90:box=1:boxcolor=black@0.3:boxborderw=5:x=10:y=h-th-10,drawtext=fontfile=/System/Library/Fonts/Avenir.ttc:text='$date_time':fontcolor=white:fontsize=90:box=1:boxcolor=black@0.3:boxborderw=5:x=w-tw-10:y=h-th-10" -hide_banner -loglevel error mod/$filename
done



ffmpeg -r 60 -f image2 -pattern_type glob -i  "mod/*.jpg" -s 3280x1844 -vcodec libx264 text-test.mp4

```

ffmpeg -r 30 -f image2 -pattern_type glob -i  "*11_2023-01-03*.jpg" -s 3280x1844 -vcodec libx264 output.mp4 -vf "drawtext=fontfile=/System/Library/Fonts/Geneva.ttf:text='Stack Overflow':fontcolor=white:fontsize=24:box=1:boxcolor=black@0.5:boxborderw=5:x=(w-text_w)/2:y=(h-text_h)/2,drawtext=fontfile=/System/Library/Fonts/Avenir.ttc:text='%{split(split(filename, '_')[2], \\\\.)[0]} %{split(split(filename, '_')[2], \\\\.)[1]}':fontcolor=white:fontsize=90:box=1:boxcolor=black@0.2:boxborderw=5:x=w-tw-10:y=h-th-10" 

ffmpeg -r 60 -f image2 -pattern_type glob -i  "*11_2023-01-03_120*.jpg" -s 3280x1844 -vcodec libx264 -vf "drawtext=fontfile=/System/Library/Fonts/Geneva.ttf:text='TEST':x=10:y=10:fontcolor=white:fontsize=24" text-test.mp4
ffmpeg -r 60 -f image2 -pattern_type glob -i  "*11_2023-01-03_120*.jpg" -s 3280x1844 -vcodec libx264 -vf "drawtext=fontfile=/System/Library/Fonts/Geneva.ttf:text='Stack Overflow':fontcolor=white:fontsize=24:box=1:boxcolor=black@0.5:boxborderw=5:x=(w-text_w)/2:y=(h-text_h)/2,drawtext=fontfile=/System/Library/Fonts/Avenir.ttc:text='%{split(split(filename, '_')[2], \\\\.)[0]} %{split(split(filename, '_')[2], \\\\.)[1]}':fontcolor=white:fontsize=90:box=1:boxcolor=black@0.2:boxborderw=5:x=w-tw-10:y=h-th-10" text-test.mp4

Avenir.ttc


ffmpeg -i text-test.mp4 -vf "drawtext=fontfile=/System/Library/Fonts/Geneva.ttf:text='Stack Overflow':fontcolor=white:fontsize=24:box=1:boxcolor=black@0.5:boxborderw=5:x=(w-text_w)/2:y=(h-text_h)/2,drawtext=fontfile=/System/Library/Fonts/Avenir.ttc:text='Bottom right text':fontcolor=white:fontsize=60:x=w-tw-10:y=h-th-10" -codec:a copy output.mp4

ffmpeg -i text-test.mp4 -vf "drawtext=fontfile=/System/Library/Fonts/Geneva.ttf:text='Stack Overflow':fontcolor=white:fontsize=24:box=1:boxcolor=black@0.5:boxborderw=5:x=(w-text_w)/2:y=(h-text_h)/2,drawtext=fontfile=/System/Library/Fonts/Avenir.ttc:text='Bottom right text':fontcolor=white:fontsize=90:box=1:boxcolor=black@0.5:boxborderw=5:x=w-tw-10:y=h-th-10" -codec:a copy output.mp4



```

# Video stabilization

https://www.paulirish.com/2021/video-stabilization-with-ffmpeg-and-vidstab/

```
ffmpeg -i unstabilized.mp4 -vf vidstabdetect -f null -
ffmpeg -i unstabilized.mp4 -vf vidstabdetect=shakiness=1:tripod=1 -f null -
ffmpeg -i unstabilized.mp4 -vf vidstabtransform stabilized.mp4
```

Comparison video:
```
# vertically stacked
ffmpeg -i unstabilized.mp4 -i stabilized.mp4  -filter_complex vstack compare-stacked.mp4

# side-by-side
ffmpeg -i unstabilized.mp4 -i stabilized.mp4  -filter_complex hstack compare-sxs.mp4
```

# API Setup
Prerequisites:
- [Dotnet 6](https://dotnet.microsoft.com/en-us/download)
- [EFCore Tooling](https://docs.microsoft.com/en-au/ef/)
```
brew install dotnet-sdk
brew install pgadmin4
dotnet tool install --global dotnet-ef
```

Postgres DB Server:
```

docker pull postgres

mkdir ${HOME}/postgres-data/


docker run -d \
	--name dev-postgres \
	-e POSTGRES_PASSWORD=Pass2020! \
	-v ${HOME}/postgres-data/:/var/lib/postgresql/data \
        -p 5432:5432 \
        postgres


docker exec -it dev-postgres bash        
```

User Secrets:
```
dotnet tool install dotnet-user-secrets
dotnet user-secrets --project timelapse.api init
dotnet user-secrets --project timelapse.api set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;User ID=postgres;Password=Pass2020!;Database=timelapse"
```


When making changes to the objects in the code, add migrations and update the database script:
```
dotnet ef --project timelapse.api migrations add --context "AppDbContext" <migrationName>
dotnet ef --project timelapse.api migrations script -i --context "AppDbContext" -o timelapse.api/Migrations/scripts.sql
```

Updating database:
```
dotnet ef --project timelapse.api database update
```


# Enable Wake Up and set RTC Time

Note - Wake up should be automatically enabled in `coreScript.py`.
```
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░┌───────────────────────── PiJuice CLI ────────────────────────┐░░░░░
░░░░░│  Wakeup Alarm                                                │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  Status: OK                                                  │░░░░░
░░░░░│  UTC Time: Sun 2000-01-02 19:36:02                           │░░░░░
░░░░░│  [ ] Wakeup enabled                                          │░░░░░
░░░░░│  < Set RTC time    >                                         │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  (X) Day                      ( ) Weekday                    │░░░░░
░░░░░│  Day: 0                       [ ] Every day                  │░░░░░
░░░░░│  Hour: 0                      [ ] Every hour                 │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  (X) Minute                   ( ) Minutes period             │░░░░░
░░░░░│  Minute: 0                                                   │░░░░░
░░░░░│  Second: 0                                                   │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  < Set alarm >                                               │░░░░░
░░░░░│  < Back      >                                               │░░░░░
░░░░░│                                                              │░░░░░
░░░░░└──────────────────────────────────────────────────────────────┘░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
〣 10 0:-*               3m 1.00 1.0GHz 367M13% 14G12% 2022-04-05 02:22:05


░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░┌───────────────────────── PiJuice CLI ────────────────────────┐░░░░░
░░░░░│  Wakeup Alarm                                                │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  Status: OK                                                  │░░░░░
░░░░░│  UTC Time: Tue 2022-04-05 01:23:15                           │░░░░░
░░░░░│  [X] Wakeup enabled                                          │░░░░░  <---
░░░░░│  < Set RTC time    >                                         │░░░░░  <---
░░░░░│                                                              │░░░░░
░░░░░│  (X) Day                      ( ) Weekday                    │░░░░░
░░░░░│  Day: 0                       [ ] Every day                  │░░░░░
░░░░░│  Hour: 0                      [ ] Every hour                 │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  ( ) Minute                   (X) Minutes period             │░░░░░  <---
░░░░░│  Minute: 10                                                  │░░░░░  <---
░░░░░│  Second: 0                                                   │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  < Set alarm >                                               │░░░░░
░░░░░│  < Back      >                                               │░░░░░
░░░░░│                                                              │░░░░░
░░░░░└──────────────────────────────────────────────────────────────┘░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
〣 10 0:-*               3m 1.00 1.0GHz 367M13% 14G12% 2022-04-05 02:23:15

```

# Battery min sleep/wake up levels:

```
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░┌───────────────────────── PiJuice CLI ────────────────────────┐░░░░░░░░░░░
░░░░░░░░░░│  System Task                                                 │░░░░░░░░░░░
░░░░░░░░░░│                                                              │░░░░░░░░░░░
░░░░░░░░░░│  [X] System task enabled                                     │░░░░░░░░░░░
░░░░░░░░░░│                                                              │░░░░░░░░░░░
░░░░░░░░░░│  [ ] Watchdog            Expire period          [ ] Restore  │░░░░░░░░░░░
░░░░░░░░░░│                          [minutes]: 4                        │░░░░░░░░░░░
░░░░░░░░░░│                                                              │░░░░░░░░░░░
░░░░░░░░░░│  [X] Wakeup on charge    Trigger level [%]: 20  [X] Restore  │░░░░░░░░░░░
░░░░░░░░░░│                                                              │░░░░░░░░░░░
░░░░░░░░░░│  [X] Min charge          Threshold [%]: 10                   │░░░░░░░░░░░
░░░░░░░░░░│                                                              │░░░░░░░░░░░
░░░░░░░░░░│  [ ] Min battery voltage 3.3                                 │░░░░░░░░░░░
░░░░░░░░░░│                                                              │░░░░░░░░░░░
░░░░░░░░░░│  [X] Software Halt Power Delay period [seconds]: 20          │░░░░░░░░░░░
░░░░░░░░░░│      Off                                                     │░░░░░░░░░░░
░░░░░░░░░░│                                                              │░░░░░░░░░░░
░░░░░░░░░░│  < Refresh        >                                          │░░░░░░░░░░░
░░░░░░░░░░│  < Apply settings >                                          │░░░░░░░░░░░
░░░░░░░░░░└──────────────────────────────────────────────────────────────┘░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
```

# System Events

```
[X] Low Charge : <SYS_FUNC_HALT_POW_OFF>

```

# Battery Profiles

Ali Express, likely-not-really 10,000mAh battery...

```

░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░┌───────────────────────── PiJuice CLI ────────────────────────┐░░░░░
░░░░░│  Battery settings                                            │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  Status: Custom profile by: HOST                             │░░░░░
░░░░░│  < Profile: CUSTOM       >                                   │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  [X] Custom                                                  │░░░░░  <-- Set to Custom
░░░░░│  Chemistry:                LIPO                              │░░░░░  
░░░░░│  Capacity [mAh]:           10000                             │░░░░░  <-- Set to 10000
░░░░░│  Charge current [mA]:      1225                              │░░░░░  <-- Set to 1250 or 1000
░░░░░│  Termination current [mA]: 50                                │░░░░░
░░░░░│  Regulation voltage [mV]:  4180                              │░░░░░
░░░░░│  Cutoff voltage [mV]:      3000                              │░░░░░
░░░░░│  Cold temperature [C]:     0                                 │░░░░░
░░░░░│  Cool temperature [C]:     2                                 │░░░░░
░░░░░│  Warm temperature [C]:     49                                │░░░░░
░░░░░│  Hot temperature [C]:      65                                │░░░░░
░░░░░│  NTC B constant [1k]:      3450                              │░░░░░
░░░░░│  NTC resistance [ohm]:     10000                             │░░░░░
░░░░░│  OCV10 [mV]:               3743                              │░░░░░
░░░░░│  OCV50 [mV]:               3933                              │░░░░░
░░░░░│  OCV90 [mV]:               4057                              │░░░░░
░░░░░│  R10 [mOhm]:               135.0                             │░░░░░
░░░░░│  R50 [mOhm]:               133.0                             │░░░░░
░░░░░│  R90 [mOhm]:               133.0                             │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  < Temperature sense: ON_BOARD    >                          │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  < Rsoc estimation: AUTO_DETECT   >                          │░░░░░
░░░░░│                                                              │░░░░░
░░░░░│  < Refresh        >                                          │░░░░░
░░░░░│  < Apply settings >                                          │░░░░░
░░░░░│  < Back           >                                          │░░░░░
░░░░░└──────────────────────────────────────────────────────────────┘░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░

```


# Battery notes

Naccon 3.7V 26650HP 5000mAh
On continuously
Charge from wall charger about 5 hours
Discharge 95 -> 5% about 12 hours.
Discharge 4 -> 1$ about 10 hours.


# Tailscale setup (optional)

Using [tailscale](https://github.com/tailscale/tailscale) to manage updates to pi's:

From https://tailscale.com/download/linux/rpi-bullseye:

```
curl -fsSL https://tailscale.com/install.sh | sh
```

```
sudo tailscale up
```




# Credits

3D models

- Raspberry Pi Camera Module v3 STL files: https://www.printables.com/model/368779-raspberry-pi-camera-module-3-v3/