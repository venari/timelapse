# timelapse
A set of tools/scripts to automate the taking and creation of timelapse videos and videos with a Raspberry Pi

# PI Setup

For now, use Buster rather than Bullseye - owing to raspicam issues on Pi Zero.

Raspberry Pi OS Lite (Legacy) 
Debian version 10 - https://downloads.raspberrypi.org/raspios_oldstable_lite_armhf/images/raspios_oldstable_lite_armhf-2022-01-28/2022-01-28-raspios-buster-armhf-lite.zip


Raspberry Pi OS Lite (32 bit - Pi Zero W)
Debian version 11 - https://downloads.raspberrypi.org/raspios_oldstable_lite_armhf/images/raspios_oldstable_lite_armhf-2022-01-28/2022-01-28-raspios-buster-armhf-lite.zip

Raspberry Pi OS Lite (64 bit - Pi Zero 2 W
Debian version 11 - https://downloads.raspberrypi.org/raspios_lite_arm64/images/raspios_lite_arm64-2022-01-28/2022-01-28-raspios-bullseye-arm64-lite.zip

Burn using Etcher.

Mount SD card
```
cp ~/wpa_supplicant.conf /Volumes/boot
touch /Volumes/boot/ssh
diskutil unmount /Volumes/boot
```

Turn on and find the pi
```
arp -a | grep e4:5f
raspberrypi.lan (192.168.86.37) at e4:5f:1:5a:6e:b3 on en0 ifscope [ethernet]

arp -a | grep b8:27
raspberrypi.lan (192.168.86.32) at b8:27:eb:94:ac:b1 on en0 ifscope [ethernet]

```

```
sudo apt-get update
sudo apt-get upgrade

# Enable camera interface
sudo raspi-config nonint do_camera 0

sudo apt-get install git pijuice-base gphoto2 python3-pip -y

pip3 install picamera

# Set timezone
sudo timedatectl set-timezone Pacific/Auckland

mkdir -p dev
cd dev
git clone https://github.com/venari/timelapse.git
cd timelapse
```

# Camera settings
Ensure gpu_mem is set to at least 144MB - sudo raspi-config, 4) Performance Options -> P2) GPU Memory -> 144
```
start_x=1
gpu_mem=144
```

Add scheduled tasks:
```
crontab -e
```
```
@reboot /usr/bin/bash /home/pi/dev/timelapse/scripts/startup.sh
* * * * * /usr/bin/bash /home/pi/dev/timelapse/scripts/uploadTelemetry.sh 
```

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



# Balena notes

https://www.balena.io/docs/learn/getting-started/raspberrypi3/nodejs/

https://github.com/balena-os/wifi-connect/blob/master/README.md

https://www.balena.io/blog/how-to-protect-your-device-with-pijuice-and-balenacloud/

https://www.balena.io/docs/learn/develop/dockerfile/
https://www.balena.io/docs/reference/base-images/base-images/
https://www.balena.io/docs/reference/base-images/devicetypes/

## timelapse_zero2 fleet:
https://dashboard.balena-cloud.com/fleets/1887284

```
 timelapse git:(feature/balena) ✗ balena push gh_leighghunt/timelapse_zero2
--------------------------------------------------------------------------------
[Warn] Node.js version "14.19.3" does not satisfy requirement ">=12.8.0 <13.0.0"
[Warn] This may cause unexpected behavior.
--------------------------------------------------------------------------------
[Info]          Starting build for timelapse_zero2, user gh_leighghunt
[Info]          Dashboard link: https://dashboard.balena-cloud.com/apps/1887284/devices
[Info]          Building on arm01
[Info]          No suitable previous release for caching, skipping cache pull
[Info]          Uploading images
[Success]       Successfully uploaded images
[Info]          Built on arm01
[Success]       Release successfully created!
[Info]          Release: 027164029471c54c61694409080f6895 (id: 2182416)
[Info]          ┌──────────────┬────────────┬────────────┐
[Info]          │ Service      │ Image Size │ Build Time │
[Info]          ├──────────────┼────────────┼────────────┤
[Info]          │ wifi-connect │ 13.21 MB   │ 4 seconds  │
[Info]          └──────────────┴────────────┴────────────┘
[Info]          Build finished in 1 minute, 27 seconds
			    \
			     \
			      \\
			       \\
			        >\/7
			    _.-(6'  \
			   (=___._/` \
			        )  \ |
			       /   / |
			      /    > /
			     j    < _\
			 _.-' :      ``.
			 \ r=._\        `.
			<`\\_  \         .`-.
			 \ r-7  `-. ._  ' .  `\
			  \`,      `-.`7  7)   )
			   \/         \|  \'  / `-._
			              ||    .'
			               \\  (
			                >\  >
			            ,.-' >.'
			           <.'_.''
			             <'
```

## Belana-cam
https://github.com/balenalabs/balena-cam

Also https://forums.balena.io/t/camera-not-working-on-zero2-balena-cam/353569/12?u=leighghunt

    | Key                                  | Value
    |--------------------------------------|----------
    |**`BALENA_HOST_CONFIG_gpu_mem`**      | **`64`**
    |**`BALENA_HOST_CONFIG_start_x`**      | **`1`**


Orginal docs - superceeded by the above

- Set these variables in the `Configuration` side tab under "fleets".
  - `BALENA_HOST_CONFIG_start_x` = `1`
  - Set all the following `gpu_mem` variables so your Pi can autoselect how much memory to allocate for hardware accelerated graphics, based on how much RAM it has available

    | Key                                  | Value
    |--------------------------------------|----------
    |**`BALENA_HOST_CONFIG_gpu_mem_256`**  | **`192`**
    |**`BALENA_HOST_CONFIG_gpu_mem_512`**  | **`256`**
    |**`BALENA_HOST_CONFIG_gpu_mem_1024`** | **`448`**
- Using [Balena CLI](https://www.balena.io/docs/reference/cli/), push the code with `balena push <fleet-name>`.
- See the magic happening, your device is getting updated 🌟Over-The-Air🌟!
- In order for your device to be accessible over the internet, toggle the switch called `PUBLIC DEVICE URL`.
- Once your device finishes updating, you can watch the live feed by visiting your device's public URL.

### Password Protect your balenaCam device

To protect your balenaCam devices using a username and a password set the following environment variables.

| Key            | Value
|----------------|---------------------------
|**`username`**  | **`yourUserNameGoesHere`**
|**`password`**  | **`yourPasswordGoesHere`**

💡 **Tips:** 💡 
* You can set them as [fleet environment variables](https://www.balena.io/docs/learn/manage/serv-vars/#fleet-environment-and-service-variables) and every new balenaCam device you add will be password protected.
* You can set them as [device environment variables](https://www.balena.io/docs/learn/manage/serv-vars/#device-environment-and-service-variables) and the username and password will be different on each device.

### Optional Settings

- To rotate the camera feed by 180 degrees, add a **device variable**: `rotation` = `1` (More information about this on the [docs](https://www.balena.io/docs/learn/manage/serv-vars/)).
- To suppress any warnings, add a **device variable**: `PYTHONWARNINGS` = `ignore`
