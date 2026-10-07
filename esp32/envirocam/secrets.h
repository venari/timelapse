/**
 * @file      secrets.h
 * @note      Fill in your WiFi credentials in secrets.cpp
 *            Do not commit real credentials to source control.
 */
#pragma once

extern const char *WIFI_SSID;
extern const char *WIFI_PASSWORD;

// Cellular fallback (see cellularConnect() in main.cpp) - used only when WiFi association fails.
// APN for the SIM fitted; leave user/pass as "" when the carrier doesn't require them (One NZ
// doesn't). An apn value set on the API's Device row overrides CELLULAR_APN at runtime.
extern const char *CELLULAR_APN;
extern const char *CELLULAR_APN_USER;
extern const char *CELLULAR_APN_PASS;
