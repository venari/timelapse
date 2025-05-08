#ifndef LOG_FUNCTIONS_H
#define LOG_FUNCTIONS_H

#include <arduino.h>
#include "sd_functions.h"

void logError(const char *format, ...);
void logMessage(const char *format, ...);
void logMessage(const char *format, va_list args);

#endif