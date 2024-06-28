#include "esp_camera.h"
#include "FS.h"
#include <SD_MMC.h>
#include "SPI.h"
// #include <U8x8lib.h>
#include <Wire.h>
#include <WiFi.h>
#include <NTPClient.h>
#include <WiFiUdp.h>
#include "RTClib.h"
#include <EEPROM.h>
// #include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include <Adafruit_NeoPixel.h>

#define CAMERA_MODEL_XIAO_ESP32S3  // Has PSRAM
#define WAVESHARE_ESP32_S3_SIM7670G_4G // Waveshare board

#include "camera_pins.h"
#include "arduino_secrets.h"

#define STATUS_INITIALISING 0
#define STATUS_CONNECTING_TO_WIFI 1
#define STATUS_UPLOADING 2
#define STATUS_INITIALISING_CAMERA 3
#define STATUS_SAVING_PHOTO 4
#define STATUS_SAVING_TELEMETRY 5
#define STATUS_COMPLETE 99

int currentStatus = STATUS_INITIALISING;

#define ERROR_BLINK_NO_RTC -3
#define ERROR_BLINK_SDCARD_MOUNT_FAILED -4
#define ERROR_BLINK_SDCARD_MOUNT_TYPE_NONE -5
#define ERROR_BLINK_CAMERA_INIT_FAILED -6
#define ERROR_BLINK_WIFI_CONNECTION_FAILED -7
#define ERROR_BLINK_WIFI_UPLOAD_FAILED -8

#define PIN_WIRE_SDA (4u)
#define PIN_WIRE_SCL (5u)

// U8X8_SSD1306_128X64_NONAME_HW_I2C u8x8(/* reset=*/U8X8_PIN_NONE);



// #define U8LOG_WIDTH 16
// #define U8LOG_HEIGHT 8
// uint8_t u8log_buffer[U8LOG_WIDTH * U8LOG_HEIGHT];
// U8X8LOG u8x8log;

const char *ssid = SECRET_SSID;
const char *password = SECRET_PASS;
char MACAddress[25] = "Not set";

WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP, "pool.ntp.org");

#define uS_TO_S_FACTOR 1000000ULL /* Conversion factor for micro seconds to seconds */
#define TIME_TO_SLEEP 60          /* Time ESP32 will go to sleep (in seconds) */

#if CONFIG_IDF_TARGET_ESP32
#define THRESHOLD 40   /* Greater the value, more the sensitivity */
#else                  //ESP32-S2 and ESP32-S3 + default for other chips (to be adjusted) */
#define THRESHOLD 5000 /* Lower the value, more the sensitivity */
#endif

const int SDMMC_CLK = 5;
const int SDMMC_CMD = 4;
const int SDMMC_DATA = 6;
const int SD_CD_PIN = 46;


// RTC_PCF8563 rtc;
RTC_DS1307 rtc;
// Pin Out definition: https://www.waveshare.com/esp32-s3-sim7670g-4g.htm
// Including pins that are used by caemra/TF card/modem, etc.
// const int RTC_DS1037_SDA = 12;
// const int RTC_DS1037_SCL = 11;
const int RTC_DS1037_SDA = 1;// ;
const int RTC_DS1037_SCL = 21; //5;
// DateTime rtcBootTime = DateTime(2000, 1, 1);


Adafruit_NeoPixel pixels = Adafruit_NeoPixel(1, 38, NEO_RGB + NEO_KHZ800);

// RTC_DATA_ATTR int bootCount = 0;

touch_pad_t touchPin;

const bool enableSleep = true;
const bool TPL5110 = false;
const int TPL5110_Reset_PIN = Y9_GPIO_NUM;

unsigned long lastCaptureTime = 0;  // Last shooting time
int imageCounter = 1;               // File Counter
int telemetryCounter = 1;

// int epochPseudoRTC = 0;             // Save time of last sleep in case we don't have a real RTC.

bool camera_sign = false;  // Check camera status
bool sd_sign = false;      // Check sd status
const char *counterFilenameBoot = "/counterBoot";
const char *counterFilenameImages = "/counterImages";
const char *counterFilenamePendingImages = "/counterPendingImages";
const char *counterFilenameTelemetry = "/counterTelemetry";
const char *counterFilenamePendingTelemetry = "/counterPendingTelemetry";
const char *logFilename = "/log.txt";

const char *pendingImageFolder = "/pendingImages";

const char *pendingTelemetryFolder = "/pendingTelemetry";

const char *serverName = "timelapse-dev.azurewebsites.net";
const int port = 443;
const char *apiPostImageURL = "/api/Image";
// const char *apiPostImageURL = "https://webhook.site/fc3e2df5-bb36-48a5-8e04-148c41a03839";
#define CHUNK_SIZE 4096  // Adjust this to a smaller size if needed

const char *ISO8061FormatString = "%04d-%02d-%02dT%02d:%02d:%02dZ";
const char *YYYYMMDDHHMMSSFormatString = "%04d-%02d-%02d_%02d%02d%02d";


// void displayMessage(const char *message) {
//   u8x8log.print(message);
//   u8x8log.print("\n");
// }

// void displayMessageNoNewline(const char *message) {
//   u8x8log.print(message);
// }

void logError(const char *format, ...) {
  va_list args;              // Create a variable argument list
  va_start(args, format);    // Initialize the variable argument list
  logMessage(format, args);  // Call the function that takes a variable argument list
  va_end(args);              // End the variable argument list
}

// logMessage function with variable arguments
void logMessage(const char *format, ...) {
  va_list args;              // Create a variable argument list
  va_start(args, format);    // Initialize the variable argument list
  logMessage(format, args);  // Call the function that takes a variable argument list
  va_end(args);              // End the variable argument list
}

void logMessage(const char *format, va_list args) {

  char buf[128];                              // Allocate a buffer to store the message
  vsnprintf(buf, sizeof(buf), format, args);  // Write the formatted string to the buffer
  Serial.println(buf);                        // Print the buffer to the serial port
  // displayMessage(buf);

  if (sd_sign == false) {
    // Serial.println("SD Card not mounted yet.");
    return;
  }

  File file = SD_MMC.open(logFilename, FILE_APPEND);
  if (!file) {
    Serial.println("Failed to open log file for writing");
    return;
  }

  if (file.println(buf)) {
    file.close();
  } else {
    Serial.println("Failed to append to log");
  }
}


void updateCounter(const char *counterFilename, int count) {
  // logMessage("updateCounter('%s', %d))", counterFilename, count);

  File file = SD_MMC.open(counterFilename, FILE_WRITE);
  if (!file) {
    Serial.println("Failed to open counter file for writing");
    return;
  }
  if (file.print(count)) {
    // Serial.println("Counter updated");
  } else {
    logError("Failed to update counter");
  }
}

int getCounter(const char *counterFilename) {
  File file = SD_MMC.open(counterFilename);
  if (!file) {
    Serial.printf("Failed to open counter %s file for reading\n", counterFilename);
    return 0;
  }
  int count = file.parseInt();
  file.close();
  return count;
}

void saveTelemetry() {

  // displayMessage("saveTelemetry()...");
  char filename[100];
  char rtcTime[25];

  DateTime now = rtc.now();
  sprintf(rtcTime, YYYYMMDDHHMMSSFormatString, now.year(), now.month(), now.day(), now.hour(), now.minute(), now.second());

  sprintf(filename, "%s/telemetry.%08d.%s.json", pendingTelemetryFolder, telemetryCounter, rtcTime);
  // Serial.printf("Telemetry filename: %s", filename);

  digitalWrite(LED_BUILTIN, LOW);  // XIAO ESP32S3 LOW = on

  // https://wiki.seeedstudio.com/check_battery_voltage/
  // https://forum.seeedstudio.com/t/battery-voltage-monitor-and-ad-conversion-for-xiao-esp32c/267535
  uint32_t Vbatt = 0;
  for (int i = 0; i < 16; i++) {
    Vbatt = Vbatt + analogReadMilliVolts(A0);  // ADC with correction
  }
  int Vbattf = 2 * Vbatt / 16;  /// 1000.0;     // attenuation ratio 1/2, mV --> V
  Serial.println(Vbattf, 3);

  // const int capacity = JSON_ARRAY_SIZE(1) + 2*JSON_OBJECT_SIZE(4);
  const int capacity = JSON_OBJECT_SIZE(8);
  StaticJsonDocument<capacity> doc;

  // JsonDocument doc;

  doc["BatteryPercent"] = 0;
  doc["TemperatureC"] = 21;
  doc["DiskSpaceFree"] = 0;
  doc["PendingImages"] = countFiles(pendingImageFolder);
  doc["PendingTelemetry"] = countFiles(pendingTelemetryFolder);
  doc["UptimeSeconds"] = millis() / 1000;

  // doc['status']['batteryVoltage'] = Vbattf;
  // String status = "{\'status':\"OK\", \"batteryVoltage\":" + String(Vbattf) + "}";
  String status = "{'status': {'isFault': False, 'isButton': False, 'battery': 'UNKNOWN', 'powerInput': 'UNKNOWN', 'powerInput5vIo': 'NOT_PRESENT'}, 'batteryVoltage': " + String(Vbattf) + ", 'batteryCurrent': 0, 'ioVoltage': 0, 'ioCurrent': 0}";


  doc["Status"] = status;
  doc["SerialNumber"] = MACAddress;

  File file = SD_MMC.open(filename, FILE_WRITE);
  String strJSON;
  serializeJsonPretty(doc, strJSON);
  // Serial.printf("strJSON");
  // Serial.printf(strJSON.c_str());
  file.print(strJSON);

  digitalWrite(LED_BUILTIN, HIGH);

  // Serial.printf("Saved telemetry: %s\r\n", filename);
  updateCounter(counterFilenameTelemetry, ++telemetryCounter);
}

void savePhoto() {

  Serial.println("savePhoto()");
  logRTC();

  char filename[100];
  char rtcTime[25];

  DateTime now = rtc.now();
  sprintf(rtcTime, YYYYMMDDHHMMSSFormatString, now.year(), now.month(), now.day(), now.hour(), now.minute(), now.second());

  sprintf(filename, "%s/image.%08d.%s.jpg", pendingImageFolder, imageCounter, rtcTime);
  Serial.printf("Save Photo filename: %s\n", filename);
  logRTC();


  // Serial.println("Starting photo_save()");
  digitalWrite(LED_BUILTIN, LOW);  // XIAO ESP32S3 LOW = on
  Serial.printf("Taking photo... %5d\n", millis());
  // delay(500);

  camera_fb_t *fb = esp_camera_fb_get();
  if (!fb) {
    logError("Failed to get camera frame buffer");
    return;
  }

  Serial.printf("Writing file... %5d\n", millis());
  // Serial.printf("Writing file...", millis());
  // Save photo to file
  writeFile(SD_MMC, filename, fb->buf, fb->len);
  Serial.printf("File written %5d\n", millis());

  // Release image buffer
  esp_camera_fb_return(fb);
  Serial.printf("image buffer released %5d\n", millis());

  digitalWrite(LED_BUILTIN, HIGH);

  Serial.printf("Saved picture: %s\r\n", filename);
  updateCounter(counterFilenameImages, ++imageCounter);
}

// SD card write file
void writeFile(fs::FS &fs, const char *path, uint8_t *data, size_t len) {
  // Serial.printf("Writing file: %s\r\n", path);

  if (!fs.exists(path)) {
    // Get folder name of file
    char folder[100];
    strcpy(folder, path);
    char *lastSlash = strrchr(folder, '/');
    if (lastSlash != NULL) {
      *lastSlash = '\0';
      if (!fs.exists(folder)) {
        fs.mkdir(folder);
      }
    }
  }

  File file = fs.open(path, FILE_WRITE);
  if (!file) {
    logError("Failed to open file for writing");
    return;
  }
  if (file.write(data, len) == len) {
    // Serial.println("File written");
  } else {
    logError("Write failed");
  }
  file.close();
}

void print_wakeup_reason() {
  esp_sleep_wakeup_cause_t wakeup_reason;

  wakeup_reason = esp_sleep_get_wakeup_cause();

  switch (wakeup_reason) {
    case ESP_SLEEP_WAKEUP_EXT0: Serial.println("Wakeup caused by external signal using RTC_IO"); break;
    case ESP_SLEEP_WAKEUP_EXT1: Serial.println("Wakeup caused by external signal using RTC_CNTL"); break;
    case ESP_SLEEP_WAKEUP_TIMER: Serial.println("Wakeup caused by timer"); break;
    case ESP_SLEEP_WAKEUP_TOUCHPAD: Serial.println("Wakeup caused by touchpad"); break;
    case ESP_SLEEP_WAKEUP_ULP: Serial.println("Wakeup caused by ULP program"); break;
    default: Serial.printf("Wakeup was not caused by deep sleep: %d\n", wakeup_reason); break;
  }
}

void print_wakeup_touchpad() {
  touchPin = esp_sleep_get_touchpad_wakeup_status();

#if CONFIG_IDF_TARGET_ESP32
  switch (touchPin) {
    case 0: Serial.println("Touch detected on GPIO 4"); break;
    case 1: Serial.println("Touch detected on GPIO 0"); break;
    case 2: Serial.println("Touch detected on GPIO 2"); break;
    case 3: Serial.println("Touch detected on GPIO 15"); break;
    case 4: Serial.println("Touch detected on GPIO 13"); break;
    case 5: Serial.println("Touch detected on GPIO 12"); break;
    case 6: Serial.println("Touch detected on GPIO 14"); break;
    case 7: Serial.println("Touch detected on GPIO 27"); break;
    case 8: Serial.println("Touch detected on GPIO 33"); break;
    case 9: Serial.println("Touch detected on GPIO 32"); break;
    default: Serial.println("Wakeup not by touchpad"); break;
  }
#else
  if (touchPin < TOUCH_PAD_MAX) {
    Serial.printf("Touch detected on GPIO %d\n", touchPin);
  } else {
    Serial.println("Wakeup not by touchpad");
  }
#endif
}

bool wifiConnect() {
  Serial.println("WiFi connecting");
  Serial.println(ssid);
  // Serial.printf(password);

  int wifiConnectTries = 0;

  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED && wifiConnectTries++ < 30) {
    delay(3000);
    // displayMessageNoNewline(".");
    Serial.print(".");
  }

  if (WiFi.status() != WL_CONNECTED) {
    logError("WiFi connection failed");
    return false;
  } else {
    Serial.println("WiFi connected");
    return true;
  }
}


void getNTPTime() {

  timeClient.begin();
  // timeClient.setTimeOffset(19800);
  timeClient.update();

  // Get NTP time....
  if (timeClient.isTimeSet()) {

    // Do all of this without logging/displaying to avoid introducing a few seconds delay.
    time_t epochTime = timeClient.getEpochTime();
    struct tm *ptm = gmtime(&epochTime);
    int monthDay = ptm->tm_mday;
    int currentMonth = ptm->tm_mon + 1;
    // String currentMonthName = months[currentMonth - 1];
    int currentYear = ptm->tm_year + 1900;

    // DateTime now = rtc.now();

    // Compare the time from the NTP server with the RTC time, and report the discrepancy in seconds.
    int timeDiscrepancy = rtc.now().unixtime() - epochTime;
    if (abs(timeDiscrepancy) > 5) {
        rtc.adjust(DateTime(currentYear, currentMonth, monthDay, ptm->tm_hour, ptm->tm_min, ptm->tm_sec));
        Serial.printf("RTC/NTP Time discrepancy was %d seconds\n", timeDiscrepancy);
      Serial.println("Updated RTC Time:");
      logRTC();
    }

  } else {
    logError("NTP Time is not set");
  }
}

const int FileArraySize = 100;
const int ImageFileBatchSize = 10;
const int TelemetryFileBatchSize = 100;

bool uploadPendingImages() {
  // returns true if all pending images have been uploaded.
  Serial.println("uploadPendingImages()");

  // Scan folder, retrieving most recent files first
  String *sortedFiles = listAndSortFiles(pendingImageFolder);
  int filesUploaded = 0;
  int filesToUpload = 0;

  // Array will be 100 large, with empty entries if files don't exist.
  for (int fileIndex = 0; fileIndex < FileArraySize; ++fileIndex) {
    if (sortedFiles[fileIndex].length() > 0) {
      ++filesToUpload;
    }
  }

  Serial.printf("Uploading: %d images\n", filesToUpload);

  for (int fileIndex = 0; fileIndex < FileArraySize && fileIndex < filesToUpload; ++fileIndex) {
    if (fileIndex >= ImageFileBatchSize) {
      Serial.printf("Batch size: %d (%d files remaining)\n", ImageFileBatchSize, filesToUpload - fileIndex);
      break;
    } else {
      // Serial.printf("Uploading %d/%d...", fileIndex + 1, filesToUpload);
      // displayMessageNoNewline(".");
      Serial.print(".");
    }

    String pendingFilename = pendingImageFolder;
    pendingFilename += "/";
    pendingFilename += sortedFiles[fileIndex];

    File file = SD_MMC.open(pendingFilename);

    if (!file.isDirectory()) {

      // Serial.printf(file.name());

      String boundary = "----WebKitFormBoundary" + String(random(0xFFFFFF), HEX);

      WiFiClientSecure client;
      client.setInsecure();  // Disable SSL certificate verification

      if (!client.connect(serverName, port)) {
        Serial.println("Connection failed!");
        return false;
      }

      String timestampString = file.name();
      // Serial.println(timestampString.c_str());
      timestampString.replace("_", "T");
      // Serial.println(timestampString.c_str());

      timestampString = timestampString.substring(15);
      // image.00000000.2000-00-01_454902.jpg
      // image.00000000.2000-00-01T454902.jpg
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333
      //                ^

      // Serial.println(timestampString.c_str());

      // 2000-00-01T454902.jpg
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333

      timestampString = timestampString.substring(0, 13) + ":" + timestampString.substring(13, 15) + ":" + timestampString.substring(15, 17) + "Z";
      // 2000-00-01T45:49:02.jpg
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333
      // ------------- -- --Z
      // Serial.println(timestampString.c_str());

      // https://forum.arduino.cc/t/sending-video-avi-and-audio-wav-files-with-arduino-script-from-esp32s3-via-http-post-multipart-form-data-to-server/1234706
      // https://stackoverflow.com/questions/53264373/try-to-send-image-file-to-php-with-httpclient

      String start_request = "--" + boundary + "\r\n";
      start_request += "Content-Disposition: form-data; name=\"SerialNumber\"\r\n\r\n";
      start_request += MACAddress;
      start_request += "\r\n";
      start_request += "--" + boundary + "\r\n";
      start_request += "Content-Disposition: form-data; name=\"Timestamp\"\r\n\r\n";
      start_request += timestampString;
      start_request += "\r\n";
      start_request += "--" + boundary + "\r\n";
      start_request += "Content-Disposition: form-data; name=\"File\"; filename=\"";
      start_request += file.name();
      start_request += "\"\r\n";
      start_request += "Content-Type: image/png\r\n\r\n";

      // Serial.print(start_request);

      String end_request = "\r\n--" + boundary + "--\r\n";

      int fileLength = file.size();
      int contentLength = start_request.length() + fileLength + end_request.length();

      client.printf("POST /api/Image HTTP/1.1\r\n");
      client.printf("Host: %s\r\n", serverName);
      client.printf("Content-Type: multipart/form-data; boundary=%s\r\n", boundary.c_str());
      client.printf("Content-Length: %d\r\n", contentLength);
      client.printf("Connection: close\r\n\r\n");

      client.print(start_request);

      uint8_t buffer[CHUNK_SIZE];
      while (file.available()) {
        size_t len = file.read(buffer, sizeof(buffer));
        client.write(buffer, len);
      }
      client.print(end_request);

      file.close();


      // Read the response from the server
      bool okResponse = false;
      bool NotFoundResponse = false;
      while (client.connected() || client.available()) {
        if (client.available()) {
          String line = client.readStringUntil('\n');
          if (line.indexOf("HTTP/1.1 200 OK") != -1) {
            okResponse = true;
            break;
          }
          if (line.indexOf("HTTP/1.1 404 Not Found") != -1) {
            NotFoundResponse = true;
            break;
          }
          Serial.println(line);
        }
      }
      client.stop();

      if (okResponse) {
        // Serial.println("Deleting file....");
        // Serial.println(pendingFilename);
        ++filesUploaded;
        if (!SD_MMC.remove(pendingFilename)) {
          logError("Failed to delete file");
        }
      } else {
        if (NotFoundResponse) {
          Serial.println("404 - check device is registered.");
        } else {
          Serial.println("Error sending data.");

          // TO DO - reject file here probably
        }
      }
    }
  }

  return filesUploaded == filesToUpload;
}

bool uploadPendingTelemetry() {
  // returns true if all pending telemetry has been uploaded.
  // Serial.printf("uploadPendingTelemetry()....");

  // Scan folder, retrieving most recent files first

  // TO DO optimise to avoid counting files...

  String *sortedFiles = listAndSortFiles(pendingTelemetryFolder);
  int filesUploaded = 0;
  int filesToUpload = 0;

  // Array will be 100 large, with empty entries if files don't exist.
  for (int fileIndex = 0; fileIndex < FileArraySize; ++fileIndex) {
    if (sortedFiles[fileIndex].length() > 0) {
      ++filesToUpload;
    }
  }

  Serial.printf("Uploading: %d telemetry files\n", filesToUpload);

  for (int fileIndex = 0; fileIndex < FileArraySize && fileIndex < filesToUpload; ++fileIndex) {
    if (fileIndex >= TelemetryFileBatchSize) {
      Serial.printf("Batch size: %d (%d files remaining)\n", TelemetryFileBatchSize, filesToUpload - fileIndex);
      break;
    } else {
      // Serial.printf("Uploading %d/%d...", fileIndex + 1, filesToUpload);
      Serial.print(".");
      // displayMessageNoNewline(".");
    }

    String pendingFilename = pendingTelemetryFolder;
    pendingFilename += "/";
    pendingFilename += sortedFiles[fileIndex];

    // Serial.printf("pendingFilename: %s", pendingFilename.c_str());

    File file = SD_MMC.open(pendingFilename);

    if (!file.isDirectory()) {

      // Serial.printf(file.name());

      String boundary = "----WebKitFormBoundary" + String(random(0xFFFFFF), HEX);

      WiFiClientSecure client;
      client.setInsecure();  // Disable SSL certificate verification

      if (!client.connect(serverName, port)) {
        Serial.println("Connection failed!");
        return false;
      }

      String timestampString = file.name();
      // Serial.println(timestampString.c_str());
      timestampString.replace("_", "T");
      // Serial.println(timestampString.c_str());
      timestampString = timestampString.substring(19);
      // Serial.println(timestampString.c_str());

      // telemetry.00000000.2000-00-01_454902.json
      // telemetry.00000000.2000-00-01T454902.json
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333
      //                    ^

      // Serial.println(timestampString.c_str());

      // 2000-00-01T454902.json
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333
      // ------------- -- --Z

      timestampString = timestampString.substring(0, 13) + ":" + timestampString.substring(13, 15) + ":" + timestampString.substring(15, 17) + "Z";
      // Serial.println(timestampString);
      // Serial.println(timestampString);

      DynamicJsonDocument doc(1024);
      // JsonDocument doc;
      deserializeJson(doc, file);

      String PendingTelemetry = doc["PendingTelemetry"];
      String TemperatureC = doc["TemperatureC"];
      String PendingImages = doc["PendingImages"];
      String Status = doc["Status"];
      String DiskSpaceFree = doc["DiskSpaceFree"];
      String UptimeSeconds = doc["UptimeSeconds"];
      String BatteryPercent = doc["BatteryPercent"];

      String payload = "--" + boundary + "\r\n"
                                         "Content-Disposition: form-data; name=\"PendingTelemetry\"\r\n\r\n"
                       + PendingTelemetry + "\r\n"
                                            "--"
                       + boundary + "\r\n"
                                    "Content-Disposition: form-data; name=\"TemperatureC\"\r\n\r\n"
                       + TemperatureC + "\r\n"
                                        "--"
                       + boundary + "\r\n"
                                    "Content-Disposition: form-data; name=\"PendingImages\"\r\n\r\n"
                       + PendingImages + "\r\n"
                                         "--"
                       + boundary + "\r\n"
                                    //  "Content-Disposition: form-data; name=\"Status\"\r\n\r\n{\"status\":\"OK\", \"batteryVoltage\":" + doc["batteryVoltage"] + "}\r\n"
                                    "Content-Disposition: form-data; name=\"Status\"\r\n\r\n"
                       + Status + "\r\n"
                                  "--"
                       + boundary + "\r\n"
                                    "Content-Disposition: form-data; name=\"DiskSpaceFree\"\r\n\r\n"
                       + DiskSpaceFree + "\r\n"
                                         "--"
                       + boundary + "\r\n"
                                    "Content-Disposition: form-data; name=\"Timestamp\"\r\n\r\n"
                       + timestampString + "\r\n"
                                           "--"
                       + boundary + "\r\n"
                                    "Content-Disposition: form-data; name=\"UptimeSeconds\"\r\n\r\n"
                       + UptimeSeconds + "\r\n"
                                         "--"
                       + boundary + "\r\n"
                                    "Content-Disposition: form-data; name=\"BatteryPercent\"\r\n\r\n"
                       + BatteryPercent + "\r\n"
                                          "--"
                       + boundary + "\r\n"
                                    "Content-Disposition: form-data; name=\"SerialNumber\"\r\n\r\n"
                       + MACAddress + "\r\n"
                                      "--"
                       + boundary + "--\r\n";

      // Serial.print(payload.c_str());
      file.close();

      int contentLength = payload.length();

      client.printf("POST /api/Telemetry HTTP/1.1\r\n");
      client.printf("Host: %s\r\n", serverName);
      client.printf("Content-Type: multipart/form-data; boundary=%s\r\n", boundary.c_str());
      client.printf("Content-Length: %d\r\n", contentLength);
      client.printf("Connection: close\r\n\r\n");

      client.print(payload);


      // Read the response from the server
      bool okResponse = false;
      bool NotFoundResponse = false;
      while (client.connected() || client.available()) {
        if (client.available()) {
          String line = client.readStringUntil('\n');
          if (line.indexOf("HTTP/1.1 200 OK") != -1) {
            okResponse = true;
            break;
          }
          if (line.indexOf("HTTP/1.1 404 Not Found") != -1) {
            NotFoundResponse = true;
            break;
          }
          if (line.indexOf("HTTP/1.1 302 Found") != -1) {
            okResponse = true;
            // Serial.printf("Got 302 rather than 200 - not sure why?");
            break;
          }
          Serial.println(line);
        }
      }
      client.stop();

      if (okResponse) {
        // Delete the file
        // Serial.println("Deleting file....");
        // Serial.println(pendingFilename);
        ++filesUploaded;

        if (!SD_MMC.remove(pendingFilename)) {
          logError("Failed to delete file");
        }
      } else {
        if (NotFoundResponse) {
          Serial.println("404 - check device is registered.");
        } else {
          Serial.println("Error sending data.");
        }
      }
    }
    // file = root.openNextFile();
  }
  // root.close();

  return filesUploaded == filesToUpload;
}

String *listAndSortFiles(const char *folder) {
  const int maxFiles = FileArraySize;
  String *filenames = new String[maxFiles];
  int fileCount = 0;

  if (!SD_MMC.exists(folder)) {
    SD_MMC.mkdir(folder);
  }

  File root = SD_MMC.open(folder);
  if (!root) {
    logError("Failed to open folder: %s", folder);
    return filenames;
  }



  // Collect filenames
  while (true) {
    File entry = root.openNextFile();
    if (!entry) {
      // no more files
      break;
    }
    if (!entry.isDirectory()) {
      if (fileCount < maxFiles) {
        filenames[fileCount] = String(entry.name());
        fileCount++;
      }
    }
    entry.close();
  }

  // Sort filenames in reverse order
  for (int i = 0; i < fileCount - 1; i++) {
    for (int j = 0; j < fileCount - i - 1; j++) {
      if (filenames[j] < filenames[j + 1]) {
        String temp = filenames[j];
        filenames[j] = filenames[j + 1];
        filenames[j + 1] = temp;
      }
    }
  }

  return filenames;
}

int countFiles(const char *folder) {

  if (!SD_MMC.exists(folder)) {
    SD_MMC.mkdir(folder);
  }

  File root = SD_MMC.open(folder);
  if (!root) {
    logError("Failed to open folder: %s", folder);
    return 0;
  }

  int fileCount = 0;

  // Collect filenames
  while (true) {
    File entry = root.openNextFile();
    if (!entry) {
      // no more files
      break;
    }
    if (!entry.isDirectory()) {
      fileCount++;
    }
    entry.close();
  }

  return fileCount;
}

void logRTC() {
  DateTime now = rtc.now();
  char rtcTime[25];
  sprintf(rtcTime, ISO8061FormatString, now.year(), now.month(), now.day(), now.hour(), now.minute(), now.second());
  Serial.printf("RTC Time: %s\n", rtcTime);
  // }
}

void setup() {

  currentStatus = STATUS_INITIALISING;

  pinMode(LED_BUILTIN, OUTPUT);
  if(TPL5110){
    pinMode(TPL5110_Reset_PIN, OUTPUT);
    digitalWrite(TPL5110_Reset_PIN, LOW);
  }

  pixels.begin();
  pixels.setPixelColor(0, pixels.Color(0, 0, 255));
  pixels.show();

  Serial.begin(115200);
  // while(!Serial); // When the serial monitor is turned on, the program starts to execute

  // u8x8.begin();
  // // u8x8.setFlipMode(1);   // set number from 1 to 3, the screen word will rotary 180

  // u8x8.setFont(u8x8_font_chroma48medium8_r);
  // // u8x8.setFont(u8x8_font_);

  // u8x8log.begin(u8x8, U8LOG_WIDTH, U8LOG_HEIGHT, u8log_buffer);
  // // u8x8log.setRedrawMode(0);		// 0: Update screen with newline, 1: Update screen for every char
  // u8x8log.setRedrawMode(1);  // 0: Update screen with newline, 1: Update screen for every char

  Wire.begin(RTC_DS1037_SDA, RTC_DS1037_SCL);

  // Serial.println("Checking RTC");
  if (!rtc.begin()) {
    logError("Couldn't find RTC");
    logError("We can't carry on :-(");
    logRTC();
    currentStatus = ERROR_BLINK_NO_RTC;
    // enableWakeupAndGoToSleep();
    return;
  } else {
    if(!rtc.isrunning()){
      logError("Found RTC but it is not running.");
      // logRTC();
      currentStatus = ERROR_BLINK_NO_RTC;
      return;
    } else {
      DateTime now = rtc.now();
      if(now.year()>2040 || now.year()<2020 || now.month()>12 || now.hour()>23 || now.minute()>59){
        // RTC not working correctly - let's behave as if it's not present
        logError("Found RTC, but it's returning incorrect date");
        // logRTC();
        currentStatus = ERROR_BLINK_NO_RTC;
        return;
      } else {
        // Everything is OK?
        // logRTC();
      }
    }
  }

  // logRTC();
  // Serial.println("About to init SD card");
  // Wire.end();

  // Initialize SD card
  pinMode(SD_CD_PIN, INPUT_PULLUP);
  // logRTC();
  // Serial.println("SD card 2");

  delay(3000);

  /*********************************
   * step 2 : start sd card
  ***********************************/

  SD_MMC.setPins(SDMMC_CLK, SDMMC_CMD, SDMMC_DATA);
  // logRTC();
  // Serial.println("SD card 3");

  if (!SD_MMC.begin("/sdcard", true)) {
      Serial.println("Card Mount Failed");
      currentStatus = ERROR_BLINK_SDCARD_MOUNT_FAILED;

      // enableWakeupAndGoToSleep();
      return;
  }

  uint8_t cardType = SD_MMC.cardType();
  if (cardType == CARD_NONE) {
      Serial.println("No SD_MMC card attached");
      currentStatus = ERROR_BLINK_SDCARD_MOUNT_TYPE_NONE;

      // enableWakeupAndGoToSleep();
      return;
  }

  Serial.print("SD_MMC Card Type: ");
  if (cardType == CARD_MMC) {
      Serial.println("MMC");
  } else if (cardType == CARD_SD) {
      Serial.println("SDSC");
  } else if (cardType == CARD_SDHC) {
      Serial.println("SDHC");
  } else {
      Serial.println("UNKNOWN");
  }

  uint64_t cardSize = SD_MMC.cardSize() / (1024 * 1024);
  Serial.printf("SD_MMC Card Size: %lluMB\n", cardSize);

  // listDir(SD_MMC, "/", 0);


  sd_sign = true;  // sd initialization check passes
  // Serial.printf("SD Card mounted %'d", millis());
  // logRTC();
  // Serial.printf("SD Card mounted");

  logRTC(); // good

  // Serial.printf("Starting up %'d", millis());/Volumes/NO NAME/counterBoot

  currentStatus = STATUS_INITIALISING_CAMERA;

  camera_config_t config;
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer = LEDC_TIMER_0;
  config.pin_d0 = Y2_GPIO_NUM;
  config.pin_d1 = Y3_GPIO_NUM;
  config.pin_d2 = Y4_GPIO_NUM;
  config.pin_d3 = Y5_GPIO_NUM;
  config.pin_d4 = Y6_GPIO_NUM;
  config.pin_d5 = Y7_GPIO_NUM;
  config.pin_d6 = Y8_GPIO_NUM;
  config.pin_d7 = Y9_GPIO_NUM;
  config.pin_xclk = XCLK_GPIO_NUM;
  config.pin_pclk = PCLK_GPIO_NUM;
  config.pin_vsync = VSYNC_GPIO_NUM;
  config.pin_href = HREF_GPIO_NUM;
  config.pin_sscb_sda = SIOD_GPIO_NUM;
  config.pin_sscb_scl = SIOC_GPIO_NUM;
  config.pin_pwdn = PWDN_GPIO_NUM;
  config.pin_reset = RESET_GPIO_NUM;
  config.xclk_freq_hz = 20000000;
  // config.xclk_freq_hz = 5000000; // Clock too high resulting in grainy/noisy image? https://github.com/espressif/esp32-camera/issues/172
  config.frame_size = FRAMESIZE_UXGA;
  // config.frame_size = FRAMESIZE_QSXGA;
  // config.frame_size = FRAMESIZE_QXGA;

  config.pixel_format = PIXFORMAT_JPEG;  // for streaming
  // config.pixel_format = PIXFORMAT_RAW; // Nope - didn't like that
  config.grab_mode = CAMERA_GRAB_WHEN_EMPTY;
  config.fb_location = CAMERA_FB_IN_PSRAM;
  config.jpeg_quality = 12;
  config.fb_count = 1;

  // Getting lots of "Failed to get camera frame buffer" errors?
  config.xclk_freq_hz = 5000000;  // Clock too high resulting in grainy/noisy image? https://github.com/espressif/esp32-camera/issues/172
  config.frame_size = FRAMESIZE_VGA;

  // if PSRAM IC present, init with UXGA resolution and higher JPEG quality
  //                      for larger pre-allocated frame buffer.
  if (config.pixel_format == PIXFORMAT_JPEG) {
    if (psramFound()) {
      config.jpeg_quality = 10;
      config.fb_count = 2;
      config.grab_mode = CAMERA_GRAB_LATEST;
    } else {
      // Limit the frame size when PSRAM is not available
      config.frame_size = FRAMESIZE_SVGA;
      config.fb_location = CAMERA_FB_IN_DRAM;
    }
  } else {
    // Best option for face detection/recognition
    config.frame_size = FRAMESIZE_240X240;
#if CONFIG_IDF_TARGET_ESP32S3
    config.fb_count = 2;
#endif
  }

  // logRTC(); // good
  // Serial.println("A");
  // logRTC(); // ?
  // Serial.println("B");
  // logRTC(); // ?

  Serial.println("about to init camera....");
  // camera init
  logRTC(); // good
  // Serial.println("C");
  // logRTC(); // good
  // This is where RTC gets messed up.
  esp_err_t err = esp_camera_init(&config);
  logRTC(); // buggered
  if (err != ESP_OK) {
    currentStatus = ERROR_BLINK_CAMERA_INIT_FAILED;
    logError("Camera init failed with error 0x%x", err);

    // enableWakeupAndGoToSleep();
    return;
  }

  // logRTC();
  sensor_t *s = esp_camera_sensor_get();
  // logRTC();
  // initial sensors are flipped vertically and colors are a bit saturated
  if (s->id.PID == OV3660_PID || s->id.PID == OV5640_PID) {
    s->set_vflip(s, 1);        // flip it back
    s->set_brightness(s, 1);   // up the brightness just a bit
    s->set_saturation(s, -2);  // lower the saturation
  }

  // logRTC();
  camera_sign = true;  // Camera initialization check passes
  Serial.println("Camera connected");
  Serial.flush();
  currentStatus = STATUS_SAVING_PHOTO;
  // displayMessage("Camera ready");
  // logRTC();



  // Serial.println("A");
  // Serial.flush();
  imageCounter = getCounter(counterFilenameImages);
  // Serial.flush();
  // Serial.println("B");
  // Serial.flush();
  telemetryCounter = getCounter(counterFilenameTelemetry);
  // Serial.flush();
  // Serial.println("C");
  // Serial.flush();
  // bootTime = getBootTime();
  Serial.printf("imageCounter = %d\r\n", imageCounter);
  // Serial.flush();
  Serial.printf("telemetryCounter = %d\r\n", telemetryCounter);
  // Serial.flush();
  // Serial.printf("bootTime = %d\r\n", bootTime);

  // print_wakeup_reason();
  // print_wakeup_touchpad();


  // logRTC();
  savePhoto();

  // logRTC(); // ?
  // Serial.println("Deinit1");
  // logRTC(); // ?

  // esp_camera_deinit();

  // Serial.println("Deinit2");
  // logRTC(); // ?

  // Serial.println("Deinit3");
  // Wire.begin(RTC_DS1037_SDA, RTC_DS1037_SCL);
  // rtc.begin();
  // logRTC(); // ?

  currentStatus = STATUS_SAVING_TELEMETRY;

  saveTelemetry();
  // Serial.printf("Staying awake for 15s to ease flashing");
  // delay(15000);


  // Serial.printf("sID: %s", sID);
  sprintf(MACAddress, "%012llx", ESP.getEfuseMac());

  int bootCount = getCounter(counterFilenameBoot);
  updateCounter(counterFilenameBoot, bootCount+1);

  Serial.printf("bootCount, %d\n", bootCount);

  if (bootCount++ % 5 == 0) {
    currentStatus = STATUS_CONNECTING_TO_WIFI;
    if (wifiConnect()) {
      getNTPTime();

      currentStatus = STATUS_UPLOADING;
      uploadPendingTelemetry();
      uploadPendingImages();
    } else {
      currentStatus = ERROR_BLINK_WIFI_CONNECTION_FAILED;
      return;
    }
  }

  Serial.printf("MAC Address: %s\n", MACAddress);


  // displayMessage("Camera startup");

  // Serial.printf("Boot number: %d", bootCount);

  // Serial.printf("bootCount, %d\n", bootCount);



  currentStatus = STATUS_COMPLETE;

  // enableWakeupAndGoToSleep();
  return;
}

void enableWakeupAndGoToSleep() {

#if CONFIG_IDF_TARGET_ESP32
  //Setup sleep wakeup on Touch Pad 3 + 7 (GPIO15 + GPIO 27)
  touchSleepWakeUpEnable(T3, THRESHOLD);
  touchSleepWakeUpEnable(T7, THRESHOLD);

#else  //ESP32-S2 + ESP32-S3
  //Setup sleep wakeup on Touch Pad 3 (GPIO3)
  touchSleepWakeUpEnable(T3, THRESHOLD);

#endif

  Serial.println("Going to sleep now");
  logRTC();
  Serial.flush();

  digitalWrite(LED_BUILTIN, LOW);  // XIAO ESP32S3 LOW = on
  delay(500);
  digitalWrite(LED_BUILTIN, HIGH);

  if(currentStatus<0){
    // Delay for 10s to show error code on LED
    delay(10000);
  }

  if (TPL5110) {
    // set pin D1 to high
    digitalWrite(TPL5110_Reset_PIN, HIGH);
    delay(1);
    digitalWrite(TPL5110_Reset_PIN, LOW);

  } else {
    // Timer Sleep:
    esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP * uS_TO_S_FACTOR);
    // Serial.printf("Setup ESP32 to sleep for  " + String(TIME_TO_SLEEP) + " Seconds");

    // External wakeup - RTC:
    esp_sleep_enable_ext0_wakeup(GPIO_NUM_33, 1);  //1 = High, 0 = Low

    if (enableSleep) {
      pixels.clear();
      pixels.show();
      esp_deep_sleep_start();
    } else {
      Serial.println("enableSleep = false - not sleeping");
    }
  }
}

void loop() {
  pixels.clear();

  Serial.printf("currentStatus: %d\n", currentStatus);
  
  if(currentStatus>0){

    switch(currentStatus){
      case STATUS_INITIALISING:
        pixels.setPixelColor(0, pixels.Color(255, 255, 255));
        pixels.show();
        break;
      case STATUS_CONNECTING_TO_WIFI:
        pixels.setPixelColor(0, pixels.Color(0, 0, 255));
        pixels.show();
        break;
      case STATUS_UPLOADING:
        pixels.setPixelColor(0, pixels.Color(0, 0, 255));
        pixels.show();
        break;
      case STATUS_SAVING_PHOTO:
        pixels.setPixelColor(0, pixels.Color(0, 50, 50));
        pixels.show();
        break;
      case STATUS_SAVING_TELEMETRY:
        pixels.setPixelColor(0, pixels.Color(0, 50, 50));
        pixels.show();
        break;
      case STATUS_COMPLETE:
        pixels.setPixelColor(0, pixels.Color(0, 255, 0));
        pixels.show();
        break;
      default:
        pixels.setPixelColor(0, pixels.Color(255, 0, 0));
        pixels.show();
        break;
    }
  } else {
    // ERROR condition.
    int numberOfFlashes = abs(currentStatus);
    Serial.printf("numberOfFlashes: %d\n", numberOfFlashes);
    for(int repeatSequence = 0; repeatSequence < 3; ++repeatSequence){
      for(int i =0; i < numberOfFlashes; ++i){
        Serial.println("R");
        for(int R=255; R >=0;  R-=30){
          pixels.setPixelColor(0, pixels.Color(R, 0, 0));
          pixels.show();
          delay(10);
        }
        pixels.setPixelColor(0, pixels.Color(0, 0, 0));
        pixels.show();
        Serial.println("-");
        delay(300);
        // Serial.println("DONE 1");
      }
      delay(1000);
    }
  }

  // Serial.println("DONE 2");
  delay(5000);

  logRTC();
  enableWakeupAndGoToSleep();
}

// uint32_t Wheel(byte WheelPos) {
//   WheelPos = 255 - WheelPos;
//   if(WheelPos < 85) {
//     return pixels.Color((255 - WheelPos * 3) / 2, 0, (WheelPos * 3) / 2);
//   }
//   if(WheelPos < 170) {
//     WheelPos -= 85;
//     return pixels.Color(0, (WheelPos * 3) / 2, (255 - WheelPos * 3) / 2);
//   }
//   WheelPos -= 170;
//   return pixels.Color((WheelPos * 3) / 2, (255 - WheelPos * 3) / 2, 0);
// }