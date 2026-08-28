/**
 * @file      CameraCaptureToSDCard.ino
 * @author    Lewis He (lewishe@outlook.com)
 * @license   MIT
 * @copyright Copyright (c) 2025  ShenZhen XinYuan Electronic Technology Co., Ltd
 * @date      2025-07-13
 * @note      Sketch is only suitable for LilyGo-A7670X-S3 version,Other versions are not supported
 *      This sketch is only applicable to the
 *      1. T-A7670X-S3-Standard
 *      2. T-SIM7000G-S3-Standard
 *      3. T-SIM7080G-S3-Standard
 *      4. T-SIM7670G-S3-Standard
 *      5. T-SIM7600X-S3-Standard
 *      Other models are not supported
 *
 * */
#include <esp_camera.h>
#include <esp_log.h>
#include <esp_heap_caps.h>
#include <esp_system.h>
#include <math.h>
#include <Wire.h>
#include <FS.h>
#include <SD.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <WebServer.h>
#include <NimBLEDevice.h>
#include <time.h>
#include <ArduinoJson.h>
#include <vector>
#include <algorithm>
#include <cstdarg>

#include "utilities.h"
#include "secrets.h"

// TINY_GSM_MODEM_* is set by utilities.h based on the board #define above - must come after it.
// Used here purely for the SIM7670G's onboard GNSS receiver (see updateGeoLocationIfDue()) - this
// sketch still uploads over the ESP32's own WiFi radio, same as ever, not over cellular.
#include <TinyGsmClient.h>

#if !defined(LILYGO_SIM7000G_S3_STAN) && !defined(LILYGO_SIM7080G_S3_STAN) \
    && !defined(LILYGO_SIM7670G_S3_STAN) && !defined(LILYGO_A7670X_S3_STAN)  && !defined(LILYGO_SIM7600X_S3_STAN)
#error "This sketch is only applicable to the T-A7670X-S3-Standard,T-SIM7000G-S3-Standard,T-SIM7080G-S3-Standard,T-SIM7670G-S3-Standard,T-SIM7600X-S3-Standard"
#endif

#define ENABLE_BATTERY_MON

#include <Wire.h>

#define uS_TO_S_FACTOR          1000000ULL  /* Conversion factor for micro seconds to seconds */
// #define TIME_TO_SLEEP           180          /* Time ESP32 will go to sleep (in seconds) */
// #define TIME_TO_SLEEP           10          /* Time ESP32 will go to sleep (in seconds) */
#define TIME_TO_SLEEP           60          /* Time ESP32 will go to sleep (in seconds) */
#define BATTERY_VOLTAGE_LOW     2900        // Set low voltage to sleep mode - ~5% per BATTERY_CURVE, checked
                                             // before the camera/modem draw anything, so this is close to resting
                                             // voltage rather than under load

#define WIFI_CONNECT_TIMEOUT_MS 15000       // Give up on WiFi after this long
#define NTP_SERVER              "pool.ntp.org"
#define GMT_OFFSET_SEC           0          // Adjust for local timezone
#define DAY_LIGHT_OFFSET_SEC     0          // Adjust for daylight saving

#define LAST_SYNC_FILE           "/last_sync.txt"

// Presence (contents don't matter) means "sync/upload again next boot, regardless of
// autoSyncPeriodS" - see the "5x expected" checks in uploadPendingTelemetry()/
// uploadPendingImages() and how this gets set/cleared in setup().
#define FORCE_SYNC_FILE          "/force_sync.flag"

#define TELEMETRY_DIR            "/telemetry/pending"   // Uploaded telemetry is deleted, not archived - see uploadPendingTelemetry()
#define TELEMETRY_HOLDING_DIR    "/telemetry/holding"   // Records the API rejected outright (e.g. 400) land here instead - kept for inspection, never retried

#define CAMERA_DIR               "/camera/pending"      // Uploaded images are deleted, not archived - see uploadPendingImages()

#define COUNTS_FILE              "/counts.txt"  // Running pending/uploaded totals, kept up to date incrementally
                                                 // instead of scanning directories (which gets slow with thousands of files)

#define CONFIG_FILE              "/config.json" // Local cache of the device config the API hands back on every upload

#define LOG_DIR                  "/logs"
#define LOG_FILE                 "/logs/envirocam.log"  // Active day's log - see rotateLogIfNeeded()
#define LOG_RETENTION_DAYS       30                      // Rotated logs older than this get deleted

// A temporary local WiFi hotspot + status/photo web page - see runSetupApWindow() - so an
// installer can confirm the camera's working right after power-on with zero cellular/internet
// WiFi needed, which is the point: this is for exactly the low/no-connectivity sites where
// DeviceConfig::supportMode (API-driven, needs a working uplink) can't help. bootCount is RTC
// memory - survives a deep-sleep wake but not a real power cycle - so this only ever runs across
// a fresh install's first few boots, never costing battery on a camera that's already deployed.
#define SETUP_AP_MAX_BOOT_COUNT  3
#define SETUP_AP_NEXT_BOOT_DELAY_S 1   // See loop() - replaces the normal cameraIntervalS wait while
                                        // still inside the setup window's boot budget above, so the
                                        // next boot's setup window is available right away rather
                                        // than waiting out a full interval on top of the one that
                                        // just ran for minutes on its own
#define SETUP_AP_WINDOW_MS       (3UL * 60 * 1000)   // How long the hotspot stays up before continuing on to deep sleep
#define SETUP_AP_STREAM_FRAMESIZE       FRAMESIZE_SVGA  // Much lighter than the QSXGA capture - plenty to check framing/focus
#define SETUP_AP_STREAM_FRAME_INTERVAL_MS (300UL)      // Paces "/stream" frames - keeps the softAP link comfortable
#define SETUP_AP_SSID_PREFIX     "EnviroCam-"
#define SETUP_AP_PASSWORD        "envirocam"          // Shared across all units - keep it out of range of anyone but the install crew

// Defaults used until a config.json exists (i.e. before the API has ever handed back a
// Device row - see applyDeviceConfigFromApiResponse). Names match the Device model's JSON
// property names on the API side (System.Text.Json defaults to camelCase).
//
// daytimeStartsAtH/daytimeEndsAtH are *local* hours (see utcOffsetMinutes below) marking the
// working day - purely a fixed schedule for computeSleepSeconds()'s sleepDuringNight feature
// (activity/uploads are unlikely outside it), unrelated to actual sunlight. Long-exposure
// switching uses real sunrise/sunset instead - see isNightForExposure().
#define DEFAULT_SLEEP_DURING_NIGHT   false
#define DEFAULT_DAYTIME_STARTS_AT_H  7
#define DEFAULT_DAYTIME_ENDS_AT_H    17
#define DEFAULT_CAMERA_INTERVAL_S    60

// With sleepDuringNight enabled, computeSleepSeconds() would otherwise sleep straight through from
// daytimeEndsAtH to daytimeStartsAtH in one shot. Instead we cap each overnight sleep at this, so
// the unit still wakes ~hourly to check in (telemetry/config/OTA) while skipping the captures it
// would normally take at cameraIntervalS. The final sleep of the night is still shortened to land
// on daytimeStartsAtH exactly.
#define NIGHT_CHECKIN_INTERVAL_S     (60UL * 60)

// Fixed local-time offset from UTC, in minutes, used only for interpreting
// daytimeStartsAtH/daytimeEndsAtH as wall-clock local time (see utcToLocalTm()/
// isOutsideWorkingHours()) - a real IANA timezone database isn't available on this hardware, and
// GMT_OFFSET_SEC/DAY_LIGHT_OFFSET_SEC above are deliberately left at 0 so every *other* timestamp
// in this firmware (logs, ISO8601 fields, isNightForExposure()'s sun math) stays in UTC. This is
// a plain fixed offset, not auto-adjusting daylight saving - NZDT (UTC+13) vs NZST (UTC+12)
// currently has to be toggled by hand via config.json/the API twice a year. Automating that is a
// reasonable future improvement if it becomes a hassle.
#define DEFAULT_UTC_OFFSET_MINUTES   (12 * 60)   // NZST, UTC+12
// Fallback location for isNightForExposure()'s sunrise/sunset calculation, used whenever
// deviceConfig.geoLat/geoLon are still their default 0,0 (no GPS fix yet - see
// updateGeoLocationIfDue()). Wellington, NZ - close enough for every unit currently deployed that
// a fix hasn't landed for yet; a device with a real fix always uses that instead.
#define DEFAULT_LAT (-41.2887)
#define DEFAULT_LON (174.7771)

// Stretches the capture interval out as the battery gets low, to conserve what's left - see
// applyLowBatteryIntervalFloor(). Each is a floor, not an override: it only ever lengthens
// whatever cameraIntervalS is already configured, never shortens it.
#define BATTERY_LOW_PERCENT_50       50
#define BATTERY_LOW_PERCENT_25       25
#define BATTERY_LOW_PERCENT_15       15
#define BATTERY_LOW_INTERVAL_50_S    (5 * 60)     // 5 minutes
#define BATTERY_LOW_INTERVAL_25_S    (15 * 60)    // 15 minutes
#define BATTERY_LOW_INTERVAL_15_S    (60 * 60)    // 1 hour

#define DEFAULT_API_URL              "https://timelapse-dev.azurewebsites.net/api/"
#define DEFAULT_HFLIP                false
#define DEFAULT_VFLIP                false
#define DEFAULT_GEO_INTERVAL_S       (60 * 60)   // Check GPS position once an hour by default
#define DEFAULT_AUTO_SYNC_PERIOD_S   (5 * 60)    // Force a WiFi resync/upload after this long, to correct clock drift
#define DEFAULT_CAMERA_MODEL         "OV5640"    // Overridden below by on-the-fly PID detection - see setup()
#define DEFAULT_CAMERA_WARMUP_FRAMES 2            // Frames grabbed+discarded after sensor init to let AEC/AWB converge - see setup()
#define DEFAULT_SUPPORT_MODE         false        // See loop() - true keeps the board awake instead of deep-sleeping

// Mirrors the Raspberry Pi units' camera.enable_long_exposure_at_night (see scripts/savePhotos.py)
// as closely as the OV5640 hardware allows - see setupCameraNightExposure(). Off by default: this
// is a much rougher lever than the Pi's libcamera ExposureTime control (see that function's
// comment), and wants field verification on real hardware before being turned on fleet-wide.
#define DEFAULT_ENABLE_LONG_EXPOSURE_AT_NIGHT true
// esp_camera's OV5640 driver derives the sensor's whole PLL/pixel-clock chain from
// camera_config_t.xclk_freq_hz (see set_framesize() in the esp32-camera component) - halving
// xclk roughly halves pixel clock, which roughly doubles how long the sensor's fixed per-frame
// exposure-line ceiling is worth in wall-clock time. 8MHz (vs. the normal 20MHz - see
// config.xclk_freq_hz below) lands comfortably inside the OV5640's documented PLL input/VCO
// range while giving roughly a 2.5x longer max exposure than the daytime clock allows. Tunable
// via config.json without a reflash if a deployed unit needs a different value.
#define DEFAULT_LONG_EXPOSURE_XCLK_HZ (8000000UL)
// set_aec_value() (see ov5640.c) clamps whatever's passed to the sensor's *current* total frame
// length in lines (register 0x380e/0x380f, "VTS") - but pinning exposure to that literal ceiling
// leaves the sensor zero blanking/readout margin, which is out of spec (most OV-series sensors
// need exposure lines <= VTS - 4) and was observed causing roughly a coin-flip's worth of failed/
// corrupted captures ("Capturing camera failed!") - see setupCameraNightExposure(). Backed off a
// bit further than the datasheet's bare minimum since this hasn't been characterised on real
// hardware yet.
#define NIGHT_EXPOSURE_MARGIN_LINES 16

// utilities.h defines MODEM_GPS_ENABLE_GPIO/MODEM_GPS_ENABLE_LEVEL etc. per board, but not this -
// matches LilyGo's own reference examples for the SIM7670G-S3 (e.g. LilyGo-Modem-Series/examples/Traccar).
#define MODEM_POWERON_PULSE_WIDTH_MS 100

#define GEO_MODEM_BOOT_RETRIES       30      // testAT() attempts before re-pulsing PWRKEY
#define GEO_FIX_TIMEOUT_MS           120000  // Give up on a GPS fix after this long, this cycle

RTC_DATA_ATTR int bootCount = 0;

struct TelemetryCounts {
    int pendingImages;
    int pendingTelemetry;
};

// Config handed back by the API in the Device object nested in every Image/Telemetry POST
// response (see applyDeviceConfigFromApiResponse), and cached to CONFIG_FILE. Reloaded fresh
// from SD every boot (deep sleep doesn't preserve normal RAM).
struct DeviceConfig {
    bool sleepDuringNight = DEFAULT_SLEEP_DURING_NIGHT;
    int daytimeStartsAtH = DEFAULT_DAYTIME_STARTS_AT_H;
    int daytimeEndsAtH = DEFAULT_DAYTIME_ENDS_AT_H;
    // See DEFAULT_UTC_OFFSET_MINUTES above - only affects daytimeStartsAtH/daytimeEndsAtH.
    int utcOffsetMinutes = DEFAULT_UTC_OFFSET_MINUTES;
    uint32_t cameraIntervalS = DEFAULT_CAMERA_INTERVAL_S;
    String apiUrl = DEFAULT_API_URL;
    bool hflip = DEFAULT_HFLIP;
    bool vflip = DEFAULT_VFLIP;
    uint32_t autoSyncPeriodS = DEFAULT_AUTO_SYNC_PERIOD_S;

    // Set remotely (see the API's Device row / scripts/uploadPending.py's identical field for the
    // Raspberry Pi units) to keep the board awake indefinitely instead of deep-sleeping between
    // wake cycles - see loop(). Meant for a technician who needs the board reliably reachable
    // (serial/OTA) rather than asleep most of the time.
    bool supportMode = DEFAULT_SUPPORT_MODE;

    // See DEFAULT_ENABLE_LONG_EXPOSURE_AT_NIGHT/DEFAULT_LONG_EXPOSURE_XCLK_HZ above and
    // setupCameraNightExposure() below - OV5640 only, silently ignored on OV2640 (its driver has
    // no equivalent lever - see setupCameraNightExposure()'s comment).
    bool enableLongExposureAtNight = DEFAULT_ENABLE_LONG_EXPOSURE_AT_NIGHT;
    uint32_t longExposureXclkHz = DEFAULT_LONG_EXPOSURE_XCLK_HZ;

    // What sensor is actually fitted - "OV5640" or "OV2640". Set from on-the-fly PID detection
    // every boot (see setup()), which is authoritative whenever it recognises the sensor; this
    // field only actually gets *used* to pick a target resolution if detection comes back with
    // something unrecognised (e.g. a driver update adding new PIDs). Still persisted to
    // CONFIG_FILE / handed back to the API each boot so it shows up centrally without needing a
    // fresh detection pass to find out what's on a given device.
    String cameraModel = DEFAULT_CAMERA_MODEL;

    // How many frames to grab and throw away right after sensor init before keeping one - the
    // sensor's auto-exposure/auto-white-balance haven't converged yet on the first frame or two
    // after power-on, which otherwise shows up as inconsistent exposure/colour cast between
    // captures (each cycle powers the camera off - see setCameraPower(false) - so this isn't a
    // one-time startup cost, it happens every single wake).
    uint8_t cameraWarmupFrames = DEFAULT_CAMERA_WARMUP_FRAMES;

    // Locally-determined (see updateGeoLocationIfDue()) - never handed back by the API, unlike
    // everything above, so applyConfigFields() only ever touches these when reading CONFIG_FILE
    // back, never from an API response. Kept here (rather than only in RAM) so a device that's
    // deep-sleeping most of the time still has a last-known position to work from - e.g. for
    // sunrise/sunset - without needing a fresh fix on every single boot.
    uint32_t geoIntervalS = DEFAULT_GEO_INTERVAL_S;
    double geoLat = 0;
    double geoLon = 0;
    String geoTimeRecorded = "";   // ISO8601 - empty means never recorded
};

DeviceConfig deviceConfig;

// Set at the end of each runWakeCycle() - see there - purely so runSetupApWindow()'s status page
// has something to show for "did this boot's capture/upload actually happen, and when". Not
// persisted; these only ever need to reflect the boot currently in progress.
String lastCaptureTimestamp = "";
bool lastWakeConnectedWiFi = false;

// The SIM7670G's cellular/GNSS modem, talked to over the UART wired up as SerialAT (see
// utilities.h) - used only for GPS here (see updateGeoLocationIfDue()).
TinyGsm modem(SerialAT);

// Piecewise-linear state-of-charge curve for a single-cell 3.7V Li-ion (these boards run off a
// single 3400mAh cell). Mirrors VoltageToPercentageHelper.cs on the API side, so a percentage
// computed here lines up with one the API would compute from raw voltage.
struct VoltagePercentPoint {
    uint16_t mv;
    uint8_t percent;
};

const VoltagePercentPoint BATTERY_CURVE[] = {
    {4200, 100},
    {3880, 90},
    {3750, 80},
    {3650, 70},
    {3535, 60},
    {3475, 50},
    {3435, 40},
    {3385, 30},
    {3280, 20},
    {3000, 10},
    {2800, 0},
};
const size_t BATTERY_CURVE_LAST = sizeof(BATTERY_CURVE) / sizeof(BATTERY_CURVE[0]) - 1;

// Struct definitions used in function signatures below are kept up here, ahead of every
// function that uses them - the Arduino IDE auto-generates forward declarations for every
// function and inserts them near the top of the file, ahead of code that (in source order)
// comes later. A struct defined further down than the functions using it would otherwise be
// unknown to the compiler at the point that auto-generated declaration lands ("'Foo' does not
// name a type"). DeviceConfig above is fine because it's already up here for the same reason.

// Where telemetry/images get filed under TELEMETRY_DIR/CAMERA_DIR - bucketed by
// yyyy/mm/dd/hh so no single directory ever accumulates more than a handful of entries no
// matter how large the backlog grows (a flat directory gets linearly slower to scan *and* to
// create new files in, on FAT, as it fills up - this is what was causing the slowdown).
// The leaf name carries the full yyyymmdd-hhmmss (not just mmss) so a file is still
// unambiguous if it ever ends up copied out of its yyyy/mm/dd/hh bucket - e.g. into a flat
// backup folder - and so two captures inside the same minute don't collide (the capture
// interval has been as low as 10s in the past).
// Falls back to a flat "unsynced/boot-N" name if the clock was never synced.
struct DatedPath {
    String dirPath;    // e.g. "2026/08/13/14", or "unsynced"
    String leafName;   // e.g. "20260813-140530", or "boot-3"
};

// Pieces of a parsed apiUrl (e.g. "https://timelapse-dev.azurewebsites.net/api/") - what
// WiFiClient(Secure)::connect() and the manually-built HTTP request need.
struct ParsedUrl {
    bool https;
    String host;
    uint16_t port;
    String path;   // includes leading '/', excludes the endpoint name (e.g. "Image")
};

// One name/value pair of a multipart/form-data POST.
struct HttpFormField {
    String name;
    String value;
};

// Holds a connection to the API host that can be reused across several postMultipartForm() calls
// within one upload batch (see uploadPendingTelemetry()/uploadPendingImages()) instead of paying
// for a fresh TCP+TLS handshake - several seconds each on this hardware - on every single record.
// One instance is scoped to a single upload batch (a local in each of those functions), not kept
// across wake cycles.
struct ApiConnection {
    WiFiClientSecure secureClient;
    WiFiClient plainClient;
    String host;
    uint16_t port = 0;
    bool https = false;
    bool isOpen = false;
};

// Guards the file-write half of logLine()/logf() - false until setupSD() confirms the card is
// mounted and LOG_DIR exists. Serial output happens either way, so nothing is lost before then.
bool sdReady = false;

// Prints `line` to Serial and, once the SD card is ready, appends it to LOG_FILE prefixed with
// the current date/time - lets a field unit's whole history be pulled off the SD card and
// grepped by date without ever needing a serial connection to it. See rotateLogIfNeeded() for
// the once-a-day file rollover, and logf() below for a printf-style version of this.
void logLine(const String &line)
{
    Serial.println(line);

    if (!sdReady) {
        return;
    }

    char timestamp[20] = "unsynced";
    struct tm timeinfo;
    if (getLocalTime(&timeinfo, 0)) {
        strftime(timestamp, sizeof(timestamp), "%Y-%m-%d %H:%M:%S", &timeinfo);
    }

    File file = SD.open(LOG_FILE, FILE_APPEND);
    if (!file) {
        Serial.println("Failed to open " LOG_FILE " for writing");
        return;
    }
    file.println(String(timestamp) + " " + line);
    file.close();
}

// printf-style version of logLine() - sized to fit the formatted message rather than truncated
// at some fixed stack buffer, since a few callers (e.g. full API response bodies) can run well
// past 1KB.
void logf(const char *format, ...)
{
    va_list args;
    va_start(args, format);
    va_list argsCopy;
    va_copy(argsCopy, args);
    int needed = vsnprintf(nullptr, 0, format, argsCopy);
    va_end(argsCopy);

    if (needed < 0) {
        va_end(args);
        return;
    }

    std::vector<char> buf(needed + 1);
    vsnprintf(buf.data(), buf.size(), format, args);
    va_end(args);

    logLine(String(buf.data()));
}

// Registered via esp_log_set_vprintf() in setup() - catches ESP-IDF's own ESP_LOGW/ESP_LOGE output
// (e.g. the esp32-camera driver's "Failed to get frame: timeout", or its DMA buffer malloc-failed
// message) and appends it to LOG_FILE alongside logLine()/logf(), not just Serial. Without this,
// a "Capturing camera failed!" entry never says *why* esp_camera_fb_get() returned NULL - the
// driver already logs the reason, it just never reached the SD card. esp_log_level_set("*", ...)
// in setup() keeps this to warnings/errors only, so routine INFO/DEBUG chatter from WiFi/BLE/the
// modem library doesn't turn into a flood of SD file opens every boot.
int fileLogVprintf(const char *fmt, va_list args)
{
    va_list argsCopy;
    va_copy(argsCopy, args);
    int written = vprintf(fmt, args);   // preserve the normal Serial/UART output

    if (sdReady) {
        char buf[256];
        vsnprintf(buf, sizeof(buf), fmt, argsCopy);

        char timestamp[20] = "unsynced";
        struct tm timeinfo;
        if (getLocalTime(&timeinfo, 0)) {
            strftime(timestamp, sizeof(timestamp), "%Y-%m-%d %H:%M:%S", &timeinfo);
        }

        File file = SD.open(LOG_FILE, FILE_APPEND);
        if (file) {
            file.print(String(timestamp) + " ");
            file.print(buf);   // ESP-IDF log lines already end in \r\n
            file.close();
        }
    }
    va_end(argsCopy);
    return written;
}

// esp_reset_reason() is the ROM/bootloader's own record of why this boot happened, surviving even
// resets that lose RTC memory (see bootCount) - unlike fileLogVprintf()/the PSRAM logging near
// esp_camera_fb_get(), which only explain a *clean* boot's failed capture, this is what tells us
// whether a run of low/resetting bootCount values (RTC_DATA_ATTR wiped) is a brownout, a watchdog
// timeout, a panic, or the unit genuinely losing power - none of which leave any other trace,
// since a brownout/panic reset happens before any app code (including our log hooks) runs.
const char *resetReasonName(esp_reset_reason_t reason)
{
    switch (reason) {
    case ESP_RST_POWERON:   return "POWERON (fresh power-on, or RTC memory lost)";
    case ESP_RST_BROWNOUT:  return "BROWNOUT (supply voltage dropped below the chip's cutoff)";
    case ESP_RST_TASK_WDT:  return "TASK_WDT (a task hung/blocked too long)";
    case ESP_RST_INT_WDT:   return "INT_WDT (interrupt watchdog - ISR ran too long)";
    case ESP_RST_WDT:       return "WDT (other watchdog)";
    case ESP_RST_PANIC:     return "PANIC (software crash - exception/assert)";
    case ESP_RST_SW:        return "SW (esp_restart() called)";
    case ESP_RST_DEEPSLEEP: return "DEEPSLEEP (normal wake from deep sleep)";
    case ESP_RST_EXT:       return "EXT (external reset pin)";
    case ESP_RST_SDIO:      return "SDIO";
    default:                return "UNKNOWN";
    }
}

bool setCameraPower(bool enable)
{
    static bool started = false;

    if (!started) {
        Wire.begin(BOARD_SDA_PIN, BOARD_SCL_PIN);
        Wire.beginTransmission(0x28);
        if (Wire.endTransmission() != 0) {
            logLine("Camera power chip not found!");
            return false;
        }
    }
    started = true;
    uint8_t vdd[] = {
        0x03,   /*reg address*/
        0x7C,   /*REG03 DVDD1 1.496V*/
        0x7C,   /*REG04 DVDD2 1.496v*/
        0xCA,   /*REG05 AVDD1 3.0V*/
        0xB1    /*REG06 AVDD2 2.8V*/
    };
    Wire.beginTransmission(0x28);
    Wire.write(vdd, sizeof(vdd) / sizeof(vdd[0]));
    Wire.endTransmission();

    uint8_t control[] = {0x0E, 0x0F};
    if (!enable) {
        control[1] = 0x00;
    }
    Wire.beginTransmission(0x28);
    Wire.write(control, sizeof(control) / sizeof(control[0]));
    Wire.endTransmission();

    if (enable) {
        /*
        * Maximize the use of GPIO. No GPIO is assigned to the camera reset pin, so the camera is reset by powering on again.
        * */
        control[1] = 0x00;
        Wire.beginTransmission(0x28);
        Wire.write(control, sizeof(control) / sizeof(control[0]));
        Wire.endTransmission();
        delay(300);
        control[1] = 0x0F;
        Wire.beginTransmission(0x28);
        Wire.write(control, sizeof(control) / sizeof(control[0]));
        Wire.endTransmission();
        delay(100);
    }
    return true;
}

uint16_t get_battery_voltage()
{
    uint16_t vol = analogReadMilliVolts(BOARD_BAT_ADC_PIN) * 2;
    logf("Voltage:%u", vol);
    return vol;
}

// Returns 0 if the board has no solar ADC pin wired up
uint16_t get_solar_voltage()
{
#ifdef BOARD_SOLAR_ADC_PIN
    uint16_t vol = analogReadMilliVolts(BOARD_SOLAR_ADC_PIN) * 2;
    logf("Solar voltage:%u", vol);
    return vol;
#else
    return 0;
#endif
}

// Converts a raw battery voltage reading to an estimated state-of-charge percentage, via
// BATTERY_CURVE above.
uint8_t get_battery_percent(uint16_t mv)
{
    if (mv >= BATTERY_CURVE[0].mv) {
        return 100;
    }
    if (mv <= BATTERY_CURVE[BATTERY_CURVE_LAST].mv) {
        return 0;
    }

    for (size_t i = 0; i < BATTERY_CURVE_LAST; i++) {
        if (mv <= BATTERY_CURVE[i].mv && mv > BATTERY_CURVE[i + 1].mv) {
            float ratio = (float)(mv - BATTERY_CURVE[i].mv) / (float)(BATTERY_CURVE[i + 1].mv - BATTERY_CURVE[i].mv);
            return (uint8_t)(BATTERY_CURVE[i].percent + ratio * (BATTERY_CURVE[i + 1].percent - BATTERY_CURVE[i].percent));
        }
    }
    return 0;
}

// Floors intervalS up to the BATTERY_LOW_INTERVAL_*_S for the lowest BATTERY_LOW_PERCENT_* tier
// batteryPercent is under - e.g. 20% -> at least BATTERY_LOW_INTERVAL_25_S. Never returns less
// than intervalS itself: an already-longer configured interval is left alone.
uint32_t applyLowBatteryIntervalFloor(uint32_t intervalS, uint8_t batteryPercent)
{
    uint32_t floorS = 0;
    if (batteryPercent < BATTERY_LOW_PERCENT_50) {
        floorS = BATTERY_LOW_INTERVAL_50_S;
    }
    if (batteryPercent < BATTERY_LOW_PERCENT_25) {
        floorS = BATTERY_LOW_INTERVAL_25_S;
    }
    if (batteryPercent < BATTERY_LOW_PERCENT_15) {
        floorS = BATTERY_LOW_INTERVAL_15_S;
    }
    return intervalS > floorS ? intervalS : floorS;
}

void set_device_to_sleep()
{
    logLine("Enter esp32 goto deepsleep!");
    esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP * uS_TO_S_FACTOR);
    delay(200);
    esp_deep_sleep_start();
    Serial.println("This will never be printed");
    while (1);
}

// Arduino IDE auto-generates these prototypes from the .ino; a plain .cpp needs them explicit
// since setupSD() below calls both before their definitions further down this file.
void ensureDirExists(const String &path);
void rotateLogIfNeeded();

// One capture+telemetry+upload cycle - see runWakeCycle() further down. setup() runs it once on
// every real boot; loop() calls it again, repeatedly, while parked awake in support mode (see
// DeviceConfig::supportMode) instead of deep-sleeping between cycles.
void runWakeCycle();

// The local, no-connectivity-needed alternative to supportMode for a fresh install - see
// runSetupApWindow() further down.
void runSetupApWindow();

bool setupSD()
{
    SPI.begin(BOARD_SCK_PIN, BOARD_MISO_PIN, BOARD_MOSI_PIN);

    if (!SD.begin(BOARD_SD_CS_PIN)) {
        Serial.println("Card Mount Failed");
        return false;
    }
    uint8_t cardType = SD.cardType();

    if (cardType == CARD_NONE) {
        Serial.println("No SD card attached");
        return false;
    }

    // Everything above can't go through logLine() yet - the card isn't confirmed writable until
    // this point, and logLine()'s file-write half depends on LOG_DIR already existing.
    ensureDirExists(LOG_DIR);
    sdReady = true;
    rotateLogIfNeeded();

    String cardTypeStr = "UNKNOWN";
    if (cardType == CARD_MMC) {
        cardTypeStr = "MMC";
    } else if (cardType == CARD_SD) {
        cardTypeStr = "SDSC";
    } else if (cardType == CARD_SDHC) {
        cardTypeStr = "SDHC";
    }
    logLine("SD Card Type: " + cardTypeStr);

    uint64_t cardSize = SD.cardSize() / (1024 * 1024);
    logf("SD Card Size: %lluMB", cardSize);
    return true;
}

// Returns 0 if never synced (no file yet, or unreadable)
time_t readLastSyncTime()
{
    File file = SD.open(LAST_SYNC_FILE, "r");
    if (!file) {
        return 0;
    }
    time_t lastSync = (time_t)file.readStringUntil('\n').toInt();
    file.close();
    return lastSync;
}

void writeLastSyncTime(time_t t)
{
    File file = SD.open(LAST_SYNC_FILE, "w");
    if (file) {
        file.println((long)t);
        file.close();
    } else {
        logLine("Failed to write last sync time!");
    }
}

// Deliberately a plain file rather than an RTC_DATA_ATTR flag (like bootCount) - RTC memory only
// survives deep sleep, not a real power loss, and a device that's lost power while badly
// backlogged is exactly the case this needs to keep working through. A plain timestamp-based
// "just don't update lastSyncTime" approach was considered instead of this, but rejected: it'd
// depend on lastSyncTime staying stale relative to `now`, which a mid-cycle NTP correction (see
// connectWiFiAndSyncTime()) could quietly undermine if it ever jumps the clock backward. An
// explicit flag doesn't care what the clock does at all.
bool readForceSyncFlag()
{
    return SD.exists(FORCE_SYNC_FILE);
}

void setForceSyncFlag()
{
    File file = SD.open(FORCE_SYNC_FILE, "w");
    if (file) {
        file.close();
    } else {
        logLine("Failed to write force sync flag!");
    }
}

void clearForceSyncFlag()
{
    if (SD.exists(FORCE_SYNC_FILE)) {
        SD.remove(FORCE_SYNC_FILE);
    }
}

// Returns all-zero counts if never written yet (no file, or unreadable)
TelemetryCounts readCounts()
{
    TelemetryCounts counts = {0, 0};
    File file = SD.open(COUNTS_FILE, "r");
    if (!file) {
        return counts;
    }
    String line = file.readStringUntil('\n');
    file.close();
    if (sscanf(line.c_str(), "%d,%d", &counts.pendingImages, &counts.pendingTelemetry) != 2) {
        counts = {0, 0};   // e.g. an older counts.txt from before uploadedImages/uploadedTelemetry were dropped
    }
    return counts;
}

void writeCounts(const TelemetryCounts &counts)
{
    File file = SD.open(COUNTS_FILE, "w");
    if (file) {
        file.printf("%d,%d\n", counts.pendingImages, counts.pendingTelemetry);
        file.close();
    } else {
        logLine("Failed to write counts file!");
    }
}

// Applies whichever of sleepDuringNight/daytimeStartsAtH/daytimeEndsAtH/utcOffsetMinutes/
// cameraIntervalS/apiUrl/hflip/vflip/autoSyncPeriodS/geoIntervalS/geoLat/geoLon/geoTimeRecorded/
// cameraModel/cameraWarmupFrames/enableLongExposureAtNight/longExposureXclkHz are present in
// `fields` on top of an existing DeviceConfig - used for both
// CONFIG_FILE (on disk) and the Device object nested in an Image/Telemetry API response, so a
// partial payload only touches the fields it mentions. In practice the geo* fields and
// cameraModel only ever come from CONFIG_FILE - the API has no way to know a device's GPS
// position or what sensor is physically fitted, so its Device object never carries them, and
// applyConfigFields() just leaves the current in-memory values alone when called with API JSON.
void applyConfigFields(JsonVariantConst fields, DeviceConfig &config)
{
    config.sleepDuringNight = fields["sleepDuringNight"] | config.sleepDuringNight;
    config.daytimeStartsAtH = fields["daytimeStartsAtH"] | config.daytimeStartsAtH;
    config.daytimeEndsAtH   = fields["daytimeEndsAtH"] | config.daytimeEndsAtH;
    config.utcOffsetMinutes = fields["utcOffsetMinutes"] | config.utcOffsetMinutes;
    config.cameraIntervalS  = fields["cameraIntervalS"] | config.cameraIntervalS;
    config.hflip            = fields["hflip"] | config.hflip;
    config.vflip            = fields["vflip"] | config.vflip;
    config.autoSyncPeriodS  = fields["autoSyncPeriodS"] | config.autoSyncPeriodS;
    config.supportMode      = fields["supportMode"] | config.supportMode;
    config.geoIntervalS     = fields["geoIntervalS"] | config.geoIntervalS;
    config.geoLat           = fields["geoLat"] | config.geoLat;
    config.geoLon           = fields["geoLon"] | config.geoLon;
    config.cameraWarmupFrames = fields["cameraWarmupFrames"] | config.cameraWarmupFrames;
    config.enableLongExposureAtNight = fields["enableLongExposureAtNight"] | config.enableLongExposureAtNight;
    config.longExposureXclkHz = fields["longExposureXclkHz"] | config.longExposureXclkHz;
    if (!fields["apiUrl"].isNull()) {
        String apiUrl = fields["apiUrl"].as<String>();
        if (apiUrl.length() > 0) {
            config.apiUrl = apiUrl;
        }
    }
    if (!fields["geoTimeRecorded"].isNull()) {
        config.geoTimeRecorded = fields["geoTimeRecorded"].as<String>();
    }
    if (!fields["cameraModel"].isNull()) {
        String cameraModel = fields["cameraModel"].as<String>();
        if (cameraModel.length() > 0) {
            config.cameraModel = cameraModel;
        }
    }
}

// Returns DEFAULT_* values (see DeviceConfig) if CONFIG_FILE doesn't exist yet or won't parse -
// i.e. before the API has ever handed back a Device config.
DeviceConfig readDeviceConfig()
{
    DeviceConfig config;

    File file = SD.open(CONFIG_FILE, "r");
    if (!file) {
        logLine(CONFIG_FILE " not found - using default config");
        return config;
    }

    DynamicJsonDocument doc(768);
    DeserializationError err = deserializeJson(doc, file);
    file.close();

    if (err) {
        logf("Failed to parse " CONFIG_FILE ": %s - using default config", err.c_str());
        return config;
    }

    applyConfigFields(doc, config);
    return config;
}

void writeDeviceConfig(const DeviceConfig &config)
{
    DynamicJsonDocument doc(768);
    doc["sleepDuringNight"] = config.sleepDuringNight;
    doc["daytimeStartsAtH"] = config.daytimeStartsAtH;
    doc["daytimeEndsAtH"] = config.daytimeEndsAtH;
    doc["utcOffsetMinutes"] = config.utcOffsetMinutes;
    doc["cameraIntervalS"] = config.cameraIntervalS;
    doc["apiUrl"] = config.apiUrl;
    doc["hflip"] = config.hflip;
    doc["vflip"] = config.vflip;
    doc["autoSyncPeriodS"] = config.autoSyncPeriodS;
    doc["supportMode"] = config.supportMode;
    doc["geoIntervalS"] = config.geoIntervalS;
    doc["geoLat"] = config.geoLat;
    doc["geoLon"] = config.geoLon;
    doc["geoTimeRecorded"] = config.geoTimeRecorded;
    doc["cameraModel"] = config.cameraModel;
    doc["cameraWarmupFrames"] = config.cameraWarmupFrames;
    doc["enableLongExposureAtNight"] = config.enableLongExposureAtNight;
    doc["longExposureXclkHz"] = config.longExposureXclkHz;

    File file = SD.open(CONFIG_FILE, "w");
    if (file) {
        serializeJson(doc, file);
        file.close();
        logLine("Config written to " CONFIG_FILE);
    } else {
        logLine("Failed to write config file!");
    }
}

// Parses the JSON body of an Image/Telemetry POST response and refreshes config + CONFIG_FILE
// if the device's config in the API has changed. ImageController/TelemetryController return
// the created row plus its related Device - the same place supportMode/hibernateMode/etc.
// already live for the Raspberry Pi units (see scripts/uploadPending.py).
void applyDeviceConfigFromApiResponse(const String &responseBody, DeviceConfig &config)
{
    DynamicJsonDocument doc(1152);
    DeserializationError err = deserializeJson(doc, responseBody);
    if (err) {
        logf("Failed to parse API response: %s", err.c_str());
        return;
    }

    JsonVariant device = doc["device"];
    if (device.isNull()) {
        logLine("API response had no device config to apply");
        return;
    }

    DeviceConfig newConfig = config;
    applyConfigFields(device, newConfig);

    if (newConfig.sleepDuringNight != config.sleepDuringNight ||
        newConfig.daytimeStartsAtH != config.daytimeStartsAtH ||
        newConfig.daytimeEndsAtH != config.daytimeEndsAtH ||
        newConfig.utcOffsetMinutes != config.utcOffsetMinutes ||
        newConfig.cameraIntervalS != config.cameraIntervalS ||
        newConfig.apiUrl != config.apiUrl ||
        newConfig.hflip != config.hflip ||
        newConfig.vflip != config.vflip ||
        newConfig.autoSyncPeriodS != config.autoSyncPeriodS ||
        newConfig.supportMode != config.supportMode ||
        newConfig.geoIntervalS !=config.geoIntervalS ||
        newConfig.cameraWarmupFrames != config.cameraWarmupFrames ||
        newConfig.enableLongExposureAtNight != config.enableLongExposureAtNight ||
        newConfig.longExposureXclkHz != config.longExposureXclkHz
) {
        config = newConfig;
        writeDeviceConfig(config);
        logLine("Config updated from API");
    }
}

bool connectWiFiAndSyncTime()
{
    logf("Connecting to WiFi: %s", WIFI_SSID);
    WiFi.mode(WIFI_STA);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    uint32_t startTime = millis();
    while (WiFi.status() != WL_CONNECTED) {
        if (millis() - startTime > WIFI_CONNECT_TIMEOUT_MS) {
            logLine("WiFi connect timed out!");
            return false;
        }
        delay(250);
        Serial.print(".");   // progress dots - serial only, not worth a timestamped line each
    }
    Serial.println();
    logf("WiFi connected, IP: %s", WiFi.localIP().toString().c_str());

    configTime(GMT_OFFSET_SEC, DAY_LIGHT_OFFSET_SEC, NTP_SERVER);

    struct tm timeinfo;
    if (!getLocalTime(&timeinfo)) {
        logLine("Failed to obtain time from NTP server!");
        return false;
    }
    logf("Time synced: %04d-%02d-%02d %02d:%02d:%02d",
         timeinfo.tm_year + 1900, timeinfo.tm_mon + 1, timeinfo.tm_mday,
         timeinfo.tm_hour, timeinfo.tm_min, timeinfo.tm_sec);
    return true;
}

// Splits an absolute path into its non-empty segments, e.g. "/a/b/c" -> ["a","b","c"].
std::vector<String> splitPath(const String &path)
{
    std::vector<String> segments;
    int start = 0;
    while (start < (int)path.length()) {
        int slashIdx = path.indexOf('/', start);
        String segment = (slashIdx >= 0) ? path.substring(start, slashIdx) : path.substring(start);
        if (segment.length() > 0) {
            segments.push_back(segment);
        }
        if (slashIdx < 0) {
            break;
        }
        start = slashIdx + 1;
    }
    return segments;
}

// SD.mkdir() on this SD library only creates one level at a time - unlike "mkdir -p", it fails
// if the parent doesn't already exist. Walks `path` component by component, creating whichever
// levels are missing.
void ensureDirExists(const String &path)
{
    String current = "";
    for (const String &segment : splitPath(path)) {
        current += "/" + segment;
        if (!SD.exists(current)) {
            if (!SD.mkdir(current)) {
                logf("Failed to create directory %s", current.c_str());
            }
        }
    }
}

// Everything before the final '/' in `path` - used to make sure a destination's parent
// directories exist before SD.rename() into it (rename, like mkdir, doesn't create missing
// parents on this SD library).
String parentDir(const String &path)
{
    int slashIdx = path.lastIndexOf('/');
    return (slashIdx > 0) ? path.substring(0, slashIdx) : "/";
}

// The last path segment, e.g. "/a/b/c.jpg" -> "c.jpg".
String fileBaseName(const String &path)
{
    int slashIdx = path.lastIndexOf('/');
    return (slashIdx >= 0) ? path.substring(slashIdx + 1) : path;
}

DatedPath getDatedPath()
{
    struct tm timeinfo;
    if (!getLocalTime(&timeinfo, 0)) {
        DatedPath fallback;
        fallback.dirPath = "unsynced";
        fallback.leafName = "boot-" + String(bootCount);
        return fallback;
    }

    char dirBuf[16];
    snprintf(dirBuf, sizeof(dirBuf), "%04d/%02d/%02d/%02d",
              timeinfo.tm_year + 1900, timeinfo.tm_mon + 1, timeinfo.tm_mday, timeinfo.tm_hour);

    char leafBuf[20];
    snprintf(leafBuf, sizeof(leafBuf), "%04d%02d%02d-%02d%02d%02d",
              timeinfo.tm_year + 1900, timeinfo.tm_mon + 1, timeinfo.tm_mday,
              timeinfo.tm_hour, timeinfo.tm_min, timeinfo.tm_sec);

    DatedPath result;
    result.dirPath = String(dirBuf);
    result.leafName = String(leafBuf);
    return result;
}

// Parses a path built from a DatedPath (e.g. ".../2026/08/13/14/20260813-140530.jpeg") back into
// an ISO8601 timestamp - the inverse of getDatedPath(), needed when uploading images (unlike
// telemetry, which already carries its own timestamp inside the file). The leaf name alone
// carries the full yyyymmdd-hhmmss, so this doesn't need to consult the yyyy/mm/dd/hh
// directory segments at all.
String parseTimestampFromPath(const String &path)
{
    String leaf = fileBaseName(path);
    int dotIdx = leaf.lastIndexOf('.');
    String stamp = (dotIdx >= 0) ? leaf.substring(0, dotIdx) : leaf;   // "yyyymmdd-hhmmss"
    if (stamp.length() < 15) {
        return "unknown";   // e.g. the "unsynced/boot-N" fallback name
    }

    return stamp.substring(0, 4) + "-" + stamp.substring(4, 6) + "-" + stamp.substring(6, 8)
           + "T" + stamp.substring(9, 11) + ":" + stamp.substring(11, 13) + ":" + stamp.substring(13, 15) + "Z";
}

// Recursively walks `root` (an absolute SD path), collecting every regular file's full path
// into `paths`. Needed now that telemetry/images live under yyyy/mm/dd/hh buckets rather than
// one flat directory.
//
// Also removes any subdirectory of `root` that turns out to be empty once its own contents have
// been listed. Once a file's uploaded and removed out of its yyyy/mm/dd/hh bucket, nothing ever
// empties that bucket's directories again - left alone they accumulate forever, and this same
// recursive walk (which has to run every single wake, to build the upload list) gets slower and
// slower having to open and list thousands of long-dead empty directories. Pruning here, as this
// function's own post-order step, costs nothing extra - it already has every directory open at
// exactly the moment it can tell whether that directory turned out to be empty. `root` itself is
// never removed, even if empty.
void listFilesRecursive(const String &root, std::vector<String> &paths)
{
    File dir = SD.open(root);
    if (!dir) {
        return;
    }

    File entry = dir.openNextFile();
    while (entry) {
        String fullPath = root + "/" + fileBaseName(entry.name());
        bool isDirectory = entry.isDirectory();
        entry.close();

        if (isDirectory) {
            size_t before = paths.size();
            listFilesRecursive(fullPath, paths);
            if (paths.size() == before) {
                SD.rmdir(fullPath);   // nothing under it (directly or nested) - safe to drop
            }
        } else {
            paths.push_back(fullPath);
        }

        entry = dir.openNextFile();
    }
    dir.close();
}

// Deletes rotated log files (LOG_DIR/envirocam-YYYY-MM-DD.log) older than LOG_RETENTION_DAYS -
// keeps a field unit's SD card from slowly filling up with a whole deployment's worth of daily
// logs. Only called once a day, from rotateLogIfNeeded(), right after that day's file rolls over.
void pruneOldLogs(const struct tm &today)
{
    struct tm todayMidnight = today;
    todayMidnight.tm_hour = todayMidnight.tm_min = todayMidnight.tm_sec = 0;
    time_t todayEpoch = mktime(&todayMidnight);

    std::vector<String> filePaths;
    listFilesRecursive(LOG_DIR, filePaths);

    for (const String &path : filePaths) {
        String name = fileBaseName(path);
        if (!name.startsWith("envirocam-") || !name.endsWith(".log")) {
            continue;   // not a rotated log - e.g. the active LOG_FILE itself
        }

        String datePart = name.substring(String("envirocam-").length(), name.length() - 4);   // strip ".log"
        struct tm fileDate = {};
        if (sscanf(datePart.c_str(), "%d-%d-%d", &fileDate.tm_year, &fileDate.tm_mon, &fileDate.tm_mday) != 3) {
            continue;   // unexpected name - leave it alone rather than guess
        }
        fileDate.tm_year -= 1900;
        fileDate.tm_mon -= 1;
        time_t fileEpoch = mktime(&fileDate);

        int ageDays = (int)((todayEpoch - fileEpoch) / 86400);
        if (ageDays > LOG_RETENTION_DAYS) {
            logLine("Deleting expired log: " + path);
            SD.remove(path);
        }
    }
}

// Rotates LOG_FILE to LOG_DIR/envirocam-YYYY-MM-DD.log once its contents are no longer from
// today, then prunes anything past LOG_RETENTION_DAYS. Kept as plain, uncompressed daily files
// rather than zipped on-device - this unit runs off battery/solar and deep-sleeps almost
// immediately every cycle, so spending CPU-awake time compressing logs isn't worth it; zip them
// up later, off-device, the same way the Raspberry Pi units' logs already get pulled (see
// grabLogs.sh) and rotated (see logs/envirocam-2w-a/timelapse.log.YYYY-MM-DD for that
// convention).
void rotateLogIfNeeded()
{
    struct tm timeinfo;
    if (!getLocalTime(&timeinfo, 0)) {
        return;   // clock not synced yet - retry on a later boot once it is
    }

    char today[11];
    strftime(today, sizeof(today), "%Y-%m-%d", &timeinfo);

    // The first line of the active log always starts with the date it was opened on (see
    // logLine()) - reading it back tells us whether today's entries would land in yesterday's
    // file, without needing a separate marker file to track it.
    File file = SD.open(LOG_FILE, "r");
    if (!file) {
        return;   // no log yet - nothing to rotate
    }
    String firstLine = file.readStringUntil('\n');
    file.close();

    if (firstLine.length() < 10 || firstLine.startsWith(today)) {
        return;   // already logging under today's date
    }

    String rotatedPath = String(LOG_DIR) + "/envirocam-" + firstLine.substring(0, 10) + ".log";
    SD.rename(LOG_FILE, rotatedPath);
    logLine("Rotated previous day's log to " + rotatedPath);

    pruneOldLogs(timeinfo);
}

// Builds an ISO8601 timestamp, e.g. "2026-08-04T05:30:45Z" - used in telemetry payloads.
// The "Z" assumes GMT_OFFSET_SEC/DAY_LIGHT_OFFSET_SEC are left at 0 (UTC)
String getISO8601Timestamp()
{
    struct tm timeinfo;
    char buf[25];
    if (!getLocalTime(&timeinfo, 0)) {
        return String("unknown");
    }
    strftime(buf, sizeof(buf), "%Y-%m-%dT%H:%M:%SZ", &timeinfo);
    return String(buf);
}

// Inverse of getISO8601Timestamp() - used to work out how long it's been since the last GPS fix
// (see updateGeoLocationIfDue()). Anything that doesn't parse - e.g. geoTimeRecorded's initial
// empty "never recorded yet" value - maps to 0, same convention as readLastSyncTime()'s "never
// synced".
time_t parseISO8601Timestamp(const String &iso)
{
    struct tm t = {};
    if (sscanf(iso.c_str(), "%d-%d-%dT%d:%d:%dZ",
               &t.tm_year, &t.tm_mon, &t.tm_mday, &t.tm_hour, &t.tm_min, &t.tm_sec) != 6) {
        return 0;
    }
    t.tm_year -= 1900;
    t.tm_mon -= 1;
    return mktime(&t);
}

// Derived from the efuse base MAC, so it's stable and unique per board without needing WiFi to be up
String getDeviceId()
{
    uint64_t chipId = ESP.getEfuseMac();
    char buf[13];
    snprintf(buf, sizeof(buf), "%04X%08X", (uint16_t)(chipId >> 32), (uint32_t)chipId);
    return String(buf);
}

// geoLat/geoLon/geoTimeRecorded are recorded here - at capture time - rather than read fresh from
// deviceConfig at upload time, same reasoning as uptimeSeconds: a GPS fix only actually refreshes
// every geoIntervalS (e.g. hourly), which can be much longer than cameraIntervalS (e.g. every
// minute) or the interval telemetry actually gets uploaded on (see autoSyncPeriodS) - if a moving
// camera captures several telemetry records between GPS fixes and between uploads, each one
// should carry whatever position was current *when it was captured*, not whatever's current when
// the whole backlog finally gets uploaded.
String buildTelemetryJson(const String &timestamp, const String &deviceId, uint16_t voltageMv, uint16_t solarVoltageMv, uint32_t uptimeSeconds,
                          double geoLat, double geoLon, const String &geoTimeRecorded, const TelemetryCounts &counts)
{
    // ESP32-S3 internal die temperature sensor, not ambient temperature.
    // Named temperatureC (rather than temperature_c) to match the field name used for the
    // same quantity elsewhere (API's TelemetryPostModel, Pi's saveTelemetry.py). Rounded to an
    // int here (rather than kept as the raw float reading) since that's what both of those
    // actually store/accept - TemperatureC is an int column, and a decimal value posted to it
    // gets rejected.
    int temperatureC = (int)lroundf(temperatureRead());
    uint8_t batteryPercent = get_battery_percent(voltageMv);

    char json[500];
    snprintf(json, sizeof(json),
             "{\"device_id\":\"%s\",\"timestamp\":\"%s\",\"boot_count\":%d,\"voltage_mv\":%u,\"solar_voltage_mv\":%u,\"temperatureC\":%d,"
             "\"batteryPercent\":%u,\"uptimeSeconds\":%u,"
             "\"geoLat\":%.6f,\"geoLon\":%.6f,\"geoTimeRecorded\":\"%s\","
             "\"pendingImages\":%d,\"pendingTelemetry\":%d}",
             deviceId.c_str(), timestamp.c_str(), bootCount, voltageMv, solarVoltageMv, temperatureC, batteryPercent, uptimeSeconds,
             geoLat, geoLon, geoTimeRecorded.c_str(),
             counts.pendingImages, counts.pendingTelemetry);
    return String(json);
}

void writeTelemetryFile(const DatedPath &datedPath, const String &json)
{
    String dirPath = String(TELEMETRY_DIR) + "/" + datedPath.dirPath;
    ensureDirExists(dirPath);

    String filename = dirPath + "/" + datedPath.leafName + ".json";

    File file = SD.open(filename, "w");
    if (file) {
        file.println(json);
        logf("Telemetry written: %s", json.c_str());
    } else {
        logLine("Failed to write telemetry file!");
    }
    file.close();
}

// Splits apiUrl (e.g. "https://timelapse-dev.azurewebsites.net/api/") into the pieces
// WiFiClient(Secure)::connect() and the manually-built HTTP request need.
ParsedUrl parseApiUrl(const String &url)
{
    ParsedUrl parsed;
    String rest = url;

    if (rest.startsWith("https://")) {
        parsed.https = true;
        rest = rest.substring(8);
    } else if (rest.startsWith("http://")) {
        parsed.https = false;
        rest = rest.substring(7);
    } else {
        parsed.https = true;   // no scheme given - assume https
    }

    int slashIdx = rest.indexOf('/');
    String hostPort = (slashIdx >= 0) ? rest.substring(0, slashIdx) : rest;
    parsed.path = (slashIdx >= 0) ? rest.substring(slashIdx) : "/";

    int colonIdx = hostPort.indexOf(':');
    if (colonIdx >= 0) {
        parsed.host = hostPort.substring(0, colonIdx);
        parsed.port = hostPort.substring(colonIdx + 1).toInt();
    } else {
        parsed.host = hostPort;
        parsed.port = parsed.https ? 443 : 80;
    }
    return parsed;
}

// Renders form fields as a JSON object, purely for Serial logging - what's actually sent on the
// wire is multipart/form-data, not JSON, but JSON is a much easier shape to eyeball when
// troubleshooting why a POST is being rejected (e.g. a field holding a value the API's model
// binder won't accept).
String fieldsToJson(const std::vector<HttpFormField> &fields)
{
    DynamicJsonDocument doc(1024);
    for (const HttpFormField &field : fields) {
        doc[field.name] = field.value;
    }
    String json;
    serializeJson(doc, json);
    return json;
}

// Returns a Client& already connected to api.host:api.port - reusing conn's existing connection
// if it's still alive and already pointed at the same host/port/scheme (the common case within
// one upload batch), otherwise (re)connecting. Returns nullptr if a fresh connect() fails.
Client *connectApiClient(ApiConnection &conn, const ParsedUrl &api)
{
    Client &client = api.https ? (Client &)conn.secureClient : (Client &)conn.plainClient;

    if (conn.isOpen && conn.host == api.host && conn.port == api.port && conn.https == api.https
        && client.connected()) {
        return &client;   // reuse - no new handshake needed
    }

    if (conn.isOpen) {
        // Either talking to a different host/port/scheme than last time, or the old connection
        // has died - drop it before opening a new one. Stopping whichever of the two wasn't the
        // one actually open is a harmless no-op.
        conn.secureClient.stop();
        conn.plainClient.stop();
        conn.isOpen = false;
    }

    if (api.https) {
        conn.secureClient.setInsecure();   // No cert store on-device - trust whatever's presented
    }
    if (!client.connect(api.host.c_str(), api.port)) {
        return nullptr;
    }

    conn.host = api.host;
    conn.port = api.port;
    conn.https = api.https;
    conn.isOpen = true;
    return &client;
}

// Performs a multipart/form-data POST to <apiUrl><endpoint>, with the given form fields plus
// an optional file streamed from SD (pass file=nullptr to omit). Returns the HTTP status code
// (0 if the connection itself failed) and, via responseBody, whatever the server sent back -
// the Image/Telemetry endpoints return the saved row plus its related Device, which is how
// config changes get back to the device (see applyDeviceConfigFromApiResponse) without a
// separate polling mechanism.
//
// Reuses `conn` across calls (see ApiConnection/connectApiClient()) rather than opening a fresh
// TCP+TLS connection every time - a full handshake is several seconds on this hardware, and with
// a backlog of hundreds of records that adds up to sync sessions long enough to be exposed to
// whatever's causing the connection drops seen in the field. Requesting keep-alive only works if
// we know exactly where the response body ends without relying on the connection closing, so this
// tracks Content-Length (or Transfer-Encoding: chunked, already framed unambiguously) - if a
// response has neither, or the server explicitly asks to close, or the body read comes up short,
// the connection is closed here rather than reused, and the next call just reconnects.
int postMultipartForm(ApiConnection &conn, const String &apiUrl, const String &endpoint, const std::vector<HttpFormField> &fields,
                       const String &fileFieldName, const String &fileName, File *file, String &responseBody)
{
    ParsedUrl api = parseApiUrl(apiUrl);
    String path = api.path;
    if (!path.endsWith("/")) {
        path += "/";
    }
    path += endpoint;

    String boundary = "----ESP32Boundary" + String((uint32_t)millis(), HEX);

    String startPart;
    for (const HttpFormField &field : fields) {
        startPart += "--" + boundary + "\r\n";
        startPart += "Content-Disposition: form-data; name=\"" + field.name + "\"\r\n\r\n";
        startPart += field.value + "\r\n";
    }

    size_t fileLength = 0;
    if (file != nullptr) {
        fileLength = file->size();
        startPart += "--" + boundary + "\r\n";
        startPart += "Content-Disposition: form-data; name=\"" + fileFieldName + "\"; filename=\"" + fileName + "\"\r\n";
        startPart += "Content-Type: image/jpeg\r\n\r\n";
    }
    String endPart = "\r\n--" + boundary + "--\r\n";

    size_t contentLength = startPart.length() + fileLength + endPart.length();

    logf("POST %s://%s:%u%s", api.https ? "https" : "http", api.host.c_str(), api.port, path.c_str());

    Client *clientPtr = connectApiClient(conn, api);
    if (clientPtr == nullptr) {
        logf("Connection to %s:%u failed", api.host.c_str(), api.port);
        return 0;
    }
    Client &client = *clientPtr;

    client.printf("POST %s HTTP/1.1\r\n", path.c_str());
    client.printf("Host: %s\r\n", api.host.c_str());
    client.printf("Content-Type: multipart/form-data; boundary=%s\r\n", boundary.c_str());
    client.printf("Content-Length: %u\r\n", (unsigned)contentLength);
    client.print("Connection: keep-alive\r\n\r\n");
    client.print(startPart);

    if (file != nullptr) {
        uint8_t buffer[1024];
        while (file->available()) {
            size_t len = file->read(buffer, sizeof(buffer));
            client.write(buffer, len);
        }
    }
    client.print(endPart);

    uint32_t start = millis();
    int statusCode = 0;
    bool chunked = false;
    long responseContentLength = -1;      // -1 means "not present in the headers"
    bool serverWantsClose = false;

    // Status line + headers
    while ((client.connected() || client.available()) && millis() - start < 15000) {
        if (!client.available()) {
            continue;
        }
        String line = client.readStringUntil('\n');
        if (line.startsWith("HTTP/1.1")) {
            statusCode = line.substring(9, 12).toInt();
        }
        // Header names are case-insensitive (RFC 7230) - Kestrel/Azure sends Transfer-Encoding
        // rather than Content-Length whenever it doesn't know the body size upfront, which is the
        // normal case for our controller responses; a fixed-size response (or an error page from
        // something in front of Kestrel) is more likely to carry a plain Content-Length instead.
        String lowerLine = line;
        lowerLine.toLowerCase();
        if (lowerLine.startsWith("transfer-encoding:") && lowerLine.indexOf("chunked") >= 0) {
            chunked = true;
        }
        if (lowerLine.startsWith("content-length:")) {
            responseContentLength = lowerLine.substring(15).toInt();
        }
        if (lowerLine.startsWith("connection:") && lowerLine.indexOf("close") >= 0) {
            serverWantsClose = true;
        }
        if (line == "\r") {
            break;   // blank line - end of headers, body follows
        }
    }

    // Body. Keeping the connection open afterward (see the end of this function) is only safe if
    // we know exactly where the body ends without relying on the connection closing to tell us -
    // chunked framing already carries that itself; otherwise Content-Length has to be there.
    responseBody = "";
    bool sawCompleteBody = true;
    if (chunked) {
        // Each chunk is "<hex size>\r\n<size bytes of data>\r\n", repeated until a zero-size
        // chunk terminates the body (optionally followed by trailer headers, then a final
        // blank line). Without this, the raw chunk framing (hex size lines, inter-chunk CRLFs)
        // ends up embedded in responseBody and breaks JSON parsing downstream.
        while (millis() - start < 15000) {
            if (!client.available() && !client.connected()) {
                sawCompleteBody = false;   // connection died mid-body
                break;
            }
            if (!client.available()) {
                continue;
            }
            String sizeLine = client.readStringUntil('\n');
            sizeLine.trim();
            int semicolon = sizeLine.indexOf(';');   // ignore chunk extensions, if any
            if (semicolon >= 0) {
                sizeLine = sizeLine.substring(0, semicolon);
            }
            long chunkSize = strtol(sizeLine.c_str(), nullptr, 16);
            if (chunkSize <= 0) {
                // Terminating "0" chunk - per RFC 7230, the chunked body doesn't actually end
                // until an optional trailer section and a final blank line have gone by too.
                // Leaving those unread was the bug: on Connection: close this didn't matter (the
                // socket just got torn down with them still sitting in it), but with the
                // connection now kept alive for reuse (see connectApiClient()), that leftover
                // blank line became the first thing the *next* request's header parser read,
                // which it mistook for headers already being over - hence a real 200 response
                // with a correct body getting logged as "status 0" with the whole raw response
                // (status line, headers and all) dumped into responseBody as if it were content.
                while (millis() - start < 15000) {
                    if (!client.available()) {
                        if (!client.connected()) {
                            break;
                        }
                        continue;
                    }
                    String trailerLine = client.readStringUntil('\n');
                    if (trailerLine == "\r" || trailerLine.length() == 0) {
                        break;   // blank line - chunked body is now fully consumed
                    }
                }
                break;   // terminating chunk
            }

            long readSoFar = 0;
            while (readSoFar < chunkSize && millis() - start < 15000) {
                if (!client.available()) {
                    continue;
                }
                responseBody += (char)client.read();
                readSoFar++;
            }
            client.readStringUntil('\n');   // consume the CRLF that follows each chunk's data
        }
    } else if (responseContentLength >= 0) {
        long readSoFar = 0;
        while (readSoFar < responseContentLength && millis() - start < 15000) {
            if (!client.available()) {
                if (!client.connected()) {
                    break;   // connection died mid-body
                }
                continue;
            }
            responseBody += (char)client.read();
            readSoFar++;
        }
        sawCompleteBody = (readSoFar >= responseContentLength);
    } else {
        // No framing info at all - the only way to know the body's finished is the connection
        // closing, which rules out reusing it afterward regardless of what the server asked for.
        while ((client.connected() || client.available()) && millis() - start < 15000) {
            if (client.available()) {
                responseBody += (char)client.read();
            }
        }
        serverWantsClose = true;
    }

    // Leave the connection open for the next call to reuse unless something says it shouldn't be:
    // the server asked to close, the body didn't frame itself unambiguously, we came up short
    // reading it, or we never even got a status line at all (statusCode == 0 - e.g. the request
    // timed out with no response, so the connection is left in an unknown state either way).
    if (serverWantsClose || !sawCompleteBody || statusCode == 0) {
        client.stop();
        conn.isOpen = false;
    }
    return statusCode;
}

// How many images (and, given three telemetry snapshots get written per capture cycle - see
// setup() - three times as many telemetry records) we'd expect to have piled up between one
// WiFi sync and the next, going by the configured capture/sync cadence alone. Used to spot a
// backlog that's grown far larger than that would ever produce (e.g. after being offline for a
// while) - see the "5x expected" checks in uploadPendingTelemetry()/uploadPendingImages() - so a
// single sync doesn't try to drain an enormous queue in one go, and instead keeps at it every
// cycle (see the didConnectWiFi handling in setup()) until it's caught up.
uint32_t expectedImagesPerSync(const DeviceConfig &config)
{
    if (config.cameraIntervalS == 0) {
        return 1;
    }
    uint32_t expected = config.autoSyncPeriodS / config.cameraIntervalS;
    return expected > 0 ? expected : 1;
}

// Uploads every telemetry file still sitting in TELEMETRY_DIR (including ones saved during
// earlier offline cycles), most recent first, deleting each one once the API acknowledges it
// with 200 OK - the API already has the authoritative copy, so there's no reason to keep a second
// one on the SD card taking up space and slowing down the next listFilesRecursive() walk. A
// validation error (400 - the request itself is bad, e.g. a field the API's model binder rejects)
// moves the file to TELEMETRY_HOLDING_DIR instead, so it stops clogging the queue but stays
// around to inspect, and processing continues with the rest of the backlog. Anything else
// (timeouts, connection failures, 5xx) is treated as transient - stops there, leaving that file
// and everything older still queued to retry next time. Also refreshes deviceConfig from the
// response, and reconciles counts.pendingTelemetry against what's actually on disk (see the
// comment at the bottom of this function).
//
// Returns true if it had to break off early because far more than expectedImagesPerSync()*3
// telemetry records have already gone out this cycle - see the didConnectWiFi handling in
// setup(), which uses this to keep reconnecting every cycle until the backlog is back to normal.
bool uploadPendingTelemetry(const String &deviceId, TelemetryCounts &counts, DeviceConfig &config)
{
    if (WiFi.status() != WL_CONNECTED) {
        return false;
    }

    // List pending files first and sort them, rather than uploading in whatever order the walk
    // happens to return. Paths are ".../yyyy/mm/dd/hh/yyyymmdd-hhmmss.json", so a lexical sort of
    // the full path is still a chronological sort - descending (rbegin/rend) puts the most recent
    // telemetry first, so the freshest data gets through even if the backlog is large enough to
    // hit the batch limit below.
    std::vector<String> filePaths;
    listFilesRecursive(TELEMETRY_DIR, filePaths);
    std::sort(filePaths.rbegin(), filePaths.rend());

    // *3 since three telemetry snapshots get written per capture cycle (see setup()).
    uint32_t expectedTelemetry = expectedImagesPerSync(config) * 3;
    bool backlogExcessive = false;

    // Reused across every request below (see ApiConnection/connectApiClient()) rather than
    // opening a fresh TCP+TLS connection per record.
    ApiConnection conn;

    int filesRemoved = 0;   // uploaded, empty, or unparseable - anything gone from disk afterwards
    int filesUploaded = 0;
    for (const String &filePath : filePaths) {
        // Process in batches of 100, same as the Pi's uploadPendingTelemetry()
        if (filesUploaded >= 100) {
            logLine("Hit upload batch limit - remaining telemetry will upload next cycle");
            break;
        }

        if ((uint32_t)filesUploaded >= expectedTelemetry * 5) {
            logf("Uploaded %d telemetry records this cycle - 5x the ~%u expected per sync - "
                 "stopping early, will keep syncing every cycle until the backlog is back to normal",
                 filesUploaded, expectedTelemetry);
            backlogExcessive = true;
            break;
        }

        File file = SD.open(filePath, "r");
        if (!file) {
            continue;
        }

        if (file.size() == 0) {
            logLine("Empty file - deleting " + filePath);
            file.close();
            SD.remove(filePath);
            filesRemoved++;
            continue;
        }

        DynamicJsonDocument doc(400);
        DeserializationError err = deserializeJson(doc, file);
        file.close();

        if (err) {
            logf("Failed to parse %s: %s - discarding", filePath.c_str(), err.c_str());
            SD.remove(filePath);
            filesRemoved++;
            continue;
        }

        // getISO8601Timestamp() (see buildTelemetryJson()) writes "unknown" here if the clock
        // wasn't synced yet at capture time - the API rejects that outright, so there's no point
        // ever attempting this upload. Discard it now rather than let it fail (and block every
        // older record behind it, since uploads run most-recent-first) every single cycle.
        if (doc["timestamp"].as<String>() == "unknown") {
            logLine("Unknown capture time - deleting " + filePath);
            SD.remove(filePath);
            filesRemoved++;
            continue;
        }

        // Packs the fields that don't have a place of their own in TelemetryPostModel into
        // Status, the same way the Pi's saveTelemetry.py packs PiJuice detail in there. geo.lat/
        // geo.lon/geo.time-recorded come from THIS record's own geoLat/geoLon/geoTimeRecorded
        // (see buildTelemetryJson()) rather than the live `config` - a moving camera can capture
        // several telemetry records between GPS fixes and between uploads, so each one needs to
        // report the position it actually had at capture time, not whatever's current by the
        // time the backlog gets uploaded.
        // geoLat/geoLon fall back to 0 automatically (ArduinoJson's .as<double>() on a missing
        // key) for telemetry files backlogged from before geo tracking was added; geoTimeRecorded
        // needs an explicit isNull() guard, same as uptimeSeconds above, since .as<String>() on a
        // missing key would otherwise post the literal string "null".
        char status[256];
        snprintf(status, sizeof(status),
                 "{\"boot_count\":%d,\"voltage_mv\":%u,\"solar_voltage_mv\":%u,"
                 "\"geo.lat\":%.6f,\"geo.lon\":%.6f,\"geo.time-recorded\":\"%s\"}",
                 doc["boot_count"].as<int>(), doc["voltage_mv"].as<unsigned>(), doc["solar_voltage_mv"].as<unsigned>(),
                 doc["geoLat"].as<double>(), doc["geoLon"].as<double>(),
                 doc["geoTimeRecorded"].isNull() ? "" : doc["geoTimeRecorded"].as<String>().c_str());

        std::vector<HttpFormField> fields = {
            {"SerialNumber", deviceId},
            {"Timestamp", doc["timestamp"].as<String>()},
            // Defensively rounds rather than trusting the file to already hold an int - older
            // backlog files written before temperatureC was rounded at capture time may still
            // have a decimal value on disk, and TemperatureC is an int column server-side.
            {"TemperatureC", String((int)lroundf(doc["temperatureC"].as<float>()))},
            {"BatteryPercent", doc["batteryPercent"].as<String>()},
            // {"BatteryVoltage", doc["voltage_mv"].as<String>()},
            {"Status", String(status)},
            // isNull() guard: telemetry files already backlogged from before uptimeSeconds was
            // added to buildTelemetryJson() won't have the key at all - without this they'd post
            // the literal string "null" instead of a number.
            {"UptimeSeconds", doc["uptimeSeconds"].isNull() ? String("0") : doc["uptimeSeconds"].as<String>()},
            {"PendingImages", doc["pendingImages"].as<String>()},
            {"PendingTelemetry", doc["pendingTelemetry"].as<String>()},
        };

        logf("Posting Telemetry: %s", fieldsToJson(fields).c_str());

        String responseBody;
        int statusCode = postMultipartForm(conn, config.apiUrl, "Telemetry", fields, "", "", nullptr, responseBody);

        logf("Telemetry response (status %d): %s", statusCode, responseBody.c_str());

        if (statusCode == 200) {
            SD.remove(filePath);
            filesRemoved++;
            filesUploaded++;
            applyDeviceConfigFromApiResponse(responseBody, config);
            logf("Uploaded and deleted %s", filePath.c_str());
        } else if (statusCode == 400) {
            // The API rejected the request itself - retrying won't help, and leaving it in place
            // would just block every older (now-behind-it, since most-recent-first) record from
            // ever being tried. Move it aside and keep going rather than losing the rest of the
            // backlog over one bad record.
            String relativePath = filePath.substring(String(TELEMETRY_DIR).length());
            String holdingPath = String(TELEMETRY_HOLDING_DIR) + relativePath;
            ensureDirExists(parentDir(holdingPath));
            SD.rename(filePath, holdingPath);
            filesRemoved++;
            logf("Rejected (400) - moved %s to %s for inspection", filePath.c_str(), holdingPath.c_str());
        } else {
            // Anything else (timeout, connection failure, 5xx) is treated as transient - stop
            // here and leave this file (and everything older, still unprocessed) queued to retry
            // next cycle rather than risk discarding something that might actually go through
            // later.
            logf("Failed to upload %s (status %d), will retry next time", filePath.c_str(), statusCode);
            break;
        }
    }

    // counts.pendingTelemetry is normally just cheap ++/-- bookkeeping (see the increment in
    // setup(), done without touching the SD card so a telemetry snapshot can be written quickly
    // every wake) and drifts over time - e.g. the empty/unparseable-file branches above delete a
    // file without ever having decremented it. listFilesRecursive() above already did a full,
    // authoritative scan of what's really pending, so this is the natural place to correct that
    // drift, without needing a second scan just to do it.
    counts.pendingTelemetry = (int)filePaths.size() - filesRemoved;
    conn.secureClient.stop();
    conn.plainClient.stop();
    return backlogExcessive;
}

// Uploads every image still sitting in CAMERA_DIR, oldest first, over HTTP to the same API
// endpoint the Raspberry Pi units already post to - imagery isn't published over MQTT, since
// message brokers aren't a great fit for large binary payloads, especially once this moves to
// cellular. Deletes each file once the API acknowledges it with 200 OK - the API already has the
// authoritative copy; stops at the first failure so a flaky connection doesn't reorder the
// backlog (same approach as uploadPendingTelemetry(), including reconciling counts.pendingImages
// against what's actually on disk - see the comment at the bottom of this function).
//
// Returns true if it had to break off early because far more than expectedImagesPerSync()*5
// images have already gone out this cycle - see the didConnectWiFi handling in setup().
bool uploadPendingImages(const String &deviceId, TelemetryCounts &counts, DeviceConfig &config)
{
    if (WiFi.status() != WL_CONNECTED) {
        return false;
    }

    std::vector<String> filePaths;
    listFilesRecursive(CAMERA_DIR, filePaths);
    std::sort(filePaths.rbegin(), filePaths.rend());

    uint32_t expectedImages = expectedImagesPerSync(config);
    bool backlogExcessive = false;

    // Reused across every request below (see ApiConnection/connectApiClient()) rather than
    // opening a fresh TCP+TLS connection per image.
    ApiConnection conn;

    int filesRemoved = 0;   // uploaded or empty - anything gone from disk afterwards
    int filesUploaded = 0;
    for (const String &filePath : filePaths) {
        // Process in batches of 10, same as the Pi's uploadPendingPhotos()
        if (filesUploaded >= 10) {
            logLine("Hit upload batch limit - remaining images will upload next cycle");
            break;
        }

        if ((uint32_t)filesUploaded >= expectedImages * 5) {
            logf("Uploaded %d images this cycle - 5x the ~%u expected per sync - stopping early, "
                 "will keep syncing every cycle until the backlog is back to normal",
                 filesUploaded, expectedImages);
            backlogExcessive = true;
            break;
        }

        File file = SD.open(filePath, "r");
        if (!file) {
            continue;
        }

        if (file.size() == 0) {
            logLine("Empty file - deleting " + filePath);
            file.close();
            SD.remove(filePath);
            filesRemoved++;
            continue;
        }

        String timestamp = parseTimestampFromPath(filePath);
        if (timestamp == "unknown") {
            // The clock wasn't synced yet when this was captured (see getDatedPath()'s
            // "unsynced/boot-N" fallback naming, which parseTimestampFromPath() can't recover a
            // real timestamp from) - the API rejects an "unknown" Timestamp outright, so there's
            // no point ever attempting this upload. Discard it now rather than let it fail (and
            // block every older image behind it, since uploads run most-recent-first) every
            // single cycle.
            logLine("Unknown capture time - deleting " + filePath);
            file.close();
            SD.remove(filePath);
            filesRemoved++;
            continue;
        }

        std::vector<HttpFormField> fields = {
            {"SerialNumber", deviceId},
            {"Timestamp", timestamp},
        };

        logf("Posting Image: %s", fieldsToJson(fields).c_str());

        String responseBody;
        int statusCode = postMultipartForm(conn, config.apiUrl, "Image", fields, "File", fileBaseName(filePath), &file, responseBody);
        file.close();

        logf("Image response (status %d): %s", statusCode, responseBody.c_str());

        if (statusCode == 200) {
            SD.remove(filePath);
            filesRemoved++;
            filesUploaded++;
            applyDeviceConfigFromApiResponse(responseBody, config);
            logf("Uploaded and deleted %s", filePath.c_str());
        } else {
            logf("Failed to upload %s (status %d), will retry next time", filePath.c_str(), statusCode);
            break;
        }
    }

    // See the matching comment in uploadPendingTelemetry() - reconciles counts.pendingImages
    // against the authoritative scan above rather than trusting the cheap ++/-- bookkeeping.
    counts.pendingImages = (int)filePaths.size() - filesRemoved;
    conn.secureClient.stop();
    conn.plainClient.stop();
    return backlogExcessive;
}

// --- Sunrise/sunset calculation -------------------------------------------------------------
// Used by isNightForExposure() below to decide when to switch the camera into its long-exposure
// setup, based on where the sun actually is rather than a fixed clock-hour boundary (see the
// git history of isCurrentlyNight() - a boundary compared in the wrong timezone once caused this
// firmware to apply *daytime* exposure settings right at real dusk, and reliably failed captures
// for the ~40 minutes until it was next disturbed). Ported from the public-domain solar position
// formulas (Jean Meeus, "Astronomical Algorithms") as popularised by mourner/suncalc.js
// (https://github.com/mourner/suncalc) - the same maths behind the Pi units' `suncalc` Python
// package (see scripts/helpers.py's currentPhase()), just the sunrise/sunset pair rather than
// suncalc's full set of twilight phases - see isNightForExposure()'s comment on why only two.
namespace SunCalc {
constexpr double RAD = 3.14159265358979323846 / 180.0;
constexpr double J1970 = 2440588.0;
constexpr double J2000 = 2451545.0;
constexpr double OBLIQUITY = RAD * 23.4397;   // Earth's axial tilt
constexpr double J0 = 0.0009;

double toDays(time_t utc)
{
    return ((double)utc / 86400.0 - 0.5 + J1970) - J2000;
}

time_t fromJulian(double j)
{
    return (time_t)((j + 0.5 - J1970) * 86400.0);
}

double solarMeanAnomaly(double d) { return RAD * (357.5291 + 0.98560028 * d); }

double eclipticLongitude(double m)
{
    double c = RAD * (1.9148 * sin(m) + 0.02 * sin(2 * m) + 0.0003 * sin(3 * m));  // equation of center
    double p = RAD * 102.9372;                                                     // perihelion of the Earth
    return m + c + p + 3.14159265358979323846;
}

double declination(double l) { return asin(sin(l) * sin(OBLIQUITY)); }   // ecliptic latitude of the sun is ~0
double julianCycle(double d, double lw) { return round(d - J0 - lw / (2 * 3.14159265358979323846)); }
double approxTransit(double ht, double lw, double n) { return J0 + (ht + lw) / (2 * 3.14159265358979323846) + n; }
double solarTransitJ(double ds, double m, double l) { return J2000 + ds + 0.0053 * sin(m) - 0.0069 * sin(2 * l); }

double hourAngle(double h, double phi, double d)
{
    double cosH = (sin(h) - sin(phi) * sin(d)) / (cos(phi) * cos(d));
    if (cosH < -1) cosH = -1;   // sun never sets this day (polar summer) - treat as "always up"
    if (cosH > 1) cosH = 1;     // sun never rises this day (polar winter) - treat as "always down"
    return acos(cosH);
}

// Sunrise/sunset (UTC) for the solar day containing `dayUTC` - pass any instant within the UTC
// calendar day you want (this file always passes local noon-ish of that day, see below, to stay
// well clear of the edges).
void sunriseSunset(time_t dayUTC, double latDeg, double lonDeg, time_t &sunriseOut, time_t &sunsetOut)
{
    double lw = RAD * -lonDeg;
    double phi = RAD * latDeg;
    double d = toDays(dayUTC);
    double n = julianCycle(d, lw);
    double ds = approxTransit(0, lw, n);
    double m = solarMeanAnomaly(ds);
    double l = eclipticLongitude(m);
    double dec = declination(l);
    double jNoon = solarTransitJ(ds, m, l);

    double h0 = -0.833 * RAD;   // standard sunrise/sunset angle - accounts for atmospheric refraction + solar radius
    double w0 = hourAngle(h0, phi, dec);
    double a0 = approxTransit(w0, lw, n);
    double jSet = solarTransitJ(a0, m, l);
    double jRise = jNoon - (jSet - jNoon);

    sunriseOut = fromJulian(jRise);
    sunsetOut = fromJulian(jSet);
}
}   // namespace SunCalc

// Whether `nowUTC` falls at night for camera-exposure purposes - i.e. anywhere between a sunset
// and the following sunrise - at (latDeg, lonDeg). Uses DEFAULT_LAT/DEFAULT_LON (Wellington) if
// both are still 0 (no GPS fix yet - see updateGeoLocationIfDue()).
//
// Deliberately checks against the wider sunset/sunrise window rather than a narrower civil-
// twilight one (dusk/dawn, ~20-30 min inside it): the OV5640 lever this firmware has for "night"
// is a coarse on/off switch (see setupCameraNightExposure()), and running it a little longer than
// strictly necessary around the edges of twilight is harmless - it was already confirmed working
// fine under full daylight (see runWakeCycle()'s "nightLongExposure" comment history) - whereas
// running normal daytime auto-exposure a little into real dusk/dawn is exactly what caused the
// failures this replaces. Only two tiers (unlike the Pi's multi-phase dawn/dusk/nautical/night
// ladder - see scripts/helpers.py's currentPhase()) because the OV5640 driver only gives us one
// lever to pull, not the Pi's continuously-variable ExposureTime.
//
// Checks yesterday/today/tomorrow's UTC-calendar-day sunset/sunrise (not just "today's") so a
// local sunset/sunrise landing on a different UTC calendar date than `nowUTC` itself - as it does
// for most of the day, this far from UTC - still gets caught correctly.
bool isNightForExposure(time_t nowUTC, double latDeg, double lonDeg)
{
    if (latDeg == 0 && lonDeg == 0) {
        latDeg = DEFAULT_LAT;
        lonDeg = DEFAULT_LON;
    }

    time_t todayNoonUTC = (nowUTC / 86400) * 86400 + 43200;
    time_t sunriseYesterday, sunsetYesterday;
    time_t sunriseToday, sunsetToday;
    time_t sunriseTomorrow, sunsetTomorrow;
    SunCalc::sunriseSunset(todayNoonUTC - 86400, latDeg, lonDeg, sunriseYesterday, sunsetYesterday);
    SunCalc::sunriseSunset(todayNoonUTC, latDeg, lonDeg, sunriseToday, sunsetToday);
    SunCalc::sunriseSunset(todayNoonUTC + 86400, latDeg, lonDeg, sunriseTomorrow, sunsetTomorrow);

    if (nowUTC >= sunsetYesterday && nowUTC < sunriseToday) return true;
    if (nowUTC >= sunsetToday && nowUTC < sunriseTomorrow) return true;
    return false;
}

// --- Fixed-offset local time --------------------------------------------------------------
// Only used for daytimeStartsAtH/daytimeEndsAtH (see isOutsideWorkingHours()) - everything else
// in this firmware (logs, ISO8601 fields, isNightForExposure() above) deliberately stays in UTC.
// mktime()/gmtime_r() below are used purely as epoch<->calendar-field converters, not for their
// usual local-timezone behaviour - this firmware never sets a timezone (GMT_OFFSET_SEC/
// DAY_LIGHT_OFFSET_SEC are left at 0), so the C library treats both as UTC, which is exactly what
// lets the manual `+ utcOffsetMinutes` shift below stand in for a real timezone conversion.

// UTC time_t -> wall-clock fields at `utcOffsetMinutes`.
void utcToLocalTm(time_t utc, int utcOffsetMinutes, struct tm &out)
{
    time_t shifted = utc + (time_t)utcOffsetMinutes * 60;
    gmtime_r(&shifted, &out);
}

// Inverse of utcToLocalTm() - interprets `local` as wall-clock time at `utcOffsetMinutes` and
// returns the equivalent UTC time_t.
time_t localTmToUtc(struct tm local, int utcOffsetMinutes)
{
    return mktime(&local) - (time_t)utcOffsetMinutes * 60;
}

// Whether `nowUTC` falls outside the device's configured working-day window
// (daytimeStartsAtH/daytimeEndsAtH, in local time - see utcOffsetMinutes) - used only by
// computeSleepSeconds() to decide whether to sleep straight through rather than waking every
// cameraIntervalS. This is a fixed clock-hour schedule (when a technician/animal/etc. is likely
// to be around), not actual daylight - see isNightForExposure() for that. Returns false - i.e.
// "assume within hours" - if the clock was never synced, same as computeSleepSeconds() falling
// back to its normal behaviour in that case.
bool isOutsideWorkingHours(time_t nowUTC, const DeviceConfig &config)
{
    struct tm local;
    utcToLocalTm(nowUTC, config.utcOffsetMinutes, local);
    return (local.tm_hour >= config.daytimeEndsAtH) || (local.tm_hour < config.daytimeStartsAtH);
}

// Decides how long to deep-sleep for, in seconds. Normally just config.cameraIntervalS, but
// when sleep_during_night is enabled and it's currently outside the working-day window, sleeps
// until daytimeStartsAtH (local time) instead of waking every cameraIntervalS overnight -
// capped at NIGHT_CHECKIN_INTERVAL_S so the unit still wakes roughly hourly through the night
// to check in (telemetry/config/OTA), just without the usual captures.
uint32_t computeSleepSeconds(const DeviceConfig &config)
{
    // Floored by battery level - see applyLowBatteryIntervalFloor() - so a low battery still gets
    // captures/telemetry, just less often, rather than running at the configured interval right
    // down to BATTERY_VOLTAGE_LOW.
    uint32_t intervalS = applyLowBatteryIntervalFloor(config.cameraIntervalS, get_battery_percent(get_battery_voltage()));

    if (!config.sleepDuringNight) {
        return intervalS;
    }

    struct tm timeinfo;
    if (!getLocalTime(&timeinfo, 0)) {
        // No synced clock to judge the working-day window from - fall back to the regular interval
        return intervalS;
    }

    time_t nowUTC = time(nullptr);
    if (!isOutsideWorkingHours(nowUTC, config)) {
        return intervalS;
    }

    struct tm wakeLocal;
    utcToLocalTm(nowUTC, config.utcOffsetMinutes, wakeLocal);
    wakeLocal.tm_hour = config.daytimeStartsAtH;
    wakeLocal.tm_min = 0;
    wakeLocal.tm_sec = 0;

    time_t wakeUTC = localTmToUtc(wakeLocal, config.utcOffsetMinutes);
    if (wakeUTC <= nowUTC) {
        wakeUTC += 24 * 60 * 60;
    }

    uint32_t untilMorningS = (uint32_t)difftime(wakeUTC, nowUTC);

    // Wake roughly hourly through the night to check in rather than sleeping straight through -
    // but never more often than the battery-level floor already wants (intervalS), so a low
    // battery still wins.
    uint32_t checkinS = (intervalS > NIGHT_CHECKIN_INTERVAL_S) ? intervalS : NIGHT_CHECKIN_INTERVAL_S;
    if (untilMorningS > checkinS) {
        logf("Night mode - sleeping %u seconds (check-in; %02d:00 local still %u s away)", checkinS, config.daytimeStartsAtH, untilMorningS);
        return checkinS;
    }
    logf("Night mode - sleeping %u seconds until %02d:00 local (UTC%+d min)", untilMorningS, config.daytimeStartsAtH, config.utcOffsetMinutes);
    return untilMorningS;
}

// Powers on the SIM7670G's modem/GNSS chip (a separate radio from the ESP32's own WiFi, which is
// still what uploads go out over), waits for a GPS fix, and updates
// config.geoLat/geoLon/geoTimeRecorded - but only once config.geoIntervalS has actually elapsed
// since the last fix (or none has ever been recorded). Persists straight to CONFIG_FILE on
// success, independent of the API-driven config fields (see applyDeviceConfigFromApiResponse) -
// a GPS fix is determined locally and the API has no way to hand one back.
//
// Bounded by GEO_FIX_TIMEOUT_MS throughout, so a unit with poor sky visibility (or none, e.g.
// deployed indoors during testing) can't block a whole wake cycle indefinitely - it just keeps
// the last known position and tries again next time this interval elapses.
void updateGeoLocationIfDue(DeviceConfig &config)
{
    time_t now = time(nullptr);
    time_t lastFix = parseISO8601Timestamp(config.geoTimeRecorded);
    if (config.geoIntervalS == 0 || lastFix != 0 && now >= lastFix && (uint32_t)(now - lastFix) < config.geoIntervalS) {
        return;   // not due yet
    }

    logLine("Checking GPS position...");
    SerialAT.begin(MODEM_BAUDRATE, SERIAL_8N1, MODEM_RX_PIN, MODEM_TX_PIN);

    pinMode(BOARD_PWRKEY_PIN, OUTPUT);
    digitalWrite(BOARD_PWRKEY_PIN, LOW);
    delay(100);
    digitalWrite(BOARD_PWRKEY_PIN, HIGH);
    delay(MODEM_POWERON_PULSE_WIDTH_MS);
    digitalWrite(BOARD_PWRKEY_PIN, LOW);

    uint32_t start = millis();
    int retry = 0;
    while (!modem.testAT(1000)) {
        if (millis() - start > GEO_FIX_TIMEOUT_MS) {
            logLine("Modem never responded to AT - giving up on this cycle's GPS fix");
            return;
        }
        if (++retry > GEO_MODEM_BOOT_RETRIES) {
            logLine("Modem not responding yet - re-pulsing PWRKEY");
            digitalWrite(BOARD_PWRKEY_PIN, LOW);
            delay(100);
            digitalWrite(BOARD_PWRKEY_PIN, HIGH);
            delay(MODEM_POWERON_PULSE_WIDTH_MS);
            digitalWrite(BOARD_PWRKEY_PIN, LOW);
            retry = 0;
        }
    }

    bool gpsEnabled = false;
    while (millis() - start < GEO_FIX_TIMEOUT_MS) {
        if (modem.enableGPS(MODEM_GPS_ENABLE_GPIO, MODEM_GPS_ENABLE_LEVEL)) {
            gpsEnabled = true;
            break;
        }
        delay(500);
    }

    bool haveFix = false;
    GPSInfo info;
    if (gpsEnabled) {
        while (millis() - start < GEO_FIX_TIMEOUT_MS) {
            if (modem.getGPS_Ex(info)) {
                haveFix = true;

                // logf()'s first argument is a printf format string, not a value to print - each
                // of these used to pass a bare number (e.g. logf(info.isFix)) with nothing to
                // format it, which is why every one of these came out blank in the log: with no
                // %-specifier in "the format string" (actually just info.isFix's numeric value
                // reinterpreted as a pointer), there's no text to print at all.
                logf("GPS fix: mode=%u lat=%.6f lon=%.6f speed=%.2f alt=%.2f course=%.2f",
                     info.isFix, info.latitude, info.longitude, info.speed, info.altitude, info.course);
                logf("GPS satellites: gps=%u beidou=%u glonass=%u galileo=%u",
                     info.gps_satellite_num, info.beidou_satellite_num, info.glonass_satellite_num, info.galileo_satellite_num);
                logf("GPS fix time: %04u-%02u-%02u %02u:%02u:%02u UTC",
                     info.year, info.month, info.day, info.hour, info.minute, info.second);
                logf("GPS precision: PDOP=%.2f HDOP=%.2f VDOP=%.2f", info.PDOP, info.HDOP, info.VDOP);
                logf("GPS raw +CGNSSINFO: %s", modem.getGPSraw().c_str());

                break;
            }
            delay(2000);
        }
    } else {
        logLine("Failed to enable GPS - giving up on this cycle's GPS fix");
    }

    if (haveFix) {
        // NOT converted from ddmm.mmmmmm here, despite what an earlier version of this function
        // (and TinyGsmClientSIM7672.h's own source comments) assumed - real hardware output
        // confirms this modem's +CGNSSINFO already reports plain decimal degrees. A longitude
        // of 174.900441 is the giveaway: as ddmm.mmmmmm that would mean 90.0441 minutes, which
        // isn't valid (minutes only go up to 59.999...). Converting it anyway is exactly what
        // produced the garbage -0.685311/2.248342 previously logged.
        config.geoLat = info.latitude;
        config.geoLon = info.longitude;
        config.geoTimeRecorded = getISO8601Timestamp();
        writeDeviceConfig(config);
        logf("GPS fix recorded: %.6f, %.6f", config.geoLat, config.geoLon);
    } else if (gpsEnabled) {
        logLine("No GPS fix within timeout - keeping last known location");
    }

    // AT+CPOF cleanly powers the whole modem down (documented SIMCom behaviour) rather than
    // fumbling with PWRKEY pulse timing again, which differs between power-on and power-off and
    // isn't worth the risk of getting wrong on hardware this sketch can't test against. Sent
    // best-effort, without waiting on/parsing a response - the ESP32 is about to deep-sleep
    // regardless, at which point SerialAT goes away either way.
    modem.disableGPS(MODEM_GPS_ENABLE_GPIO, 0);
    SerialAT.println("AT+CPOF");
    delay(2000);
}

void setup()
{
    Serial.begin(115200);

    Serial.println();

    // Warnings/errors only (see fileLogVprintf()'s comment) - keeps this to the diagnostic
    // messages worth persisting rather than every library's routine INFO/DEBUG chatter.
    esp_log_level_set("*", ESP_LOG_WARN);
    esp_log_set_vprintf(fileLogVprintf);

    //Increment boot number and print it every reboot
    ++bootCount;
    logLine("Boot number: " + String(bootCount));


#ifdef ENABLE_BATTERY_MON
    int tryCount = 4;
    while (tryCount--) {
        if (get_battery_voltage() < (BATTERY_VOLTAGE_LOW + 100)) {
            set_device_to_sleep();
        } else {
            logLine("Battery voltage is normal");
        }
        delay(500);
    }
#endif

    runWakeCycle();

    // Fresh install only (see SETUP_AP_MAX_BOOT_COUNT/runSetupApWindow()) - gives an installer a
    // window to confirm the camera's working, right there in the field, with no cellular/internet
    // WiFi needed for it.
    if (bootCount <= SETUP_AP_MAX_BOOT_COUNT) {
        runSetupApWindow();
    }
}

// Switches the sensor from auto exposure to a fixed exposure pinned at its maximum - the closest
// the OV5640 driver gets to the Raspberry Pi units' camera.enable_long_exposure_at_night /
// ExposureTime (see scripts/savePhotos.py). Only called from runWakeCycle() when
// deviceConfig.enableLongExposureAtNight is set, it's night (see isNightForExposure()), and the
// detected sensor is OV5640.
//
// How this differs from the Pi: libcamera lets Picamera2 ask the IMX708 for an explicit
// ExposureTime in microseconds, up to several seconds. The esp32-camera OV5640 driver has no such
// control - sensor->set_aec_value() only accepts a value up to the sensor's *current* frame
// length in lines (register 0x380e/0x380f, read internally - see set_aec_value() in the
// component's ov5640.c), i.e. at most one frame period. The only lever the driver's public
// sensor_t API gives us to stretch that ceiling is to slow the whole pixel clock down by lowering
// camera_config_t.xclk_freq_hz before esp_camera_init() - set_framesize() in ov5640.c derives
// every PLL register from sensor->xclk_freq_hz, so a lower xclk means a proportionally longer
// wall-clock duration per frame/line even though the line-count ceiling itself doesn't change.
// runWakeCycle() already switches config.xclk_freq_hz to deviceConfig.longExposureXclkHz before
// esp_camera_init() for this reason (see nightLongExposure there) - this function just:
//   1. Turns auto exposure off (set_exposure_ctrl(0)) so the sensor's own AEC doesn't fight the
//      manual value set below.
//   2. Pins aec_value to the sensor's maximum for whatever frame length is currently configured -
//      set_aec_value() clamps internally to that ceiling, so passing a deliberately oversized
//      value is enough; there's no need to separately read the real ceiling back out first.
// Gain is left on auto (set_gain_ctrl(1), set further up in runWakeCycle) so AGC can still
// brighten the image on top of the maxed-out exposure if that alone isn't enough - as close as
// this hardware gets to the Pi's paired ExposureTime/AnalogueGain.
//
// OV2640 has no comparable lever - its set_aec_value() ceiling is a hardcoded 1200, unrelated to
// frame timing (see ov2640.c) - so runWakeCycle() never calls this for a detected/configured
// OV2640; enableLongExposureAtNight is simply a no-op there.
//
// NOT verified against real hardware: DEFAULT_LONG_EXPOSURE_XCLK_HZ was picked from the OV5640
// driver's own PLL math (see that macro's comment), not measured on a device, and the OV5640
// datasheet's PLL/VCO valid ranges aren't enforced anywhere in this driver - an unsuitable
// longExposureXclkHz could plausibly stop the sensor's PLL from locking rather than just slowing
// it down. Confirm actual exposure time and image quality on a real unit before enabling this
// fleet-wide, and retune deviceConfig.longExposureXclkHz (config.json, no reflash needed) if it
// needs adjusting.
void setupCameraNightExposure(sensor_t *s)
{
    s->set_exposure_ctrl(s, 0);    // manual exposure - see comment above

    // Read VTS (frame length, lines) ourselves via the generic register accessor the driver
    // exposes on sensor_t (get_reg with a >0xFF mask reads a 16-bit register - see get_reg() in
    // ov5640.c), so we can back off NIGHT_EXPOSURE_MARGIN_LINES from it - see that macro's
    // comment for why pinning to the literal ceiling isn't safe.
    int vtsLines = s->get_reg(s, 0x380e, 0xFFFF);
    int aecTarget = (vtsLines > NIGHT_EXPOSURE_MARGIN_LINES) ? (vtsLines - NIGHT_EXPOSURE_MARGIN_LINES) : 0xFFFF;
    s->set_aec_value(s, aecTarget);   // set_aec_value() still clamps internally as a backstop
    logf("Night long exposure active: xclk=%dHz, VTS=%d, AEC=%d", s->xclk_freq_hz, vtsLines, aecTarget);
}

// Everything a single wake cycle does: bring the camera up, capture, take/refresh a GPS fix,
// build+upload telemetry, upload any backlogged images, then power the camera back down. This
// is the whole of what used to be setup() below the boot-once battery check above - normally
// that's fine to run only once, since deep sleep resets the chip back through setup() on every
// real wake anyway. Support mode changes that: it keeps the chip running instead of sleeping, so
// loop() calls this again itself, repeatedly, to keep imagery/telemetry flowing meanwhile.
void runWakeCycle()
{
    // Turn on the camera power
    if (!setCameraPower(true)) {
        logLine("Failed to initialize Camera power chip!"); return;
    }

    // Initialize sd card
    if (!setupSD()) {
        logLine("Failed to initialize SD card! Please check SD card!"); return;
    }

    // Logged here (once SD/sdReady is up, see logLine()) rather than at the very top of setup() -
    // esp_reset_reason() itself doesn't need that, but landing this in envirocam.log rather than
    // only Serial is the whole point (see resetReasonName()'s comment). A brownout/watchdog/panic
    // reset happens before app code runs, so this is the only trace of *why* such a reset
    // happened - bootCount (RTC_DATA_ATTR) just tells us one occurred, via dropping back down.
    logf("Reset reason: %s", resetReasonName(esp_reset_reason()));

    // Kept up to date incrementally below rather than re-scanned from disk each wake,
    // since scanning directories with thousands of backlogged files gets slow
    TelemetryCounts counts = readCounts();

    // Reloaded fresh every boot - deep sleep doesn't preserve normal RAM - then refreshed
    // from the API response further down, once WiFi is up.
    deviceConfig = readDeviceConfig();

    // Deep sleep keeps the RTC running, so the clock usually survives between wake-ups.
    // Only reconnect to WiFi if the clock looks like it was reset by a power interruption
    // (i.e. it's now earlier than the last time we successfully synced), if it's been longer
    // than deviceConfig.autoSyncPeriodS since the last sync (to correct clock drift), or if a
    // previous cycle set FORCE_SYNC_FILE because the backlog was too big to clear in one sync
    // (see the "5x expected" checks in uploadPendingTelemetry()/uploadPendingImages(), and how
    // the flag gets set/cleared further down). This is also the only time telemetry/images
    // actually get uploaded (see uploadPendingTelemetry/uploadPendingImages's WiFi.status()
    // check) - so with cameraIntervalS shorter than autoSyncPeriodS, several captures can build
    // up between uploads. Support mode always needs a sync - the whole point is imagery/
    // telemetry flowing every cycle rather than waiting out the normal autoSyncPeriodS.
    time_t lastSyncTime = readLastSyncTime();
    time_t now = time(nullptr);

    // With sleepDuringNight enabled, computeSleepSeconds() only lets the device wake roughly hourly
    // through the night (NIGHT_CHECKIN_INTERVAL_S) rather than every cameraIntervalS. Treat each of
    // those overnight wakes as needing a sync, so telemetry/config/OTA still round-trip every hour
    // overnight instead of waiting out autoSyncPeriodS - the capture itself still happens either
    // way. Gated on a synced clock (same as computeSleepSeconds()), since isOutsideWorkingHours()
    // is only meaningful then.
    struct tm nightCheckTm;
    bool nightCheckin = deviceConfig.sleepDuringNight && getLocalTime(&nightCheckTm, 0)
                          && isOutsideWorkingHours(now, deviceConfig);

    bool needsSync = (lastSyncTime == 0) || (now < lastSyncTime) || (now - lastSyncTime >= deviceConfig.autoSyncPeriodS)
                      || readForceSyncFlag() || deviceConfig.supportMode || nightCheckin;
    bool didConnectWiFi = false;
    if (needsSync) {
        logLine("Clock needs sync, connecting to WiFi...");
        didConnectWiFi = connectWiFiAndSyncTime();
        if (!didConnectWiFi) {
            logLine("Continuing without synced time, filenames will use boot count!");
        }
    } else {
        logLine("Clock already synced, skipping WiFi connection");
    }
    lastWakeConnectedWiFi = didConnectWiFi;

    // See setupCameraNightExposure() below - decided here, ahead of camera_config_t, because
    // xclk_freq_hz has to be picked before esp_camera_init() (it can't be changed afterwards
    // without a full re-init). deviceConfig.cameraModel is last boot's PID detection (or the
    // "OV5640" default before any detection has run) - good enough to avoid slowing an OV2640's
    // clock for no benefit (see setupCameraNightExposure()'s comment on why OV2640 can't use
    // this), without waiting on this boot's own detection which only happens after init.
    //
    // isNightForExposure() uses actual sunrise/sunset (deviceConfig.geoLat/geoLon, or Wellington
    // until a GPS fix lands) rather than daytimeStartsAtH/daytimeEndsAtH - those are a fixed
    // working-day schedule for computeSleepSeconds() (see isOutsideWorkingHours()), not sunlight,
    // and a fixed clock-hour boundary drifts against real dusk/dawn by up to a couple of hours
    // across the seasons - see isNightForExposure()'s comment for how that already bit this
    // firmware once.
    bool nightLongExposure = deviceConfig.enableLongExposureAtNight
                              && deviceConfig.cameraModel != "OV2640"
                              && isNightForExposure(time(nullptr), deviceConfig.geoLat, deviceConfig.geoLon);

    // Enable / disable power save mode (1 disabled, 0 enabled)
    pinMode(BOARD_POWER_SAVE_MODE_PIN, OUTPUT);
    digitalWrite(BOARD_POWER_SAVE_MODE_PIN, HIGH);

    camera_config_t config;
    config.ledc_channel = LEDC_CHANNEL_0;
    config.ledc_timer = LEDC_TIMER_0;
    config.pin_d0 = CAMERA_Y2_PIN;
    config.pin_d1 = CAMERA_Y3_PIN;
    config.pin_d2 = CAMERA_Y4_PIN;
    config.pin_d3 = CAMERA_Y5_PIN;
    config.pin_d4 = CAMERA_Y6_PIN;
    config.pin_d5 = CAMERA_Y7_PIN;
    config.pin_d6 = CAMERA_Y8_PIN;
    config.pin_d7 = CAMERA_Y9_PIN;
    config.pin_xclk = CAMERA_XCLK_PIN;
    config.pin_pclk = CAMERA_PCLK_PIN;
    config.pin_vsync = CAMERA_VSYNC_PIN;
    config.pin_href = CAMERA_HREF_PIN;
    config.pin_sccb_sda = CAMERA_SIOD_PIN;
    config.pin_sccb_scl = CAMERA_SIOC_PIN;
    config.pin_pwdn = CAMERA_PWDN_PIN;
    config.pin_reset = CAMERA_RESET_PIN;
    config.xclk_freq_hz = nightLongExposure ? deviceConfig.longExposureXclkHz : 20000000;
    // Highest we ever want (OV5640's max). esp_camera_init() probes the sensor internally and
    // automatically clamps this down to whatever that sensor actually supports (e.g. OV2640 ->
    // UXGA, its native ~2MP max) *before* sizing the DMA receive buffer for it - so the buffer
    // and the resolution always agree. Do NOT try to raise the resolution after init via
    // sensor->set_framesize() instead - the DMA buffer stays sized for whatever was requested
    // here, so upsizing afterwards overflows it (cam_hal: "FB-OVF", then capture failures).
    config.frame_size = FRAMESIZE_QSXGA;   // 2560x1920
    config.pixel_format = PIXFORMAT_JPEG;  // JPEG formart
    config.grab_mode = CAMERA_GRAB_WHEN_EMPTY;
    config.fb_location = CAMERA_FB_IN_PSRAM;
    config.jpeg_quality = 5;
    config.fb_count = 2;

    // camera init
    esp_err_t err = esp_camera_init(&config);
    if (err != ESP_OK) {
        logf("Camera init failed with error 0x%x", err);
        return;
    }

    sensor_t *s = esp_camera_sensor_get();
    // initial sensors are flipped vertically and colors are a bit saturated
    if (s->id.PID == OV3660_PID) {
        s->set_vflip(s, 1);        // flip it back
        s->set_brightness(s, 1);   // up the brightness just a bit
        s->set_saturation(s, -2);  // lower the saturation
    }

    // Identify the sensor from its PID purely to record it - actual resolution was already
    // decided by esp_camera_init()'s own clamping (see config.frame_size above), nothing further
    // to do about that here. deviceConfig.cameraModel is only used as a fallback label for a PID
    // this firmware doesn't recognise, and is otherwise kept in sync with what's actually
    // detected so it's visible in CONFIG_FILE without needing a fresh detection pass.
    String detectedModel;
    if (s->id.PID == OV5640_PID) {
        detectedModel = "OV5640";
    } else if (s->id.PID == OV2640_PID) {
        detectedModel = "OV2640";
    } else {
        logf("Unrecognised camera PID 0x%04x - keeping configured cameraModel (%s)", s->id.PID, deviceConfig.cameraModel.c_str());
        detectedModel = deviceConfig.cameraModel;
    }
    if (detectedModel != deviceConfig.cameraModel) {
        logf("Camera model detected as %s (config had %s) - updating " CONFIG_FILE, detectedModel.c_str(), deviceConfig.cameraModel.c_str());
        deviceConfig.cameraModel = detectedModel;
        writeDeviceConfig(deviceConfig);
    }

    // AEC/AWB tuning - explicit rather than relying on driver defaults, which have drifted
    // between esp32-camera versions and differ between the OV5640/OV2640 drivers.
    s->set_whitebal(s, 1);        // auto white balance on
    s->set_awb_gain(s, 1);        // let AWB adjust the R/B gains, not just report a mode
    s->set_exposure_ctrl(s, 1);   // auto exposure on
    s->set_aec2(s, 1);            // DSP-based AEC - noticeably steadier than the sensor's own AEC
    s->set_ae_level(s, 0);        // neutral target exposure, no bias
    s->set_gain_ctrl(s, 1);       // auto gain on

    // Overrides the auto-exposure settings just above - see setupCameraNightExposure()'s comment
    // for what this does and doesn't achieve compared to the Raspberry Pi units' long exposure.
    // Gated on the sensor actually detected this boot (detectedModel), not the
    // nightLongExposure/deviceConfig.cameraModel check that picked config.xclk_freq_hz above -
    // that one only had last boot's PID detection (or the default) to go on.
    if (nightLongExposure && detectedModel == "OV5640") {
        setupCameraNightExposure(s);
    }

    // Mounting-orientation correction, set centrally on the server (see DeviceConfig /
    // applyConfigFields) - lets a camera be physically mounted upside down or mirrored without
    // a firmware change. Applied after the OV3660 fixup above, so it's the final word on
    // orientation regardless of sensor variant.
    s->set_vflip(s, deviceConfig.vflip ? 1 : 0);
    s->set_hmirror(s, deviceConfig.hflip ? 1 : 0);

    // The first frame(s) out of a freshly (re)started sensor are usually still mid-convergence on
    // exposure/white balance - especially here, since the camera gets fully powered off between
    // every single wake (see setCameraPower(false) below), so this settling happens on every
    // capture, not just once at first boot. Grab and discard a few before keeping one, so the
    // saved image reflects the sensor's settled AEC/AWB state rather than whatever it started at.
    // Tunable via deviceConfig.cameraWarmupFrames (config.json / API) without a reflash.
    for (uint8_t i = 0; i < deviceConfig.cameraWarmupFrames; i++) {
        camera_fb_t *warmupFrame = esp_camera_fb_get();
        if (warmupFrame) {
            esp_camera_fb_return(warmupFrame);
        }
        delay(100);
    }

    DatedPath datedPath = getDatedPath();

    // Capture camera photo. A single retry costs at most one more frame period if the first
    // attempt comes back NULL (timeout, DMA hiccup, transient SCCB error) - cheap insurance
    // against losing the whole wake cycle to one bad frame, most likely to matter at night
    // where a slower clock/near-ceiling exposure (see setupCameraNightExposure()) leaves less
    // margin than the normal daytime capture.
    //
    // Logged every cycle (not just on failure) so PSRAM free/largest-block has a baseline to
    // compare a failure against - e.g. distinguishing a fragmented/exhausted heap from the
    // "Failed to get frame: timeout" the driver itself logs (see fileLogVprintf()) when it isn't.
    logf("Free PSRAM before capture: %u bytes (largest block %u)",
         heap_caps_get_free_size(MALLOC_CAP_SPIRAM), heap_caps_get_largest_free_block(MALLOC_CAP_SPIRAM));

    camera_fb_t *frame = esp_camera_fb_get();
    if (!frame) {
        logLine("First capture attempt failed, retrying once...");
        frame = esp_camera_fb_get();
    }

    if (frame) {

        // Stored under CAMERA_DIR/yyyy/mm/dd/hh/ rather than directly in CAMERA_DIR - keeps
        // any single directory small no matter how large the backlog grows.
        String dirPath = String(CAMERA_DIR) + "/" + datedPath.dirPath;
        ensureDirExists(dirPath);

        String filename = dirPath + "/" + datedPath.leafName + ".jpeg";

        uint32_t startTime = millis();
        File jpg = SD.open(filename, "w");
        if (jpg) {
            logf("JPG created successfully,filename:%s write image data,framesize:%u * %u", filename.c_str(), frame->width, frame->height);
            jpg.write(frame->buf, frame->len);
            logf("JPG was written successfully, taking %lu ms", millis() - startTime);
            counts.pendingImages++;
        } else {
            logLine("JPG created failed!");
        }
        jpg.close();

        // Purely for runSetupApWindow()'s status page ("last recorded capture") - the AP window's
        // own live view is a direct camera stream (see "/stream"), not this file.
        lastCaptureTimestamp = getISO8601Timestamp();

        esp_camera_fb_return(frame);
    } else {
        logLine("Capturing camera failed!");
    }

    // Only actually powers on the modem and takes a fix once deviceConfig.geoIntervalS has
    // elapsed since the last one - see updateGeoLocationIfDue(). Checked here (after the photo,
    // before telemetry/uploads) so a freshly-updated position makes it into this cycle's Status
    // field below rather than waiting for the next wake.
    updateGeoLocationIfDue(deviceConfig);

    counts.pendingTelemetry++;
    String deviceId = getDeviceId();
    String telemetryJson = buildTelemetryJson(getISO8601Timestamp(), deviceId, get_battery_voltage(), get_solar_voltage(),
                                               (uint32_t)(millis() / 1000),
                                               deviceConfig.geoLat, deviceConfig.geoLon, deviceConfig.geoTimeRecorded, counts);
    writeTelemetryFile(datedPath, telemetryJson);
    bool telemetryBacklogExcessive = uploadPendingTelemetry(deviceId, counts, deviceConfig);

    // A second telemetry snapshot, captured after uploading telemetry (and any GPS fix) have finished -
    // comparing its uptimeSeconds/timestamp against the first snapshot's (written before any of
    // that work started) is a quick way to see how long this cycle's upload/GPS work actually
    // took, without having to comb through the serial log for it. Uses its own fresh DatedPath
    // (rather than reusing `datedPath` from the photo capture above) since it's genuinely being
    // captured later - that's the point of it.
    DatedPath secondDatedPath = getDatedPath();
    counts.pendingTelemetry++;
    String telemetryJson2 = buildTelemetryJson(getISO8601Timestamp(), deviceId, get_battery_voltage(), get_solar_voltage(),
                                                (uint32_t)(millis() / 1000),
                                                deviceConfig.geoLat, deviceConfig.geoLon, deviceConfig.geoTimeRecorded, counts);

    writeTelemetryFile(secondDatedPath, telemetryJson2);

    bool imagesBacklogExcessive = uploadPendingImages(deviceId, counts, deviceConfig);

    // A third telemetry snapshot, captured after imagery uploads have finished -
    DatedPath thirdDatedPath = getDatedPath();
    counts.pendingTelemetry++;
    String telemetryJson3 = buildTelemetryJson(getISO8601Timestamp(), deviceId, get_battery_voltage(), get_solar_voltage(),
                                                (uint32_t)(millis() / 1000),
                                                deviceConfig.geoLat, deviceConfig.geoLon, deviceConfig.geoTimeRecorded, counts);
    writeTelemetryFile(thirdDatedPath, telemetryJson3);

    // See the comment by needsSync above. FORCE_SYNC_FILE set here means next boot resyncs
    // regardless of autoSyncPeriodS; cleared once a cycle finally gets through without either
    // upload reporting an excessive backlog, whether or not it was actually set (harmless either
    // way - see clearForceSyncFlag()).
    if (didConnectWiFi) {
        if (telemetryBacklogExcessive || imagesBacklogExcessive) {
            setForceSyncFlag();
            logLine("Backlog well beyond what's expected per sync - flagged to sync again next boot regardless of autoSyncPeriodS");
        } else {
            clearForceSyncFlag();
            writeLastSyncTime(time(nullptr));
        }
    }

    writeCounts(counts);
}

// The page runSetupApWindow() serves at "/" - just enough for an installer to tell the camera's
// actually working: battery/solar, GPS fix, whether this boot could reach the internet at all,
// and the backlog - without needing any of that to actually have gone anywhere yet.
// windowRemainingS is how long until runSetupApWindow() closes the AP and this boot goes on to
// deep sleep - purely to drive the on-page countdown below, counted down client-side from there
// rather than re-polled, so it doesn't need another request to the device to stay accurate.
String buildStatusHtml(uint32_t windowRemainingS)
{
    TelemetryCounts counts = readCounts();
    uint16_t batteryMv = get_battery_voltage();

    // The viewport tag matters more than the font-size below - without it iOS Safari renders at
    // desktop width and shrinks the whole page to fit, which would undo any font-size increase.
    String html = "<html><head><title>EnviroCam Status</title>"
                  "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
                  "<style>body{font-size:1.4em}</style>"
                  "</head><body>";
    html += "<h1>EnviroCam " + getDeviceId() + "</h1>";
    // "/stream" is a multipart/x-mixed-replace MJPEG feed (see runSetupApWindow()) - browsers
    // render that natively in a plain <img>, no polling/JS needed to keep it live.
    html += "<img src=\"/stream\" style=\"max-width:100%\"><br>";
    html += "<p>Setup window closes (and this boot goes to sleep) in <span id=\"countdown\">" +
            String(windowRemainingS) + "</span>s</p>";
    html += "<script>var t=" + String(windowRemainingS) + ";setInterval(function(){"
            "t=Math.max(0,t-1);document.getElementById('countdown').textContent=t;"
            "},1000);</script>";
    html += "<p>Last recorded capture: " + (lastCaptureTimestamp.length() ? lastCaptureTimestamp : String("none yet")) + "</p>";
    html += "<ul>";
    html += "<li>Boot number: " + String(bootCount) + "</li>";
    html += "<li>Uptime: " + String(millis() / 1000) + "s</li>";
    html += "<li>Battery: " + String(batteryMv) + "mV (" + String(get_battery_percent(batteryMv)) + "%)</li>";
    html += "<li>Solar: " + String(get_solar_voltage()) + "mV</li>";
    html += "<li>GPS: " + String(deviceConfig.geoLat, 6) + ", " + String(deviceConfig.geoLon, 6) +
            " (fix recorded " + (deviceConfig.geoTimeRecorded.length() ? deviceConfig.geoTimeRecorded : String("never")) + ")</li>";
    html += "<li>This boot reached the internet: " + String(lastWakeConnectedWiFi ? "yes" : "no") + "</li>";
    html += "<li>Pending images: " + String(counts.pendingImages) + "</li>";
    html += "<li>Pending telemetry: " + String(counts.pendingTelemetry) + "</li>";
    html += "<li>Support mode: " + String(deviceConfig.supportMode ? "on" : "off") + "</li>";
    html += "<li>Camera interval: " + String(deviceConfig.cameraIntervalS) + "s</li>";
    html += "<li>API URL: " + deviceConfig.apiUrl + "</li>";
    html += "</ul></body></html>";
    return html;
}

// A temporary local WiFi hotspot + the status page above - entirely offline, no cellular or
// internet WiFi required - so an installer can confirm the camera's working right where they're
// standing. See SETUP_AP_* and the comment on setup()'s call to this. Runs for
// SETUP_AP_WINDOW_MS then returns, letting loop() carry on into its normal deep-sleep decision.
void runSetupApWindow()
{
    // Dropped to a much lighter resolution for this preview - the QSXGA frames runWakeCycle()
    // captures for real are far more than a live "is it aimed right" view needs, and every one of
    // those bytes has to go out over the softAP link. Safe to lower after esp_camera_init() (only
    // *raising* it afterwards overflows the DMA buffer, which was sized for the larger frame_size
    // requested at init) - and nothing needs it back at QSXGA, since the camera gets fully
    // deinitialised in loop() right after this returns regardless.
    sensor_t *sensor = esp_camera_sensor_get();
    if (sensor) {
        sensor->set_framesize(sensor, SETUP_AP_STREAM_FRAMESIZE);
    }

    String ssid = String(SETUP_AP_SSID_PREFIX) + getDeviceId();
    WiFi.mode(WIFI_AP);
    WiFi.softAP(ssid.c_str(), SETUP_AP_PASSWORD);
    logf("Setup AP \"%s\" (password \"%s\") up at %s for %lu seconds - connect to check status",
         ssid.c_str(), SETUP_AP_PASSWORD, WiFi.softAPIP().toString().c_str(), SETUP_AP_WINDOW_MS / 1000);

    // Advertises under the same name as the AP SSID so a companion app can discover which
    // physical unit is which without the installer having to read/type it - the app then joins
    // the AP itself using that name plus the fixed SETUP_AP_PASSWORD above, so nothing secret
    // ever needs to go over BLE. Advertising is stopped for as long as a station is actually
    // connected (below) since it shares the same 2.4GHz radio as the /stream handler and isn't
    // needed once a phone's already joined.
    NimBLEDevice::init(ssid.c_str());
    NimBLEAdvertising *pAdvertising = NimBLEDevice::getAdvertising();
    // NimBLEDevice::init() only sets the GATT device name (visible once connected) - the
    // advertisement payload itself needs the name set separately, or scanners see no name
    // at all. Matches the pattern in NimBLE-Arduino's own NimBLE_Server example.
    pAdvertising->setName(ssid.c_str());
    pAdvertising->start();
    logf("BLE advertising started as \"%s\"", ssid.c_str());
    wifi_event_id_t bleAdvertisingEventId = WiFi.onEvent([pAdvertising](WiFiEvent_t event, WiFiEventInfo_t info) {
        if (event == ARDUINO_EVENT_WIFI_AP_STACONNECTED) {
            pAdvertising->stop();
            logLine("Station connected - BLE advertising stopped");
        } else if (event == ARDUINO_EVENT_WIFI_AP_STADISCONNECTED) {
            pAdvertising->start();
            logLine("Station disconnected - BLE advertising resumed");
        }
    });

    uint32_t start = millis();

    WebServer server(80);
    server.on("/", HTTP_GET, [&server, start]() {
        uint32_t elapsedMs = millis() - start;
        uint32_t remainingS = (elapsedMs < SETUP_AP_WINDOW_MS) ? (SETUP_AP_WINDOW_MS - elapsedMs) / 1000 : 0;
        server.send(200, "text/html", buildStatusHtml(remainingS));
    });
    server.on("/stream", HTTP_GET, [&server, start]() {
        // Same multipart/x-mixed-replace MJPEG trick the CameraWebServer example uses - a
        // continuous run of JPEG frames a browser renders directly in an <img> tag, no page-side
        // JS needed. Frames come straight off the camera each time round, never touching SD.
        //
        // Bounded by the same SETUP_AP_WINDOW_MS deadline as the outer loop below: once a browser
        // opens this, the handler doesn't otherwise return - and hence never lets
        // server.handleClient() regain control - until the client disconnects, which would
        // otherwise stop the window from ever closing on schedule while a phone's still watching.
        WiFiClient client = server.client();
        client.print("HTTP/1.1 200 OK\r\nContent-Type: multipart/x-mixed-replace; boundary=frame\r\n\r\n");

        while (client.connected() && millis() - start < SETUP_AP_WINDOW_MS) {
            camera_fb_t *frame = esp_camera_fb_get();
            if (!frame) {
                break;
            }
            client.printf("--frame\r\nContent-Type: image/jpeg\r\nContent-Length: %u\r\n\r\n", frame->len);
            client.write(frame->buf, frame->len);
            client.print("\r\n");
            esp_camera_fb_return(frame);
            delay(SETUP_AP_STREAM_FRAME_INTERVAL_MS);
        }
    });
    server.begin();

    while (millis() - start < SETUP_AP_WINDOW_MS) {
        server.handleClient();
        delay(10);
    }

    // One more telemetry point, queued for the next wake's upload same as any other pending
    // record (nothing to upload it to right now - this is softAP, not a route to the internet) -
    // so the eventual upload shows uptimeSeconds covering the setup window too, not just
    // whatever runWakeCycle() had already captured before it ever opened.
    TelemetryCounts counts = readCounts();
    counts.pendingTelemetry++;
    String finalTelemetryJson = buildTelemetryJson(getISO8601Timestamp(), getDeviceId(), get_battery_voltage(), get_solar_voltage(),
                                                    (uint32_t)(millis() / 1000),
                                                    deviceConfig.geoLat, deviceConfig.geoLon, deviceConfig.geoTimeRecorded, counts);
    writeTelemetryFile(getDatedPath(), finalTelemetryJson);
    writeCounts(counts);

    server.close();
    // Unregister before touching BLE/WiFi teardown - softAPdisconnect() below forcibly drops
    // any connected station, which fires STADISCONNECTED, and a still-registered handler calling
    // through pAdvertising after it's gone would crash the same way NimBLEDevice::deinit() does
    // below (was, until this change - see next comment).
    WiFi.removeEvent(bleAdvertisingEventId);
    pAdvertising->stop();

    // Deliberately NOT calling NimBLEDevice::deinit() here. It signals NimBLE's separate host
    // FreeRTOS task to unwind out of nimble_port_run() and returns immediately, without waiting
    // for that task to actually finish self-deleting - and then goes on to tear down the BT
    // controller out from under it. Confirmed in the field (firmware.elf backtraces decoded
    // straight into NimBLEDevice::host_task()) as a reliably reproducible "Guru Meditation
    // Error ... InstrFetchProhibited" panic, PC=0. This is a known, unresolved bug in
    // NimBLE-Arduino itself (https://github.com/h2zero/NimBLE-Arduino/issues/803) - a 50ms delay
    // patched into deinit() (see lib/NimBLE-Arduino/src/NimBLEDevice.cpp, the same workaround
    // reported upstream) did not stop it recurring on every single AP window in the field, so
    // this isn't just a timing window worth widening further.
    //
    // We don't need a graceful BLE shutdown here: this boot is headed straight for
    // esp_deep_sleep_start() (or, in support mode, further wake cycles that never call
    // NimBLEDevice::init() again - see setup()/loop()), and deep sleep power-collapses the radio
    // hardware regardless of what state NimBLE's host stack thinks it's in. pAdvertising->stop()
    // above already silences the actual over-the-air advertising; leaving the host task/BT
    // controller nominally "initialized" but idle until the chip sleeps is a smaller risk than a
    // guaranteed crash on every setup/support-mode boot.
    WiFi.softAPdisconnect(true);
    WiFi.mode(WIFI_OFF);
    logLine("Setup AP window closed");
}

void loop()
{
    logLine("Disbale camera");
    esp_camera_deinit();

    logLine("Power off camera");
    setCameraPower(false);

    WiFi.disconnect(true);
    WiFi.mode(WIFI_OFF);

    // Support mode (set remotely via the API - see DeviceConfig::supportMode) keeps the board
    // awake instead of deep-sleeping, repeating the normal capture/telemetry/upload cycle on
    // cameraIntervalS instead of going quiet - so it stays reliably reachable, and imagery/
    // telemetry keep flowing, while someone's working on it. Each cycle re-reads config.json
    // (see runWakeCycle() -> readDeviceConfig()) and re-applies whatever the API's Device row
    // says (see applyDeviceConfigFromApiResponse()), which is how support mode being turned back
    // off ever gets noticed - falls through to the normal deep-sleep path below the next time
    // loop() runs after that.
    //
    // Ignored below BATTERY_LOW_PERCENT_15 - staying awake and uploading every cycle is exactly
    // what a battery that low can least afford, so this falls through to the normal (floored)
    // deep-sleep path instead, same as if support mode were off.
    uint8_t batteryPercent = get_battery_percent(get_battery_voltage());
    if (deviceConfig.supportMode && batteryPercent >= BATTERY_LOW_PERCENT_15) {
        logf("Support mode active - repeating wake cycle in %u seconds instead of deep-sleeping", deviceConfig.cameraIntervalS);
        delay(deviceConfig.cameraIntervalS * 1000UL);
        runWakeCycle();
        return;
    } else if (deviceConfig.supportMode) {
        logf("Support mode active but battery at %u%% - ignoring support mode and deep-sleeping instead", batteryPercent);
    }

    // Still inside the setup window's boot budget (see SETUP_AP_MAX_BOOT_COUNT) - runSetupApWindow()
    // just ran for minutes on its own, so skip the normal wait on top of that and make the next
    // boot (and its own setup window, while the budget lasts) available almost immediately instead.
    uint32_t sleepSeconds = (bootCount <= SETUP_AP_MAX_BOOT_COUNT) ? SETUP_AP_NEXT_BOOT_DELAY_S : computeSleepSeconds(deviceConfig);
    logf("Enter esp32 goto deepsleep for %u seconds!", sleepSeconds);
    esp_sleep_enable_timer_wakeup((uint64_t)sleepSeconds * uS_TO_S_FACTOR);
    delay(200);
    esp_deep_sleep_start();
}
