#ifndef SD_FUNCTIONS_H
#define SD_FUNCTIONS_H

#include <arduino.h>
#include <SD_MMC.h>
#include "log_functions.h"
#include "status_functions.h"

extern const int SDMMC_CLK;
extern const int SDMMC_CMD;
extern const int SDMMC_DATA;
extern const int SD_CD_PIN;

extern bool sd_sign;

extern const int FileArraySize;
extern const int ImageFileBatchSize;
extern const int TelemetryFileBatchSize;

extern const char *counterFilenameBoot;
extern const char *counterFilenameImages;
extern const char *counterFilenamePendingImages;
extern const char *counterFilenameTelemetry;
extern const char *counterFilenamePendingTelemetry;
extern const char *logFilename;

extern const char *pendingImageFolder;

extern const char *pendingTelemetryFolder;


void updateCounter(const char *counterFilename, int count);
int getCounter(const char *counterFilename);
void writeFile(fs::FS &fs, const char *path, uint8_t *data, size_t len);
String *listAndSortFiles(const char *folder);
int countFiles(const char *folder);

void setupSD();

#endif