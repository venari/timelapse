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

- Telemetry is published (retained) to MQTT topic `camera/<deviceId>/telemetry`. `deviceId` is derived from the efuse MAC, printed on boot.
- Device config is read from MQTT topic `camera/<deviceId>/config` (also retained - publish it once and every future boot/reconnect picks it up). Sent as JSON, any subset of:
  ```json
  {
    "sleep_during_night": true,
    "daytime_starts_at_h": 7,
    "daytime_ends_at_h": 17,
    "camera.interval": 300,
    "apiUrl": "https://timelapse-dev.azurewebsites.net/api/"
  }
  ```
  Hours are UTC. Received config is cached to `/config.json` on the SD card and reloaded on every boot, so it survives deep sleep/power loss and applies even on cycles where WiFi doesn't come up. `sleep_during_night` makes the device sleep straight through to `daytime_starts_at_h` instead of waking every `camera.interval` seconds overnight.
- Images are uploaded over HTTP (not MQTT - not a good fit for large binary payloads, especially once this moves to cellular) as a multipart POST to `<apiUrl>Image`, matching the endpoint the Raspberry Pi units already post to via `scripts/uploadPending.py`.
