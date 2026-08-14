Documentation here: https://github.com/Xinyuan-LILYGO/LilyGo-Modem-Series/blob/main/docs/en/esp32s3/sim7670g-s3-standard/README.MD

# Windows

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
