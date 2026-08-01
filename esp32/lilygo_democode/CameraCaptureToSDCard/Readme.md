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
