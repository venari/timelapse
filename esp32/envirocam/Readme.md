Documentation here: https://github.com/Xinyuan-LILYGO/LilyGo-Modem-Series/blob/main/docs/en/esp32s3/sim7670g-s3-standard/README.MD

# Windows/WSL, VSCode, PlatformIO

Windows:
```
usbipd list
```

```
C:\Users\LeighHunt>usbipd list
Connected:
BUSID  VID:PID    DEVICE                                                        STATE
3-4    413c:301a  USB Input Device                                              Not shared
3-6    04f2:b829  Integrated Camera, Integrated IR Camera, Camera DFU Device    Not shared
3-9    27c6:659a  Goodix MOC Fingerprint                                        Not shared
3-10   8087:0033  Intel(R) Wireless Bluetooth(R)                                Not shared
4-1    25a4:9311  USB C Video Adaptor                                           Not shared
4-4    045e:07f8  USB Input Device                                              Not shared

Persisted:
GUID                                  DEVICE


C:\Users\LeighHunt>usbipd list
Connected:
BUSID  VID:PID    DEVICE                                                        STATE
3-3    303a:1001  USB Serial Device (COM10), USB JTAG/serial debug unit         Not shared
3-4    413c:301a  USB Input Device                                              Not shared
3-6    04f2:b829  Integrated Camera, Integrated IR Camera, Camera DFU Device    Not shared
3-9    27c6:659a  Goodix MOC Fingerprint                                        Not shared
3-10   8087:0033  Intel(R) Wireless Bluetooth(R)                                Not shared
4-1    25a4:9311  USB C Video Adaptor                                           Not shared
4-4    045e:07f8  USB Input Device                                              Not shared

Persisted:
GUID                                  DEVICE

```

In admin command prompt:
```
Microsoft Windows [Version 10.0.26200.8893]
(c) Microsoft Corporation. All rights reserved.

C:\Windows\System32>usbipd bind --busid 3-3

C:\Windows\System32>usbipd attach --wsl --busid 3-3 --auto-attach
usbipd: info: Using WSL distribution 'Ubuntu' to attach; the device will be available in all WSL 2 distributions.
usbipd: info: Detected networking mode 'nat'.
usbipd: info: Using IP address 172.23.80.1 to reach the host.
usbipd: info: Starting endless attach loop; press Ctrl+C to quit.
WSL Monitoring host 172.23.80.1 for BUSID: 3-3
WSL 2026-08-20 23:25:59 Device 3-3 is now attached.
```

In WSL/Ubuntu:

```
at 14:09:03 ~
✗ dmesg | tail
[ 1813.225907] vhci_hcd vhci_hcd.0: pdev(0) rhport(0) sockfd(3)
[ 1813.225914] vhci_hcd vhci_hcd.0: devid(327684) speed(2) speed_str(full-speed)
[ 1813.225966] vhci_hcd vhci_hcd.0: Device attached
[ 1813.390953] vhci_hcd: vhci_device speed not set
[ 1813.446912] usb 1-1: new full-speed USB device number 2 using vhci_hcd
[ 1813.510937] vhci_hcd: vhci_device speed not set
[ 1813.567064] usb 1-1: SetAddress Request (2) to port 0
[ 1813.598657] cdc_acm 1-1:1.0: ttyACM0: USB ACM device                                   <<<<< ===
[ 1813.598688] usbcore: registered new interface driver cdc_acm
[ 1813.598689] cdc_acm: USB Abstract Control Model driver for USB modems and ISDN adapters
```

## Arduino

- Install [Arduino ESP32](https://docs.espressif.com/projects/arduino-esp32/en/latest/)
- Use **ESP32S3 Dev Module** as board.
- Tweak `utilities.h` to specify board being used (`LILYGO_SIM7670G_S3_STAN`).
- For serial, specify  baud, NL&CR.
- To trigger upload if in deep sleep, press & hold **Boot**. press & release **Reset**, and then release **Boot**.
- Board was attached on COM10 for me on Windows.
- Follow notes in top level [README.md](/README.md) regarding using `uspipd` to share if using `WSL`.

## Telemetry, config and images

Both telemetry and images go over HTTP to the same API the Raspberry Pi units already use (`scripts/uploadPending.py`) - no MQTT. `deviceId` is derived from the efuse MAC, printed on boot, and sent as `SerialNumber`.

- Telemetry is a multipart POST to `<apiUrl>Telemetry`; images to `<apiUrl>Image`.
- Both endpoints return the saved row plus its related `Device` (see `ImageController`/`TelemetryController`), and the device config lives on that `Device` object - `sleepDuringNight`, `daytimeStartsAtH`, `daytimeEndsAtH`, `cameraIntervalS`, `apiUrl`, `hflip`, `vflip` (edit them on a device's page in the API's admin UI). Every successful upload re-reads these and, if anything changed, rewrites `/config.json` on the SD card. That cache is reloaded on every boot, so config survives deep sleep/power loss and applies even on cycles where WiFi doesn't come up.
- Hours are UTC. `sleepDuringNight` makes the device sleep straight through to `daytimeStartsAtH` instead of waking every `cameraIntervalS` seconds overnight.
- `apiUrl` is itself one of the config fields, so pointing a device at a different API deployment is just an admin-side edit - the device picks it up next time it successfully uploads.
- `hflip`/`vflip` mirror/flip the sensor image (`set_hmirror`/`set_vflip`), applied at camera init each boot - useful for correcting a camera that's physically mounted upside down or reversed.
