#ifndef WIFI_FUNCTIONS_H
#define WIFI_FUNCTIONS_H

#include <arduino.h>
#include <WiFi.h>
#include "arduino_secrets.h"

extern const char *ssid;
extern const char *password;

bool wifiConnect();

#endif