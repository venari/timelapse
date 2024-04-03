#include "esp_camera.h"
#include "FS.h"
#include "SD.h"
#include "SPI.h"

#define CAMERA_MODEL_XIAO_ESP32S3 // Has PSRAM

#include "camera_pins.h"


#define uS_TO_S_FACTOR 1000000ULL  /* Conversion factor for micro seconds to seconds */
#define TIME_TO_SLEEP  10        /* Time ESP32 will go to sleep (in seconds) */

#if CONFIG_IDF_TARGET_ESP32
  #define THRESHOLD   40      /* Greater the value, more the sensitivity */
#else //ESP32-S2 and ESP32-S3 + default for other chips (to be adjusted) */
  #define THRESHOLD   5000   /* Lower the value, more the sensitivity */
#endif


RTC_DATA_ATTR int bootCount = 0;
touch_pad_t touchPin;


unsigned long lastCaptureTime = 0; // Last shooting time
int imageCount = 1;                // File Counter
bool camera_sign = false;          // Check camera status
bool sd_sign = false;              // Check sd status
const char *counterFilename = "/counter";
const char *logFilename = "/log.txt";

void logError(const char *format, ...){
  va_list args; // Create a variable argument list
  va_start(args, format); // Initialize the variable argument list
  logMessage(format, args); // Call the function that takes a variable argument list
  va_end(args); // End the variable argument list
}

// logMessage function with variable arguments
void logMessage(const char *format, ...){
  va_list args; // Create a variable argument list
  va_start(args, format); // Initialize the variable argument list
  logMessage(format, args); // Call the function that takes a variable argument list
  va_end(args); // End the variable argument list
}

void logMessage(const char *format, va_list args){

  char buf[128]; // Allocate a buffer to store the message
  vsnprintf(buf, sizeof(buf), format, args); // Write the formatted string to the buffer
  Serial.println(buf); // Print the buffer to the serial port

  if(sd_sign == false){
    Serial.println("SD Card not mounted yet.");
    return;
  }
  
  // Serial.println("A");
  // Write the current count to the counter file
  File file = SD.open(logFilename, FILE_APPEND);
  // Serial.println("B");
  if(!file){
      // Serial.println("C");

    Serial.println("Failed to open log file for writing");
      // Serial.println("D");

    return;
  }
  // Serial.println("E");

  // file.seek(EOF);
  // Serial.println("F");
  if(file.println(buf)){
      file.close();
      // Serial.println("G");

    // Serial.println("Counter updated");
  } else {
      // Serial.println("H");

    Serial.println("Failed to append to log");
  }
    // Serial.println("I");

}


void updateCounter(int count){
  // Write the current count to the counter file
  File file = SD.open(counterFilename, FILE_WRITE);
  if(!file){
    logMessage("Failed to open counter file for writing");
    return;
  }
  if(file.print(count)){
    // Serial.println("Counter updated");
  } else {
    logError("Failed to update counter");
  }
}

int getCounter(){
  // Read the current count from the counter file
  File file = SD.open(counterFilename);
  if(!file){
    logMessage("Failed to open counter file for reading");
    return 0;
  }
  int count = file.parseInt();
  file.close();
  return count;
}

// Save pictures to SD card
void photo_save(const char * fileName) {
  // Take a photo
  
  // Serial.println("Starting photo_save()");
  digitalWrite(LED_BUILTIN, LOW); // XIAO ESP32S3 LOW = on
  // delay(500);

  camera_fb_t *fb = esp_camera_fb_get();
  if (!fb) {
    logError("Failed to get camera frame buffer");
    return;
  }
  // Save photo to file
  writeFile(SD, fileName, fb->buf, fb->len);
  
  // Release image buffer
  esp_camera_fb_return(fb);

  digitalWrite(LED_BUILTIN, HIGH);
  // delay(500);

  // Serial.println("Photo saved to file");
}

// SD card write file
void writeFile(fs::FS &fs, const char * path, uint8_t * data, size_t len){
    // Serial.printf("Writing file: %s\r\n", path);

    File file = fs.open(path, FILE_WRITE);
    if(!file){
        logError("Failed to open file for writing");
        return;
    }
    if(file.write(data, len) == len){
        // Serial.println("File written");
    } else {
        logError("Write failed");
    }
    file.close();
}

void print_wakeup_reason(){
  esp_sleep_wakeup_cause_t wakeup_reason;

  wakeup_reason = esp_sleep_get_wakeup_cause();

  switch(wakeup_reason)
  {
    case ESP_SLEEP_WAKEUP_EXT0 : logMessage("Wakeup caused by external signal using RTC_IO"); break;
    case ESP_SLEEP_WAKEUP_EXT1 : logMessage("Wakeup caused by external signal using RTC_CNTL"); break;
    case ESP_SLEEP_WAKEUP_TIMER : logMessage("Wakeup caused by timer"); break;
    case ESP_SLEEP_WAKEUP_TOUCHPAD : logMessage("Wakeup caused by touchpad"); break;
    case ESP_SLEEP_WAKEUP_ULP : logMessage("Wakeup caused by ULP program"); break;
    default : logMessage("Wakeup was not caused by deep sleep: %d\n",wakeup_reason); break;
  }
}

void print_wakeup_touchpad(){
  touchPin = esp_sleep_get_touchpad_wakeup_status();

  #if CONFIG_IDF_TARGET_ESP32
    switch(touchPin)
    {
      case 0  : logMessage("Touch detected on GPIO 4"); break;
      case 1  : logMessage("Touch detected on GPIO 0"); break;
      case 2  : logMessage("Touch detected on GPIO 2"); break;
      case 3  : logMessage("Touch detected on GPIO 15"); break;
      case 4  : logMessage("Touch detected on GPIO 13"); break;
      case 5  : logMessage("Touch detected on GPIO 12"); break;
      case 6  : logMessage("Touch detected on GPIO 14"); break;
      case 7  : logMessage("Touch detected on GPIO 27"); break;
      case 8  : logMessage("Touch detected on GPIO 33"); break;
      case 9  : logMessage("Touch detected on GPIO 32"); break;
      default : logMessage("Wakeup not by touchpad"); break;
    }
  #else
    if(touchPin < TOUCH_PAD_MAX)
    {
      logMessage("Touch detected on GPIO %d\n", touchPin); 
    }
    else
    {
      logMessage("Wakeup not by touchpad");
    }
  #endif
}

void setup() {

  pinMode(LED_BUILTIN, OUTPUT);

  Serial.begin(115200);
  // while(!Serial); // When the serial monitor is turned on, the program starts to execute

  ++bootCount;
  logMessage("Boot number: %d", bootCount);




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
  config.frame_size = FRAMESIZE_UXGA;
  config.pixel_format = PIXFORMAT_JPEG; // for streaming
  config.grab_mode = CAMERA_GRAB_WHEN_EMPTY;
  config.fb_location = CAMERA_FB_IN_PSRAM;
  config.jpeg_quality = 12;
  config.fb_count = 1;
  
  // if PSRAM IC present, init with UXGA resolution and higher JPEG quality
  //                      for larger pre-allocated frame buffer.
  if(config.pixel_format == PIXFORMAT_JPEG){
    if(psramFound()){
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

    logMessage("Going to sleep now");
    Serial.flush(); 
    esp_deep_sleep_start();


    return;
  }
  
  camera_sign = true; // Camera initialization check passes

  // Initialize SD card
  if(!SD.begin(21)){
    logError("Card Mount Failed");

    // logMessage("Going to sleep now");
    // Serial.flush(); 
    // esp_deep_sleep_start();

    return;
  }
  uint8_t cardType = SD.cardType();

  // Determine if the type of SD card is available
  if(cardType == CARD_NONE){
    logError("No SD card attached");

    // logMessage("Going to sleep now");
    // Serial.flush(); 
    // esp_deep_sleep_start();

    return;
  }

  logMessage("SD Card Type: ");
  if(cardType == CARD_MMC){
    logMessage("MMC");
  } else if(cardType == CARD_SD){
    logMessage("SDSC");
  } else if(cardType == CARD_SDHC){
    logMessage("SDHC");
  } else {
    logMessage("UNKNOWN");
  }

  sd_sign = true; // sd initialization check passes

  imageCount = getCounter();
  logMessage("imageCount = %d\r\n", imageCount);



  char filename[32];
  sprintf(filename, "/image%d.jpg", imageCount);
  photo_save(filename);
  logMessage("Saved picture: %s\r\n", filename);
  updateCounter(++imageCount);
  // lastCaptureTime = now;







  //Print the wakeup reason for ESP32
  print_wakeup_reason();
  print_wakeup_touchpad();

  #if CONFIG_IDF_TARGET_ESP32 
  //Setup sleep wakeup on Touch Pad 3 + 7 (GPIO15 + GPIO 27) 
  touchSleepWakeUpEnable(T3,THRESHOLD);
  touchSleepWakeUpEnable(T7,THRESHOLD);
  
  #else //ESP32-S2 + ESP32-S3
  //Setup sleep wakeup on Touch Pad 3 (GPIO3) 
  touchSleepWakeUpEnable(T3,THRESHOLD);

  #endif



  // Timer Sleep:
    // esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP * uS_TO_S_FACTOR);
    // Serial.println("Setup ESP32 to sleep for every " + String(TIME_TO_SLEEP) +
    // " Seconds");


  // External wakeup - RTC:
    // esp_sleep_enable_ext0_wakeup(GPIO_NUM_33,1); //1 = High, 0 = Low



  logMessage("Going to sleep now");
  Serial.flush(); 
  esp_deep_sleep_start();


}

void loop() {
}
