#ifndef STATUS_FUNCTIONS_H
#define STATUS_FUNCTIONS_H

#include <arduino.h>
#include <SD_MMC.h>
#include <Adafruit_NeoPixel.h>
#include "log_functions.h"

#define STATUS_INITIALISING 0
#define STATUS_CONNECTING_TO_WIFI 1
#define STATUS_UPLOADING 2
#define STATUS_UPLOAD_COMPLETE 3
#define STATUS_INITIALISING_CAMERA 4
#define STATUS_SAVING_PHOTO 5
#define STATUS_SAVING_TELEMETRY 6
#define STATUS_COMPLETE 99

extern int currentStatus;

#define ERROR_BLINK_NO_RTC -3
#define ERROR_BLINK_SDCARD_MOUNT_FAILED -4
#define ERROR_BLINK_SDCARD_MOUNT_TYPE_NONE -5
#define ERROR_BLINK_CAMERA_INIT_FAILED -6
#define ERROR_BLINK_WIFI_CONNECTION_FAILED -7
#define ERROR_BLINK_WIFI_UPLOAD_FAILED -8

void setupStatus();
void flash(int R, int G, int B, int numFlashes, int msDuration = 300, int numRepeats=0);
void displayStatus();
void clearStatus();

#endif