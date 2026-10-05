#include "log_functions.h"

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