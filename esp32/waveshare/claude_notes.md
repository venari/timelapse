# Camera init crash fix (Waveshare ESP32-S3-SIM7670G-4G)

## Symptom

`esp_camera_init()` panicked with `Guru Meditation Error: Core 0 panic'ed (LoadProhibited)`,
`EXCVADDR: 0x00000000`, crashing in `ll_cam_set_pin` (called from `cam_init` → `esp_camera_init`
→ `init_camera` in `main/take_picture.c`).

## Root cause

`main/take_picture.c` had `#define BOARD_WROVER_KIT 1` hardcoded, which pulls in the original
ESP32 WROVER-KIT pin map from `main/camera_pinout.h` (`CAM_PIN_VSYNC 25`, `CAM_PIN_HREF 23`,
`CAM_PIN_PCLK 22`). The target chip is an **ESP32-S3**, which does not have GPIO22–25 at all —
indexing the GPIO mux table with those pin numbers returns a bogus/zero register address, and
writing through it null-derefs.

## Fix

- Added two new board pin sets to `main/camera_pinout.h`: `BOARD_WAVESHARE_SIM7670G_V1` and
  `BOARD_WAVESHARE_SIM7670G_V2`, matching the two hardware revisions of this board.
- `main/take_picture.c` now defines `BOARD_WAVESHARE_SIM7670G_V1` instead of `BOARD_WROVER_KIT`.
  Confirmed working on the user's hardware.
- If a V2 board is ever used instead, comment out the V1 `#define` and uncomment the V2 one at
  `main/take_picture.c:60-61`, then rebuild.

### Pin values (from Waveshare docs, see reference below)

| Signal | V1 | V2 |
|---|---|---|
| XCLK | 34 | 39 |
| SIOD | 15 | 15 |
| SIOC | 16 | 16 |
| Y2–Y9 (D0–D7) | 7,8,9,10,11,12,13,14 | 7,8,9,10,11,12,13,14 |
| VSYNC | 36 | 42 |
| HREF | 35 | 41 |
| PCLK | 37 | 46 |
| PWDN / RESET | -1 / -1 | -1 / -1 |

## Reference docs

- Waveshare ESP32-S3-SIM7670G-4G Arduino guide (source of the pin table above):
  https://docs.waveshare.com/ESP32-S3-SIM7670G-4G/Arduino
- Waveshare ESP32-S3-SIM7670G-4G resources index:
  https://docs.waveshare.com/ESP32-S3-SIM7670G-4G/Resources-And-Documents
- `esp32-camera` component source (crash site):
  `managed_components/espressif__esp32-camera/target/esp32s3/ll_cam.c`
