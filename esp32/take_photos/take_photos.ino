#include "esp_camera.h"
#include "FS.h"
#include "SD.h"
#include "SPI.h"
#include <U8x8lib.h>
#include <Wire.h>
#include <WiFi.h>
#include <NTPClient.h>
#include <WiFiUdp.h>
#include "RTClib.h"
#include <EEPROM.h>
// #include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>

#define CAMERA_MODEL_XIAO_ESP32S3  // Has PSRAM

#include "camera_pins.h"
#include "arduino_secrets.h"



#define PIN_WIRE_SDA (4u)
#define PIN_WIRE_SCL (5u)

// U8X8_SSD1306_128X64_NONAME_HW_I2C u8x8(/* clock=*/ PIN_WIRE_SCL, /* data=*/ PIN_WIRE_SDA, /* reset=*/ U8X8_PIN_NONE);   // OLEDs without Reset of the Display
U8X8_SSD1306_128X64_NONAME_HW_I2C u8x8(/* reset=*/U8X8_PIN_NONE);

#define U8LOG_WIDTH 16
#define U8LOG_HEIGHT 8
uint8_t u8log_buffer[U8LOG_WIDTH * U8LOG_HEIGHT];
U8X8LOG u8x8log;

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


RTC_PCF8563 rtc;

RTC_DATA_ATTR int bootCount = 0;
RTC_DATA_ATTR time_t bootTime = 0;

bool rtcPresent=true;
touch_pad_t touchPin;

const bool enableSleep = true;

unsigned long lastCaptureTime = 0;  // Last shooting time
int imageCounter = 1;                 // File Counter
int telemetryCounter = 1;

// int epochPseudoRTC = 0;             // Save time of last sleep in case we don't have a real RTC.

bool camera_sign = false;           // Check camera status
bool sd_sign = false;               // Check sd status
const char *counterFilenameImages = "/counterImages";
const char *counterFilenameTelemetry = "/counterTelemetry";
// const char *bootTimeFilename = "/bootTime";
const char *logFilename = "/log.txt";

const char *pendingImageFolder = "/pendingImages";

const char *pendingTelemetryFolder = "/pendingTelemetry";

const char *serverName = "timelapse-dev.azurewebsites.net";
const int port = 443;
const char *apiPostImageURL = "/api/Image";
// const char *apiPostImageURL = "https://webhook.site/fc3e2df5-bb36-48a5-8e04-148c41a03839";
#define CHUNK_SIZE 4096 // Adjust this to a smaller size if needed

const char *ISO8061FormatString = "%04d-%02d-%02dT%02d:%02d:%02dZ";
const char *YYYYMMDDHHMMSSFormatString = "%04d-%02d-%02d_%02d%02d%02d";


void displayMessage(const char *message) {
  u8x8log.print(message);
  u8x8log.print("\n");
}

void displayMessageNoNewline(const char *message) {
  u8x8log.print(message);
}

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
  displayMessage(buf);

  if (sd_sign == false) {
    Serial.println("SD Card not mounted yet.");
    return;
  }

  File file = SD.open(logFilename, FILE_APPEND);
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
  logMessage("updateCounter('%s', %d))", counterFilename, count);
    
  File file = SD.open(counterFilename, FILE_WRITE);
  if (!file) {
    logMessage("Failed to open counter file for writing");
    return;
  }
  if (file.print(count)) {
    // Serial.println("Counter updated");
  } else {
    logError("Failed to update counter");
  }
}

int getCounter(const char *counterFilename) {
  File file = SD.open(counterFilename);
  if (!file) {
    logMessage("Failed to open counter %s file for reading", counterFilename);
    return 0;
  }
  int count = file.parseInt();
  file.close();
  return count;
}

// void updateBootTime(time_t booted) {
//   logMessage("updateBootTime()");
//   logMessage("booted: %s", booted);
//   File file = SD.open(bootTimeFilename, FILE_WRITE);
//   if (!file) {
//     logMessage("Failed to open bootTime file for writing");
//     return;
//   }
//   if (file.print(booted)) {
//     // Serial.println("Counter updated");
//   } else {
//     logError("Failed to update boot time");
//   }
// }

void setPseudoRTC(time_t timeNow){
  // Update the time we booted from NTP
  logMessage("setPseudoRTC()");
  logMessage("timeNow: %d", timeNow);
  logMessage("millis: %d", millis());
  bootTime = timeNow - millis()/1000;
  logMessage("bootTime: %d", bootTime);
  // bootTime = booted;
}

time_t getPseudoRTC(){
  // time_t bootTime = getBootTime();
  logMessage("getPseudoRTC()");
  logMessage("bootTime: %d", bootTime);
  logMessage("millis(): %d", millis());
  return bootTime + millis()/1000;
}

DateTime PRTCnow(){
  if(rtcPresent){
    return rtc.now();
  } else {
    return getPseudoRTCNow();
  }
}

DateTime getPseudoRTCNow(){
  time_t pseudoRTC = getPseudoRTC();
  logMessage("pseudoRTC: %d", pseudoRTC);
  DateTime pseudoRTCNow = DateTime(pseudoRTC);

  return pseudoRTCNow;
}


void saveTelemetry() {

  char filename[100];
  char rtcTime[25];
  
  DateTime now = PRTCnow();
  sprintf(rtcTime, YYYYMMDDHHMMSSFormatString, now.year(), now.month(), now.day(), now.hour(), now.minute(), now.second());

  sprintf(filename, "%s/telemetry.%08d.%s.json", pendingTelemetryFolder, telemetryCounter, rtcTime);
  logMessage("Telemetry filename: %s", filename);

  digitalWrite(LED_BUILTIN, LOW);  // XIAO ESP32S3 LOW = on

  // https://wiki.seeedstudio.com/check_battery_voltage/
  // https://forum.seeedstudio.com/t/battery-voltage-monitor-and-ad-conversion-for-xiao-esp32c/267535
  uint32_t Vbatt = 0;
  for(int i = 0; i < 16; i++) {
    Vbatt = Vbatt + analogReadMilliVolts(A0); // ADC with correction   
  }
  int Vbattf = 2 * Vbatt / 16; /// 1000.0;     // attenuation ratio 1/2, mV --> V
  Serial.println(Vbattf, 3);

  // const int capacity = JSON_ARRAY_SIZE(1) + 2*JSON_OBJECT_SIZE(4);
  const int capacity = JSON_OBJECT_SIZE(8);
  StaticJsonDocument<capacity> doc;

  // JsonDocument doc;

  doc["BatteryPercent"] =  0;
  doc["TemperatureC"] =  21;
  doc["DiskSpaceFree"] =  0;
  doc["PendingImages"] =  countFiles(pendingImageFolder);
  doc["PendingTelemetry"] =  countFiles(pendingTelemetryFolder);
  doc["UptimeSeconds"] =  millis()/1000;

  // doc['status']['batteryVoltage'] = Vbattf;
  // String status = "{\'status':\"OK\", \"batteryVoltage\":" + String(Vbattf) + "}";
  String status = "{'status': {'isFault': False, 'isButton': False, 'battery': 'UNKNOWN', 'powerInput': 'UNKNOWN', 'powerInput5vIo': 'NOT_PRESENT'}, 'batteryVoltage': " + String(Vbattf) + ", 'batteryCurrent': 0, 'ioVoltage': 0, 'ioCurrent': 0}";


  doc["Status"] = status;
  doc["SerialNumber"] = MACAddress;

  File file = SD.open(filename, FILE_WRITE);
  String strJSON;
  serializeJsonPretty(doc, strJSON);
  logMessage("strJSON");
  logMessage(strJSON.c_str());
  file.print(strJSON);



  digitalWrite(LED_BUILTIN, HIGH);

  logMessage("Saved telemetry: %s\r\n", filename);
  updateCounter(counterFilenameTelemetry, ++telemetryCounter);
}

void savePhoto() {

  char filename[100];
  char rtcTime[25];
  
  DateTime now = PRTCnow();
  sprintf(rtcTime, YYYYMMDDHHMMSSFormatString, now.year(), now.month(), now.day(), now.hour(), now.minute(), now.second());

  sprintf(filename, "%s/image.%08d.%s.jpg", pendingImageFolder, imageCounter, rtcTime);
  logMessage("Save Photo filename: %s", filename);


  // Serial.println("Starting photo_save()");
  digitalWrite(LED_BUILTIN, LOW);  // XIAO ESP32S3 LOW = on
  // logMessage("Taking photo... %5d", millis());
  // delay(500);

  camera_fb_t *fb = esp_camera_fb_get();
  if (!fb) {
    logError("Failed to get camera frame buffer");
    return;
  }

  logMessage("Writing file... %5d", millis());
  // logMessage("Writing file...", millis());
  // Save photo to file
  writeFile(SD, filename, fb->buf, fb->len);
  // logMessage("File written %5d", millis());

  // Release image buffer
  esp_camera_fb_return(fb);
  // logMessage("image buffer released %5d", millis());

  digitalWrite(LED_BUILTIN, HIGH);

  logMessage("Saved picture: %s\r\n", filename);
  updateCounter(counterFilenameImages, ++imageCounter);
}

// SD card write file
void writeFile(fs::FS &fs, const char *path, uint8_t *data, size_t len) {
  Serial.printf("writeFile()");
  Serial.flush();
  Serial.printf("Writing file: %s\r\n", path);
  Serial.flush();

  if(!fs.exists(path)){
    // Get folder name of file
    char folder[100];
    strcpy(folder, path);
    char *lastSlash = strrchr(folder, '/');
    if (lastSlash != NULL) {
      *lastSlash = '\0';
      if(!fs.exists(folder)){
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
    case ESP_SLEEP_WAKEUP_EXT0: logMessage("Wakeup caused by external signal using RTC_IO"); break;
    case ESP_SLEEP_WAKEUP_EXT1: logMessage("Wakeup caused by external signal using RTC_CNTL"); break;
    case ESP_SLEEP_WAKEUP_TIMER: logMessage("Wakeup caused by timer"); break;
    case ESP_SLEEP_WAKEUP_TOUCHPAD: logMessage("Wakeup caused by touchpad"); break;
    case ESP_SLEEP_WAKEUP_ULP: logMessage("Wakeup caused by ULP program"); break;
    default: logMessage("Wakeup was not caused by deep sleep: %d\n", wakeup_reason); break;
  }
}

void print_wakeup_touchpad() {
  touchPin = esp_sleep_get_touchpad_wakeup_status();

#if CONFIG_IDF_TARGET_ESP32
  switch (touchPin) {
    case 0: logMessage("Touch detected on GPIO 4"); break;
    case 1: logMessage("Touch detected on GPIO 0"); break;
    case 2: logMessage("Touch detected on GPIO 2"); break;
    case 3: logMessage("Touch detected on GPIO 15"); break;
    case 4: logMessage("Touch detected on GPIO 13"); break;
    case 5: logMessage("Touch detected on GPIO 12"); break;
    case 6: logMessage("Touch detected on GPIO 14"); break;
    case 7: logMessage("Touch detected on GPIO 27"); break;
    case 8: logMessage("Touch detected on GPIO 33"); break;
    case 9: logMessage("Touch detected on GPIO 32"); break;
    default: logMessage("Wakeup not by touchpad"); break;
  }
#else
  if (touchPin < TOUCH_PAD_MAX) {
    logMessage("Touch detected on GPIO %d\n", touchPin);
  } else {
    logMessage("Wakeup not by touchpad");
  }
#endif
}

bool wifiConnect() {
  logMessage("WiFi connecting");
  logMessage(ssid);
  // logMessage(password);

  int wifiConnectTries = 0;

  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED && wifiConnectTries++ < 30) {
    delay(3000);
    displayMessageNoNewline(".");
    Serial.print(".");
  }

  if (WiFi.status() != WL_CONNECTED) {
    logError("WiFi connection failed");
    return false;
  } else {
    logMessage("WiFi connected");
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
    int timeDiscrepancy = PRTCnow().unixtime() - epochTime;
    if (abs(timeDiscrepancy) > 5) {
      if(rtcPresent){
        rtc.adjust(DateTime(currentYear, currentMonth, monthDay, ptm->tm_hour, ptm->tm_min, ptm->tm_sec));
        logMessage("Adjusted RTC...");
      } else {
        setPseudoRTC(epochTime);
        logMessage("Adjusted Pseudo RTC...");

      }

      logMessage("RTC/NTP Time discrepancy was %d seconds", timeDiscrepancy);

      // DateTime now = rtc.now();
      // char rtcTime[25];
      // sprintf(rtcTime, ISO8061FormatString, now.year(), now.month(), now.day(), now.hour(), now.minute(), now.second());
      // logMessage("Updated RTC Time: %s", rtcTime);
      logMessage("Updated RTC Time:");
      logRTC();
    }

  } else {
    logError("NTP Time is not set");
  }
}

const int FileArraySize = 100;
const int ImageFileBatchSize = 10;
const int TelemetryFileBatchSize = 100;

bool uploadPendingImages(){
  // returns true if all pending images have been uploaded.
  logMessage("uploadPendingImages()....");

  // Scan folder, retrieving most recent files first
  String* sortedFiles = listAndSortFiles(pendingImageFolder);
  int filesUploaded = 0;
  int filesToUpload = 0;

  // Array will be 100 large, with empty entries if files don't exist.
  for(int fileIndex = 0; fileIndex < FileArraySize; ++fileIndex){
    if(sortedFiles[fileIndex].length()>0){
      ++filesToUpload;
    }
  }

  for(int fileIndex = 0; fileIndex < FileArraySize && fileIndex < filesToUpload; ++fileIndex){
    if(fileIndex>=ImageFileBatchSize){
      logMessage("Batch size: %d (%d files remaining)", ImageFileBatchSize, filesToUpload - fileIndex);
      break;
    } else {
      logMessage("Uploading %d/%d...", fileIndex+1, filesToUpload);
    }

    String pendingFilename = pendingImageFolder;
    pendingFilename += "/";
    pendingFilename += sortedFiles[fileIndex];

    File file = SD.open(pendingFilename);
    
    if (!file.isDirectory()) {

      logMessage(file.name());

      String boundary = "----WebKitFormBoundary" + String(random(0xFFFFFF), HEX);

      WiFiClientSecure client;
      client.setInsecure(); // Disable SSL certificate verification

      if (!client.connect(serverName, port)) {
        Serial.println("Connection failed!");
        return false;
      }
      
      String timestampString = file.name();
      Serial.println(timestampString.c_str());
      timestampString.replace("_", "T");
      Serial.println(timestampString.c_str());

      timestampString = timestampString.substring(15);
      // image.00000000.2000-00-01_454902.jpg
      // image.00000000.2000-00-01T454902.jpg
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333
      //                ^

      Serial.println(timestampString.c_str());

      // 2000-00-01T454902.jpg
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333

      timestampString = timestampString.substring(0, 13) + ":" + timestampString.substring(13, 15) + ":" + timestampString.substring(15, 17) + "Z";      
      // 2000-00-01T45:49:02.jpg
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333
      // ------------- -- --Z
      Serial.println(timestampString.c_str());

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
          if(line.indexOf("HTTP/1.1 200 OK") != -1){
            okResponse = true;
            break;
          }
          if(line.indexOf("HTTP/1.1 404 Not Found") != -1){
            NotFoundResponse = true;
            break;
          }
          Serial.println(line);
        }
      }
      client.stop();

      if(okResponse){
        Serial.println("Deleting file....");
        Serial.println(pendingFilename);
        ++filesUploaded;
        if (!SD.remove(pendingFilename)) {
          logError("Failed to delete file");
        }
      } else {
        if(NotFoundResponse){
          logMessage("404 - check device is registered.");
        } else {
          logMessage("Error sending data.");
        }
      }

    }
  }

  return filesUploaded == filesToUpload;
}

bool uploadPendingTelemetry(){
  // returns true if all pending telemetry has been uploaded.
  logMessage("uploadPendingTelemetry()....");

  // Scan folder, retrieving most recent files first
  String* sortedFiles = listAndSortFiles(pendingTelemetryFolder);
  int filesUploaded = 0;
  int filesToUpload = 0;

  // Array will be 100 large, with empty entries if files don't exist.
  for(int fileIndex = 0; fileIndex < FileArraySize; ++fileIndex){
    if(sortedFiles[fileIndex].length()>0){
      ++filesToUpload;
    }
  }

  for(int fileIndex = 0; fileIndex < FileArraySize && fileIndex < filesToUpload; ++fileIndex){
    if(fileIndex>=TelemetryFileBatchSize){
      logMessage("Batch size: %d (%d files remaining)", TelemetryFileBatchSize, filesToUpload - fileIndex);
      break;
    } else {
      logMessage("Uploading %d/%d...", fileIndex+1, filesToUpload);
    }

    String pendingFilename = pendingTelemetryFolder;
    pendingFilename += "/";
    pendingFilename += sortedFiles[fileIndex];

    logMessage("pendingFilename: %s", pendingFilename.c_str());

    File file = SD.open(pendingFilename);
    
    if (!file.isDirectory()) {

      logMessage(file.name());

      String boundary = "----WebKitFormBoundary" + String(random(0xFFFFFF), HEX);

      WiFiClientSecure client;
      client.setInsecure(); // Disable SSL certificate verification

      if (!client.connect(serverName, port)) {
        Serial.println("Connection failed!");
        return false;
      }
      
      String timestampString = file.name();
      Serial.println(timestampString.c_str());
      timestampString.replace("_", "T");
      Serial.println(timestampString.c_str());
      timestampString = timestampString.substring(19);
      Serial.println(timestampString.c_str());

      // telemetry.00000000.2000-00-01_454902.json
      // telemetry.00000000.2000-00-01T454902.json
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333
      //                    ^

      Serial.println(timestampString.c_str());

      // 2000-00-01T454902.json
      // 01234567890123456789012345678901234567890123456789
      // 0000000000111111111122222222223333333333
      // ------------- -- --Z

      timestampString = timestampString.substring(0, 13) + ":" + timestampString.substring(13, 15) + ":" + timestampString.substring(15, 17) + "Z";      
      Serial.println(timestampString);
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
                     "Content-Disposition: form-data; name=\"PendingTelemetry\"\r\n\r\n" + PendingTelemetry + "\r\n"
                     "--" + boundary + "\r\n"
                     "Content-Disposition: form-data; name=\"TemperatureC\"\r\n\r\n" + TemperatureC + "\r\n"
                     "--" + boundary + "\r\n"
                     "Content-Disposition: form-data; name=\"PendingImages\"\r\n\r\n" + PendingImages + "\r\n"
                     "--" + boundary + "\r\n"
                    //  "Content-Disposition: form-data; name=\"Status\"\r\n\r\n{\"status\":\"OK\", \"batteryVoltage\":" + doc["batteryVoltage"] + "}\r\n"
                     "Content-Disposition: form-data; name=\"Status\"\r\n\r\n" + Status + "\r\n"
                     "--" + boundary + "\r\n"
                     "Content-Disposition: form-data; name=\"DiskSpaceFree\"\r\n\r\n" + DiskSpaceFree + "\r\n"
                     "--" + boundary + "\r\n"
                     "Content-Disposition: form-data; name=\"Timestamp\"\r\n\r\n" + timestampString + "\r\n"
                     "--" + boundary + "\r\n"
                     "Content-Disposition: form-data; name=\"UptimeSeconds\"\r\n\r\n" + UptimeSeconds + "\r\n"
                     "--" + boundary + "\r\n"
                     "Content-Disposition: form-data; name=\"BatteryPercent\"\r\n\r\n" + BatteryPercent + "\r\n"
                     "--" + boundary + "\r\n"
                     "Content-Disposition: form-data; name=\"SerialNumber\"\r\n\r\n" + MACAddress + "\r\n"
                     "--" + boundary + "--\r\n";

      Serial.print(payload.c_str());
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
          if(line.indexOf("HTTP/1.1 200 OK") != -1){
            okResponse = true;
            break;
          }
          if(line.indexOf("HTTP/1.1 404 Not Found") != -1){
            NotFoundResponse = true;
            break;
          }
          if(line.indexOf("HTTP/1.1 302 Found") != -1){
            okResponse = true;
            logMessage("Got 302 rather than 200 - not sure why?");
            break;
          }
          Serial.println(line);
        }
      }
      client.stop();

      if(okResponse){
        // Delete the file
        Serial.println("Deleting file....");
        Serial.println(pendingFilename);
        ++filesUploaded;

        if (!SD.remove(pendingFilename)) {
          logError("Failed to delete file");
        }
      } else {
        if(NotFoundResponse){
          logMessage("404 - check device is registered.");
        } else {
          logMessage("Error sending data.");
        }
      }

    }
    // file = root.openNextFile();
  }
  // root.close();

  return filesUploaded == filesToUpload;
}

String* listAndSortFiles(const char* folder) {
  const int maxFiles = FileArraySize;
  String* filenames = new String[maxFiles];
  int fileCount = 0;

  if(!SD.exists(folder)){
    SD.mkdir(folder);
  }

  File root = SD.open(folder);
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
  Serial.println(entry.name());
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

int countFiles(const char* folder) {

  if(!SD.exists(folder)){
    SD.mkdir(folder);
  }

  File root = SD.open(folder);
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
  // if(rtcPresent){
    DateTime now = PRTCnow();
    char rtcTime[25];
    sprintf(rtcTime, ISO8061FormatString, now.year(), now.month(), now.day(), now.hour(), now.minute(), now.second());
    logMessage("RTC Time: %s", rtcTime);
  // }
}

void setup() {

  pinMode(LED_BUILTIN, OUTPUT);

  Serial.begin(115200);
  // while(!Serial); // When the serial monitor is turned on, the program starts to execute

  u8x8.begin();
  // u8x8.setFlipMode(1);   // set number from 1 to 3, the screen word will rotary 180

  u8x8.setFont(u8x8_font_chroma48medium8_r);
  // u8x8.setFont(u8x8_font_);

  u8x8log.begin(u8x8, U8LOG_WIDTH, U8LOG_HEIGHT, u8log_buffer);
  // u8x8log.setRedrawMode(0);		// 0: Update screen with newline, 1: Update screen for every char
  u8x8log.setRedrawMode(1);  // 0: Update screen with newline, 1: Update screen for every char


  // displayMessage("Checking RTC");
  if (!rtc.begin()) {
    rtcPresent=false;
    logError("Couldn't find RTC");
    // while (1) delay(10);
  } else {
    logMessage("Setting PseduoRTC from expected bootTime...");
    rtc.adjust(getPseudoRTCNow());
    logMessage("Adjusted RTC with pseudo real time.");
  }

  logRTC();



  // Initialize SD card
  if (!SD.begin(21)) {
    logError("Card Mount Failed");

    // logMessage("Going to sleep now");
    // Serial.flush();
    // esp_deep_sleep_start();

    enableWakeupAndGoToSleep();
    return;
  }
  uint8_t cardType = SD.cardType();

  // Determine if the type of SD card is available
  if (cardType == CARD_NONE) {
    logError("No SD card attached");

    // logMessage("Going to sleep now");
    // Serial.flush();
    // esp_deep_sleep_start();

    enableWakeupAndGoToSleep();
    return;
  }

  logMessage("SD Card Type: ");
  if (cardType == CARD_MMC) {
    logMessage("MMC");
  } else if (cardType == CARD_SD) {
    logMessage("SDSC");
  } else if (cardType == CARD_SDHC) {
    logMessage("SDHC");
  } else {
    logMessage("UNKNOWN");
  }

  sd_sign = true;  // sd initialization check passes
  // logMessage("SD Card mounted %'d", millis());
  logRTC();
  logMessage("SD Card mounted");

  // logMessage("sID: %s", sID);
  sprintf(MACAddress, "%012llx", ESP.getEfuseMac());

  if (bootCount % 5 == 0) {
    if(wifiConnect()){
      getNTPTime();
      uploadPendingImages();
      uploadPendingTelemetry();
    }
  }

  logMessage("MAC Address: %s", MACAddress);


  displayMessage("Camera startup");


  ++bootCount;
  logMessage("Boot number: %d", bootCount);

  // logMessage("Starting up %'d", millis());


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
  config.xclk_freq_hz = 5000000; // Clock too high resulting in grainy/noisy image? https://github.com/espressif/esp32-camera/issues/172
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


  // camera init
  esp_err_t err = esp_camera_init(&config);
  if (err != ESP_OK) {
    logError("Camera init failed with error 0x%x", err);

    enableWakeupAndGoToSleep();

    return;
  }

  sensor_t * s = esp_camera_sensor_get();
  // initial sensors are flipped vertically and colors are a bit saturated
  if (s->id.PID == OV3660_PID || s->id.PID == OV5640_PID) {
    s->set_vflip(s, 1); // flip it back
    s->set_brightness(s, 1); // up the brightness just a bit
    s->set_saturation(s, -2); // lower the saturation
  }

  camera_sign = true;  // Camera initialization check passes
  // logMessage("Camera connected %'d", millis());
  displayMessage("Camera ready");


  imageCounter = getCounter(counterFilenameImages);
  telemetryCounter = getCounter(counterFilenameTelemetry);
  // bootTime = getBootTime();
  // logMessage("imageCounter = %d\r\n", imageCounter);
  // logMessage("bootTime = %d\r\n", bootTime);

  // print_wakeup_reason();
  // print_wakeup_touchpad();

  savePhoto();
  saveTelemetry();
  // logMessage("Staying awake for 15s to ease flashing");
  // delay(15000);

  enableWakeupAndGoToSleep();
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



  // Timer Sleep:
  esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP * uS_TO_S_FACTOR);
  // logMessage("Setup ESP32 to sleep for  " + String(TIME_TO_SLEEP) + " Seconds");

  // External wakeup - RTC:
  esp_sleep_enable_ext0_wakeup(GPIO_NUM_33,1); //1 = High, 0 = Low

  logMessage("Going to sleep now");
  logRTC();
  Serial.flush();

  digitalWrite(LED_BUILTIN, LOW);  // XIAO ESP32S3 LOW = on
  delay(500);
  digitalWrite(LED_BUILTIN, HIGH);

  if (enableSleep) {
    DateTime expectedWakeup = PRTCnow();
    TimeSpan spanToSleep = TimeSpan(TIME_TO_SLEEP);
    expectedWakeup = expectedWakeup + spanToSleep;
    // setPseudoRTC(expectedWakeup.unixtime());
    bootTime=expectedWakeup.unixtime();
    esp_deep_sleep_start();
  } else {
    logMessage("enableSleep = false - not sleeping");
  }
}


void loop() {
  // Catch hang and shutdown
  // if (millis() >= 60000) {
  //   logMessage("We've been awake for 60s - must have hung, going to sleep now");
  //   Serial.flush();
  //   enableWakeupAndGoToSleep();
  // }
}
