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
#include <Wire.h>
#include <FS.h>
#include <SD.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <time.h>
#include <ArduinoJson.h>
#include <vector>
#include <algorithm>
#include <cstdarg>

#include "utilities.h"
#include "secrets.h"

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
#define BATTERY_VOLTAGE_LOW     3000        // Set low voltage to sleep mode

#define WIFI_CONNECT_TIMEOUT_MS 15000       // Give up on WiFi after this long
#define NTP_SERVER              "pool.ntp.org"
#define GMT_OFFSET_SEC           0          // Adjust for local timezone
#define DAY_LIGHT_OFFSET_SEC     0          // Adjust for daylight saving

#define LAST_SYNC_FILE           "/last_sync.txt"
#define AUTO_SYNC_PERIOD_SEC     (5 * 60)     // Force a resync after this long, to correct clock drift

#define TELEMETRY_DIR            "/telemetry"
#define TELEMETRY_UPLOADED_DIR   "/uploaded"        // Successfully uploaded telemetry is moved here

#define CAMERA_DIR               "/camera/pending"    // Sibling of CAMERA_UPLOADED_DIR, not a parent of it -
                                                       // otherwise a recursive walk of CAMERA_DIR (see
                                                       // listFilesRecursive) would wander into "uploaded" and
                                                       // re-discover already-uploaded images as pending forever.
#define CAMERA_UPLOADED_DIR      "/camera/uploaded"  // Successfully uploaded images are moved here

#define COUNTS_FILE              "/counts.txt"  // Running pending/uploaded totals, kept up to date incrementally
                                                 // instead of scanning directories (which gets slow with thousands of files)

#define CONFIG_FILE              "/config.json" // Local cache of the device config the API hands back on every upload

#define LOG_DIR                  "/logs"
#define LOG_FILE                 "/logs/envirocam.log"  // Active day's log - see rotateLogIfNeeded()
#define LOG_RETENTION_DAYS       30                      // Rotated logs older than this get deleted

// Defaults used until a config.json exists (i.e. before the API has ever handed back a
// Device row - see applyDeviceConfigFromApiResponse). Names match the Device model's JSON
// property names on the API side (System.Text.Json defaults to camelCase).
#define DEFAULT_SLEEP_DURING_NIGHT   false
#define DEFAULT_DAYTIME_STARTS_AT_H  7
#define DEFAULT_DAYTIME_ENDS_AT_H    17
#define DEFAULT_CAMERA_INTERVAL_S    300
#define DEFAULT_API_URL              "https://timelapse-dev.azurewebsites.net/api/"
#define DEFAULT_HFLIP                false
#define DEFAULT_VFLIP                false

RTC_DATA_ATTR int bootCount = 0;

struct TelemetryCounts {
    int pendingImages;

    int uploadedImages;
    int pendingTelemetry;
    int uploadedTelemetry;
};

// Config handed back by the API in the Device object nested in every Image/Telemetry POST
// response (see applyDeviceConfigFromApiResponse), and cached to CONFIG_FILE. Reloaded fresh
// from SD every boot (deep sleep doesn't preserve normal RAM).
struct DeviceConfig {
    bool sleepDuringNight = DEFAULT_SLEEP_DURING_NIGHT;
    int daytimeStartsAtH = DEFAULT_DAYTIME_STARTS_AT_H;
    int daytimeEndsAtH = DEFAULT_DAYTIME_ENDS_AT_H;
    uint32_t cameraIntervalS = DEFAULT_CAMERA_INTERVAL_S;
    String apiUrl = DEFAULT_API_URL;
    bool hflip = DEFAULT_HFLIP;
    bool vflip = DEFAULT_VFLIP;
};

DeviceConfig deviceConfig;

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

void set_device_to_sleep()
{
    logLine("Enter esp32 goto deepsleep!");
    esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP * uS_TO_S_FACTOR);
    delay(200);
    esp_deep_sleep_start();
    Serial.println("This will never be printed");
    while (1);
}


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

// Returns all-zero counts if never written yet (no file, or unreadable)
TelemetryCounts readCounts()
{
    TelemetryCounts counts = {0, 0, 0, 0};
    File file = SD.open(COUNTS_FILE, "r");
    if (!file) {
        return counts;
    }
    String line = file.readStringUntil('\n');
    file.close();
    sscanf(line.c_str(), "%d,%d,%d,%d",
           &counts.pendingImages, &counts.uploadedImages,
           &counts.pendingTelemetry, &counts.uploadedTelemetry);
    return counts;
}

void writeCounts(const TelemetryCounts &counts)
{
    File file = SD.open(COUNTS_FILE, "w");
    if (file) {
        file.printf("%d,%d,%d,%d\n", counts.pendingImages, counts.uploadedImages,
                     counts.pendingTelemetry, counts.uploadedTelemetry);
        file.close();
    } else {
        logLine("Failed to write counts file!");
    }
}

// Applies whichever of sleepDuringNight/daytimeStartsAtH/daytimeEndsAtH/cameraIntervalS/apiUrl/
// hflip/vflip are present in `fields` on top of an existing DeviceConfig - used for both
// CONFIG_FILE (on disk) and the Device object nested in an Image/Telemetry API response, so a
// partial payload only touches the fields it mentions.
void applyConfigFields(JsonVariantConst fields, DeviceConfig &config)
{
    config.sleepDuringNight = fields["sleepDuringNight"] | config.sleepDuringNight;
    config.daytimeStartsAtH = fields["daytimeStartsAtH"] | config.daytimeStartsAtH;
    config.daytimeEndsAtH   = fields["daytimeEndsAtH"] | config.daytimeEndsAtH;
    config.cameraIntervalS  = fields["cameraIntervalS"] | config.cameraIntervalS;
    config.hflip            = fields["hflip"] | config.hflip;
    config.vflip            = fields["vflip"] | config.vflip;
    if (!fields["apiUrl"].isNull()) {
        String apiUrl = fields["apiUrl"].as<String>();
        if (apiUrl.length() > 0) {
            config.apiUrl = apiUrl;
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

    DynamicJsonDocument doc(512);
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
    DynamicJsonDocument doc(512);
    doc["sleepDuringNight"] = config.sleepDuringNight;
    doc["daytimeStartsAtH"] = config.daytimeStartsAtH;
    doc["daytimeEndsAtH"] = config.daytimeEndsAtH;
    doc["cameraIntervalS"] = config.cameraIntervalS;
    doc["apiUrl"] = config.apiUrl;
    doc["hflip"] = config.hflip;
    doc["vflip"] = config.vflip;

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
    DynamicJsonDocument doc(1024);
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
        newConfig.cameraIntervalS != config.cameraIntervalS ||
        newConfig.apiUrl != config.apiUrl ||
        newConfig.hflip != config.hflip ||
        newConfig.vflip != config.vflip) {
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
            listFilesRecursive(fullPath, paths);
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

// Derived from the efuse base MAC, so it's stable and unique per board without needing WiFi to be up
String getDeviceId()
{
    uint64_t chipId = ESP.getEfuseMac();
    char buf[13];
    snprintf(buf, sizeof(buf), "%04X%08X", (uint16_t)(chipId >> 32), (uint32_t)chipId);
    return String(buf);
}

String buildTelemetryJson(const String &timestamp, const String &deviceId, uint16_t voltageMv, uint16_t solarVoltageMv, const TelemetryCounts &counts)
{
    // ESP32-S3 internal die temperature sensor, not ambient temperature.
    // Named temperatureC (rather than temperature_c) to match the field name used for the
    // same quantity elsewhere (API's TelemetryPostModel, Pi's saveTelemetry.py). Rounded to an
    // int here (rather than kept as the raw float reading) since that's what both of those
    // actually store/accept - TemperatureC is an int column, and a decimal value posted to it
    // gets rejected.
    int temperatureC = (int)lroundf(temperatureRead());
    uint8_t batteryPercent = get_battery_percent(voltageMv);

    char json[400];
    snprintf(json, sizeof(json),
             "{\"device_id\":\"%s\",\"timestamp\":\"%s\",\"boot_count\":%d,\"voltage_mv\":%u,\"solar_voltage_mv\":%u,\"temperatureC\":%d,"
             "\"batteryPercent\":%u,"
             "\"pendingImages\":%d,\"uploadedImages\":%d,\"pendingTelemetry\":%d,\"uploadedTelemetry\":%d}",
             deviceId.c_str(), timestamp.c_str(), bootCount, voltageMv, solarVoltageMv, temperatureC, batteryPercent,
             counts.pendingImages, counts.uploadedImages, counts.pendingTelemetry, counts.uploadedTelemetry);
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

// Performs a multipart/form-data POST to <apiUrl><endpoint>, with the given form fields plus
// an optional file streamed from SD (pass file=nullptr to omit). Returns the HTTP status code
// (0 if the connection itself failed) and, via responseBody, whatever the server sent back -
// the Image/Telemetry endpoints return the saved row plus its related Device, which is how
// config changes get back to the device (see applyDeviceConfigFromApiResponse) without a
// separate polling mechanism.
int postMultipartForm(const String &apiUrl, const String &endpoint, const std::vector<HttpFormField> &fields,
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

    WiFiClientSecure secureClient;
    WiFiClient plainClient;
    if (api.https) {
        secureClient.setInsecure();   // No cert store on-device - trust whatever's presented
    }
    Client &client = api.https ? (Client &)secureClient : (Client &)plainClient;

    logf("POST %s://%s:%u%s", api.https ? "https" : "http", api.host.c_str(), api.port, path.c_str());

    if (!client.connect(api.host.c_str(), api.port)) {
        logf("Connection to %s:%u failed", api.host.c_str(), api.port);
        return 0;
    }

    client.printf("POST %s HTTP/1.1\r\n", path.c_str());
    client.printf("Host: %s\r\n", api.host.c_str());
    client.printf("Content-Type: multipart/form-data; boundary=%s\r\n", boundary.c_str());
    client.printf("Content-Length: %u\r\n", (unsigned)contentLength);
    client.print("Connection: close\r\n\r\n");
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

    // Status line + headers
    while ((client.connected() || client.available()) && millis() - start < 15000) {
        if (!client.available()) {
            continue;
        }
        String line = client.readStringUntil('\n');
        if (line.startsWith("HTTP/1.1")) {
            statusCode = line.substring(9, 12).toInt();
        }
        // Header names are case-insensitive (RFC 7230) - Kestrel/Azure sends this rather than
        // a Content-Length whenever it doesn't know the body size upfront, which is the normal
        // case for our controller responses.
        String lowerLine = line;
        lowerLine.toLowerCase();
        if (lowerLine.startsWith("transfer-encoding:") && lowerLine.indexOf("chunked") >= 0) {
            chunked = true;
        }
        if (line == "\r") {
            break;   // blank line - end of headers, body follows
        }
    }

    // Body
    responseBody = "";
    if (chunked) {
        // Each chunk is "<hex size>\r\n<size bytes of data>\r\n", repeated until a zero-size
        // chunk terminates the body (optionally followed by trailer headers, then a final
        // blank line). Without this, the raw chunk framing (hex size lines, inter-chunk CRLFs)
        // ends up embedded in responseBody and breaks JSON parsing downstream.
        while (millis() - start < 15000) {
            if (!client.available() && !client.connected()) {
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
    } else {
        while ((client.connected() || client.available()) && millis() - start < 15000) {
            if (client.available()) {
                responseBody += (char)client.read();
            }
        }
    }

    client.stop();
    return statusCode;
}

// Uploads every telemetry file still sitting in TELEMETRY_DIR (including ones saved during
// earlier offline cycles), oldest first, moving each one to TELEMETRY_UPLOADED_DIR once the
// API acknowledges it with 200 OK. Stops at the first failure, leaving it and everything after
// it in place to retry next time. Also refreshes deviceConfig from the response.
void uploadPendingTelemetry(const String &deviceId, TelemetryCounts &counts, DeviceConfig &config)
{
    if (WiFi.status() != WL_CONNECTED) {
        return;
    }

    // List pending files first and sort them, rather than uploading in whatever order the walk
    // happens to return. Paths are ".../yyyy/mm/dd/hh/yyyymmdd-hhmmss.json", so a lexical sort
    // of the full path is still a chronological sort.
    std::vector<String> filePaths;
    listFilesRecursive(TELEMETRY_DIR, filePaths);
    std::sort(filePaths.begin(), filePaths.end());

    int filesUploaded = 0;
    for (const String &filePath : filePaths) {
        // Process in batches of 100, same as the Pi's uploadPendingTelemetry()
        if (filesUploaded >= 100) {
            logLine("Hit upload batch limit - remaining telemetry will upload next cycle");
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
            continue;
        }

        DynamicJsonDocument doc(400);
        DeserializationError err = deserializeJson(doc, file);
        file.close();

        if (err) {
            logf("Failed to parse %s: %s - discarding", filePath.c_str(), err.c_str());
            SD.remove(filePath);
            continue;
        }

        // Packs the fields that don't have a place of their own in TelemetryPostModel into
        // Status, the same way the Pi's saveTelemetry.py packs PiJuice detail in there.
        char status[128];
        snprintf(status, sizeof(status), "{\"boot_count\":%d,\"voltage_mv\":%u,\"solar_voltage_mv\":%u}",
                 doc["boot_count"].as<int>(), doc["voltage_mv"].as<unsigned>(), doc["solar_voltage_mv"].as<unsigned>());

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
            {"UptimeSeconds", String((uint32_t)(millis() / 1000))},
            {"PendingImages", doc["pendingImages"].as<String>()},
            {"UploadedImages", doc["uploadedImages"].as<String>()},
            {"PendingTelemetry", doc["pendingTelemetry"].as<String>()},
            {"UploadedTelemetry", doc["uploadedTelemetry"].as<String>()},
        };

        logf("Posting Telemetry: %s", fieldsToJson(fields).c_str());

        String responseBody;
        int statusCode = postMultipartForm(config.apiUrl, "Telemetry", fields, "", "", nullptr, responseBody);

        logf("Telemetry response (status %d): %s", statusCode, responseBody.c_str());

        if (statusCode == 200) {
            // Mirrors the same yyyy/mm/dd/hh bucketing into TELEMETRY_UPLOADED_DIR - otherwise
            // we'd just relocate the flat-directory slowdown from "pending" to "uploaded".
            String relativePath = filePath.substring(String(TELEMETRY_DIR).length());
            String uploadedPath = String(TELEMETRY_UPLOADED_DIR) + relativePath;
            ensureDirExists(parentDir(uploadedPath));
            SD.rename(filePath, uploadedPath);
            counts.pendingTelemetry--;
            counts.uploadedTelemetry++;
            filesUploaded++;
            applyDeviceConfigFromApiResponse(responseBody, config);
            logf("Uploaded and archived %s", filePath.c_str());
        } else {
            logf("Failed to upload %s (status %d), will retry next time", filePath.c_str(), statusCode);
            break;
        }
    }

    DatedPath datedPath = getDatedPath();
    counts.pendingTelemetry++;
    String telemetryJson = buildTelemetryJson(getISO8601Timestamp(), deviceId, get_battery_voltage(), get_solar_voltage(), counts);
    writeTelemetryFile(datedPath, telemetryJson);
}

// Uploads every image still sitting in CAMERA_DIR (not the "uploaded" subfolder), oldest
// first, over HTTP to the same API endpoint the Raspberry Pi units already post to - imagery
// isn't published over MQTT, since message brokers aren't a great fit for large binary
// payloads, especially once this moves to cellular. Moves each file to CAMERA_UPLOADED_DIR
// once the API acknowledges it with 200 OK; stops at the first failure so a flaky connection
// doesn't reorder the backlog (same approach as uploadPendingTelemetry()).
void uploadPendingImages(const String &deviceId, TelemetryCounts &counts, DeviceConfig &config)
{
    if (WiFi.status() != WL_CONNECTED) {
        return;
    }

    std::vector<String> filePaths;
    listFilesRecursive(CAMERA_DIR, filePaths);
    std::sort(filePaths.begin(), filePaths.end());

    int filesUploaded = 0;
    for (const String &filePath : filePaths) {
        // Process in batches of 10, same as the Pi's uploadPendingPhotos()
        if (filesUploaded >= 10) {
            logLine("Hit upload batch limit - remaining images will upload next cycle");
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
            continue;
        }

        String timestamp = parseTimestampFromPath(filePath);

        std::vector<HttpFormField> fields = {
            {"SerialNumber", deviceId},
            {"Timestamp", timestamp},
        };

        logf("Posting Image: %s", fieldsToJson(fields).c_str());

        String responseBody;
        int statusCode = postMultipartForm(config.apiUrl, "Image", fields, "File", fileBaseName(filePath), &file, responseBody);
        file.close();

        logf("Image response (status %d): %s", statusCode, responseBody.c_str());

        if (statusCode == 200) {
            // Mirrors the same yyyy/mm/dd/hh bucketing into CAMERA_UPLOADED_DIR - otherwise
            // we'd just relocate the flat-directory slowdown from "pending" to "uploaded".
            String relativePath = filePath.substring(String(CAMERA_DIR).length());
            String uploadedPath = String(CAMERA_UPLOADED_DIR) + relativePath;
            ensureDirExists(parentDir(uploadedPath));
            SD.rename(filePath, uploadedPath);
            counts.pendingImages--;
            counts.uploadedImages++;
            filesUploaded++;
            applyDeviceConfigFromApiResponse(responseBody, config);
            logf("Uploaded and archived %s", filePath.c_str());
        } else {
            logf("Failed to upload %s (status %d), will retry next time", filePath.c_str(), statusCode);
            break;
        }
    }

    DatedPath datedPath = getDatedPath();
    counts.pendingTelemetry++;
    String telemetryJson = buildTelemetryJson(getISO8601Timestamp(), deviceId, get_battery_voltage(), get_solar_voltage(), counts);
    writeTelemetryFile(datedPath, telemetryJson);
}

// Decides how long to deep-sleep for, in seconds. Normally just config.cameraIntervalS, but
// when sleep_during_night is enabled and it's currently outside daytime hours, sleeps through
// until daytimeStartsAtH instead of waking every cameraIntervalS to take a photo in the dark.
// Hours are compared in UTC, since GMT_OFFSET_SEC/DAY_LIGHT_OFFSET_SEC are left at 0.
uint32_t computeSleepSeconds(const DeviceConfig &config)
{
    if (!config.sleepDuringNight) {
        return config.cameraIntervalS;
    }

    struct tm timeinfo;
    if (!getLocalTime(&timeinfo, 0)) {
        // No synced clock to judge night from - fall back to the regular interval
        return config.cameraIntervalS;
    }

    bool isNight = (timeinfo.tm_hour >= config.daytimeEndsAtH) || (timeinfo.tm_hour < config.daytimeStartsAtH);
    if (!isNight) {
        return config.cameraIntervalS;
    }

    struct tm wake = timeinfo;
    wake.tm_hour = config.daytimeStartsAtH;
    wake.tm_min = 0;
    wake.tm_sec = 0;

    time_t now = mktime(&timeinfo);
    time_t wakeTime = mktime(&wake);
    if (wakeTime <= now) {
        wakeTime += 24 * 60 * 60;
    }

    uint32_t sleepSeconds = (uint32_t)difftime(wakeTime, now);
    logf("Night mode - sleeping %u seconds until %02d:00 UTC", sleepSeconds, config.daytimeStartsAtH);
    return sleepSeconds;
}

void setup()
{
    Serial.begin(115200);

    Serial.println();

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

    // Turn on the camera power
    if (!setCameraPower(true)) {
        logLine("Failed to initialize Camera power chip!"); return;
    }

    // Initialize sd card
    if (!setupSD()) {
        logLine("Failed to initialize SD card! Please check SD card!"); return;
    }

    // Kept up to date incrementally below rather than re-scanned from disk each wake,
    // since scanning directories with thousands of backlogged files gets slow
    TelemetryCounts counts = readCounts();

    // Reloaded fresh every boot - deep sleep doesn't preserve normal RAM - then refreshed
    // from the API response further down, once WiFi is up.
    deviceConfig = readDeviceConfig();

    // Deep sleep keeps the RTC running, so the clock usually survives between wake-ups.
    // Only reconnect to WiFi if the clock looks like it was reset by a power interruption
    // (i.e. it's now earlier than the last time we successfully synced), or if it's been
    // longer than AUTO_SYNC_PERIOD_SEC since the last sync, to correct clock drift.
    time_t lastSyncTime = readLastSyncTime();
    time_t now = time(nullptr);
    bool needsSync = (lastSyncTime == 0) || (now < lastSyncTime) || (now - lastSyncTime >= AUTO_SYNC_PERIOD_SEC);
    if (needsSync) {
        logLine("Clock needs sync, connecting to WiFi...");
        if (connectWiFiAndSyncTime()) {
            writeLastSyncTime(time(nullptr));
        } else {
            logLine("Continuing without synced time, filenames will use boot count!");
        }
    } else {
        logLine("Clock already synced, skipping WiFi connection");
    }

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
    config.xclk_freq_hz = 20000000;
    config.frame_size = FRAMESIZE_HD;       //1280x720
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

    // Mounting-orientation correction, set centrally on the server (see DeviceConfig /
    // applyConfigFields) - lets a camera be physically mounted upside down or mirrored without
    // a firmware change. Applied after the OV3660 fixup above, so it's the final word on
    // orientation regardless of sensor variant.
    s->set_vflip(s, deviceConfig.vflip ? 1 : 0);
    s->set_hmirror(s, deviceConfig.hflip ? 1 : 0);

    DatedPath datedPath = getDatedPath();

    // Capture camera photo
    camera_fb_t *frame = esp_camera_fb_get();

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
        esp_camera_fb_return(frame);
    } else {
        logLine("Capturing camera failed!");
    }

    counts.pendingTelemetry++;
    String deviceId = getDeviceId();
    String telemetryJson = buildTelemetryJson(getISO8601Timestamp(), deviceId, get_battery_voltage(), get_solar_voltage(), counts);
    writeTelemetryFile(datedPath, telemetryJson);
    uploadPendingTelemetry(deviceId, counts, deviceConfig);


    uploadPendingImages(deviceId, counts, deviceConfig);

    writeCounts(counts);
}

void loop()
{
    logLine("Disbale camera");
    esp_camera_deinit();

    logLine("Power off camera");
    setCameraPower(false);

    WiFi.disconnect(true);
    WiFi.mode(WIFI_OFF);

    uint32_t sleepSeconds = computeSleepSeconds(deviceConfig);
    logf("Enter esp32 goto deepsleep for %u seconds!", sleepSeconds);
    esp_sleep_enable_timer_wakeup((uint64_t)sleepSeconds * uS_TO_S_FACTOR);
    delay(200);
    esp_deep_sleep_start();
}
