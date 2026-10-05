#include "wifi_functions.h"
#include "log_functions.h"

const char *ssid = SECRET_SSID;
const char *password = SECRET_PASS;

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
