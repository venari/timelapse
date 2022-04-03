# timelapse
A set of tools/scripts to automate the taking and creation of timelapse videos and videos with a Raspberry Pi

# PI Setup

For now, use Buster ratherr than Bullseye - owing to raspicam issues on Pi Zero.

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

git clone https://github.com/venari/timelapse.git

cd timelapse
```


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