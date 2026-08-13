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
#include <time.h>
#include <PubSubClient.h>
#include <vector>
#include <algorithm>

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

#define MQTT_BROKER_HOST         "mqtt.venari.co.nz"
#define MQTT_BROKER_PORT         1883
#define MQTT_TOPIC_PREFIX        "camera"     // Telemetry is published to <prefix>/<device_id>/telemetry

#define TELEMETRY_DIR            "/telemetry"
#define TELEMETRY_UPLOADED_DIR   "/uploaded"        // Successfully published telemetry is moved here

#define CAMERA_DIR               "/camera"
#define CAMERA_UPLOADED_DIR      "/camera/uploaded"  // Nothing moves images here yet - reserved for a future upload step

#define COUNTS_FILE              "/counts.txt"  // Running pending/uploaded totals, kept up to date incrementally
                                                 // instead of scanning directories (which gets slow with thousands of files)

RTC_DATA_ATTR int bootCount = 0;

struct TelemetryCounts {
    int pendingImages;
    int uploadedImages;
    int pendingTelemetry;
    int uploadedTelemetry;
};

WiFiClient mqttNetClient;
PubSubClient mqttClient(mqttNetClient);

bool setCameraPower(bool enable)
{
    static bool started = false;

    if (!started) {
        Wire.begin(BOARD_SDA_PIN, BOARD_SCL_PIN);
        Wire.beginTransmission(0x28);
        if (Wire.endTransmission() != 0) {
            Serial.println("Camera power chip not found!");
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
    Serial.printf("Voltage:%u\n", vol);
    return vol;
}

// Returns 0 if the board has no solar ADC pin wired up
uint16_t get_solar_voltage()
{
#ifdef BOARD_SOLAR_ADC_PIN
    uint16_t vol = analogReadMilliVolts(BOARD_SOLAR_ADC_PIN) * 2;
    Serial.printf("Solar voltage:%u\n", vol);
    return vol;
#else
    return 0;
#endif
}

void set_device_to_sleep()
{
    Serial.println("Enter esp32 goto deepsleep!");
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

    Serial.print("SD Card Type: ");
    if (cardType == CARD_MMC) {
        Serial.println("MMC");
    } else if (cardType == CARD_SD) {
        Serial.println("SDSC");
    } else if (cardType == CARD_SDHC) {
        Serial.println("SDHC");
    } else {
        Serial.println("UNKNOWN");
    }

    uint64_t cardSize = SD.cardSize() / (1024 * 1024);
    Serial.printf("SD Card Size: %lluMB\n", cardSize);
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
        Serial.println("Failed to write last sync time!");
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
        Serial.println("Failed to write counts file!");
    }
}

bool connectWiFiAndSyncTime()
{
    Serial.printf("Connecting to WiFi: %s\n", WIFI_SSID);
    WiFi.mode(WIFI_STA);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    uint32_t startTime = millis();
    while (WiFi.status() != WL_CONNECTED) {
        if (millis() - startTime > WIFI_CONNECT_TIMEOUT_MS) {
            Serial.println("WiFi connect timed out!");
            return false;
        }
        delay(250);
        Serial.print(".");
    }
    Serial.printf("\nWiFi connected, IP: %s\n", WiFi.localIP().toString().c_str());

    configTime(GMT_OFFSET_SEC, DAY_LIGHT_OFFSET_SEC, NTP_SERVER);

    struct tm timeinfo;
    if (!getLocalTime(&timeinfo)) {
        Serial.println("Failed to obtain time from NTP server!");
        return false;
    }
    Serial.printf("Time synced: %04d-%02d-%02d %02d:%02d:%02d\n",
                   timeinfo.tm_year + 1900, timeinfo.tm_mon + 1, timeinfo.tm_mday,
                   timeinfo.tm_hour, timeinfo.tm_min, timeinfo.tm_sec);
    return true;
}

// Builds "yyyymmdd-hhmmss" from the current system time - filesystem-safe, used for filenames
// (FAT doesn't allow ':' in names, so this can't be true ISO8601)
String getTimestampString()
{
    struct tm timeinfo;
    char buf[16];
    if (!getLocalTime(&timeinfo, 0)) {
        // Fall back to boot count if time was never synced
        snprintf(buf, sizeof(buf), "boot-%d", bootCount);
        return String(buf);
    }
    strftime(buf, sizeof(buf), "%Y%m%d-%H%M%S", &timeinfo);
    return String(buf);
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
    // ESP32-S3 internal die temperature sensor, not ambient temperature
    float temperatureC = temperatureRead();

    char json[350];
    snprintf(json, sizeof(json),
             "{\"device_id\":\"%s\",\"timestamp\":\"%s\",\"boot_count\":%d,\"voltage_mv\":%u,\"solar_voltage_mv\":%u,\"temperature_c\":%.2f,"
             "\"pendingImages\":%d,\"uploadedImages\":%d,\"pendingTelemetry\":%d,\"uploadedTelemetry\":%d}",
             deviceId.c_str(), timestamp.c_str(), bootCount, voltageMv, solarVoltageMv, temperatureC,
             counts.pendingImages, counts.uploadedImages, counts.pendingTelemetry, counts.uploadedTelemetry);
    return String(json);
}

void writeTelemetryFile(const String &timestamp, const String &json)
{
    if (!SD.exists(TELEMETRY_DIR)) {
        if (SD.mkdir(TELEMETRY_DIR)) {
            Serial.println("Created telemetry directory!");
        }
    }

    String filename = String(TELEMETRY_DIR) + "/" + timestamp + ".json";

    File file = SD.open(filename, "w");
    if (file) {
        file.println(json);
        Serial.printf("Telemetry written: %s\n", json.c_str());
    } else {
        Serial.println("Failed to write telemetry file!");
    }
    file.close();
}

// Publishes every telemetry file still sitting in TELEMETRY_DIR (including ones saved
// during earlier offline cycles), oldest first, moving each one to TELEMETRY_UPLOADED_DIR
// once its publish is acknowledged. Published retained, so the broker holds onto the most
// recently published telemetry and serves it immediately to anyone who queries/subscribes
// later. Stops at the first failure, leaving it and everything after it in place to retry
// next time.
void publishPendingTelemetry(const String &deviceId, TelemetryCounts &counts)
{
    if (WiFi.status() != WL_CONNECTED) {
        return;
    }

    mqttClient.setServer(MQTT_BROKER_HOST, MQTT_BROKER_PORT);
    // Default PubSubClient buffer is 256 bytes, which the telemetry JSON (plus topic)
    // now exceeds now that solar_voltage_mv is included - bump it so publish() doesn't
    // just silently return false for being "too long".
    mqttClient.setBufferSize(384);
    if (!mqttClient.connected() && !mqttClient.connect(deviceId.c_str())) {
        Serial.printf("MQTT connect to %s failed, state:%d\n", MQTT_BROKER_HOST, mqttClient.state());
        return;
    }

    if (!SD.exists(TELEMETRY_UPLOADED_DIR)) {
        SD.mkdir(TELEMETRY_UPLOADED_DIR);
    }

    String topic = String(MQTT_TOPIC_PREFIX) + "/" + deviceId + "/telemetry";

    // List pending filenames first and sort them, rather than publishing in whatever
    // order openNextFile() happens to return (directory entry order isn't guaranteed
    // to be chronological once files have been created/renamed/deleted over time).
    // Filenames are "yyyymmdd-hhmmss.json", so a lexical sort is a chronological sort.
    std::vector<String> baseNames;
    File dir = SD.open(TELEMETRY_DIR);
    File entry = dir.openNextFile();
    while (entry) {
        if (!entry.isDirectory()) {
            String entryName = entry.name();
            baseNames.push_back(entryName.substring(entryName.lastIndexOf('/') + 1));
        }
        entry.close();
        entry = dir.openNextFile();
    }
    dir.close();

    std::sort(baseNames.begin(), baseNames.end());

    // Publish oldest first, retained, so that if we stop partway through (or the
    // connection drops), the most recent telemetry published is always the last
    // one - and being retained, it's the value anyone querying MQTT will see,
    // rather than an older reading it happens to have published successfully.
    for (const String &baseName : baseNames) {
        String filePath = String(TELEMETRY_DIR) + "/" + baseName;
        File file = SD.open(filePath, "r");
        if (!file) {
            continue;
        }
        String payload = file.readString();
        file.close();

        if (mqttClient.publish(topic.c_str(), payload.c_str(), true)) {
            String uploadedPath = String(TELEMETRY_UPLOADED_DIR) + "/" + baseName;
            SD.rename(filePath, uploadedPath);
            counts.pendingTelemetry--;
            counts.uploadedTelemetry++;
            Serial.printf("Published and archived %s\n", filePath.c_str());
        } else {
            Serial.printf("Failed to publish %s, will retry next time\n", filePath.c_str());
            // Stop here rather than skipping ahead to newer files: if the broker/connection
            // is the problem, later publishes would fail too, and any that then made it through
            // in a gap could become the retained message ahead of this older, still-pending one.
            break;
        }
    }

    mqttClient.disconnect();
}

void setup()
{
    Serial.begin(115200);

    Serial.println();

    //Increment boot number and print it every reboot
    ++bootCount;
    Serial.println("Boot number: " + String(bootCount));


#ifdef ENABLE_BATTERY_MON
    int tryCount = 4;
    while (tryCount--) {
        if (get_battery_voltage() < (BATTERY_VOLTAGE_LOW + 100)) {
            set_device_to_sleep();
        } else {
            Serial.println("Battery voltage is normal");
        }
        delay(500);
    }
#endif

    // Turn on the camera power
    if (!setCameraPower(true)) {
        Serial.println("Failed to initialize Camera power chip!"); return;
    }

    // Initialize sd card
    if (!setupSD()) {
        Serial.println("Failed to initialize SD card! Please check SD card!"); return;
    }

    // Kept up to date incrementally below rather than re-scanned from disk each wake,
    // since scanning directories with thousands of backlogged files gets slow
    TelemetryCounts counts = readCounts();

    // Deep sleep keeps the RTC running, so the clock usually survives between wake-ups.
    // Only reconnect to WiFi if the clock looks like it was reset by a power interruption
    // (i.e. it's now earlier than the last time we successfully synced), or if it's been
    // longer than AUTO_SYNC_PERIOD_SEC since the last sync, to correct clock drift.
    time_t lastSyncTime = readLastSyncTime();
    time_t now = time(nullptr);
    bool needsSync = (lastSyncTime == 0) || (now < lastSyncTime) || (now - lastSyncTime >= AUTO_SYNC_PERIOD_SEC);
    if (needsSync) {
        Serial.println("Clock needs sync, connecting to WiFi...");
        if (connectWiFiAndSyncTime()) {
            writeLastSyncTime(time(nullptr));
        } else {
            Serial.println("Continuing without synced time, filenames will use boot count!");
        }
    } else {
        Serial.println("Clock already synced, skipping WiFi connection");
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
        Serial.printf("Camera init failed with error 0x%x\n", err);
        return;
    }

    sensor_t *s = esp_camera_sensor_get();
    // initial sensors are flipped vertically and colors are a bit saturated
    if (s->id.PID == OV3660_PID) {
        s->set_vflip(s, 1);        // flip it back
        s->set_brightness(s, 1);   // up the brightness just a bit
        s->set_saturation(s, -2);  // lower the saturation
    }

    // s->set_vflip(s, 1);
    // s->set_hmirror(s, 1);

    String timestamp = getTimestampString();

    // Capture camera photo
    camera_fb_t *frame = esp_camera_fb_get();

    if (frame) {

        // Stored in the camera directory
        if (!SD.exists(CAMERA_DIR)) {
            if (SD.mkdir(CAMERA_DIR)) {
                Serial.println("Created camera directory!");
            }
        }

        String filename = String(CAMERA_DIR) + "/" + timestamp + ".jpg";

        uint32_t startTime = millis();
        File jpg = SD.open(filename, "w");
        if (jpg) {
            Serial.printf("JPG created successfully,filename:%s write image data,framesize:%u * %u", filename.c_str(), frame->width, frame->height);
            jpg.write(frame->buf, frame->len);
            Serial.printf("JPG was written successfully, taking %lu ms\n", millis() - startTime);
            counts.pendingImages++;
        } else {
            Serial.printf("JPG created failed!");
        }
        jpg.close();
        esp_camera_fb_return(frame);
    } else {
        Serial.println("Capturing camera failed!");
    }

    counts.pendingTelemetry++;
    String deviceId = getDeviceId();
    String telemetryJson = buildTelemetryJson(getISO8601Timestamp(), deviceId, get_battery_voltage(), get_solar_voltage(), counts);
    writeTelemetryFile(timestamp, telemetryJson);
    publishPendingTelemetry(deviceId, counts);
    writeCounts(counts);
}

void loop()
{
    Serial.println("Disbale camera");
    esp_camera_deinit();

    Serial.println("Power off camera");
    setCameraPower(false);

    WiFi.disconnect(true);
    WiFi.mode(WIFI_OFF);

    Serial.println("Enter esp32 goto deepsleep!");
    esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP * uS_TO_S_FACTOR);
    delay(200);
    esp_deep_sleep_start();
}
