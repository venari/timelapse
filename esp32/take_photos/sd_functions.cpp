#include "sd_functions.h"

bool sd_sign=false;      // Check sd status

const int SDMMC_CLK = 5;
const int SDMMC_CMD = 4;
const int SDMMC_DATA = 6;
const int SD_CD_PIN = 46;

const int FileArraySize = 100;
const int ImageFileBatchSize = 10;
const int TelemetryFileBatchSize = 100;

const char *counterFilenameBoot = "/counterBoot";
const char *counterFilenameImages = "/counterImages";
const char *counterFilenamePendingImages = "/counterPendingImages";
const char *counterFilenameTelemetry = "/counterTelemetry";
const char *counterFilenamePendingTelemetry = "/counterPendingTelemetry";
const char *logFilename = "/log.txt";

const char *pendingImageFolder = "/pendingImages";

const char *pendingTelemetryFolder = "/pendingTelemetry";

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

void setupSD(){
  // sd_sign = false;      // Check sd status
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
}