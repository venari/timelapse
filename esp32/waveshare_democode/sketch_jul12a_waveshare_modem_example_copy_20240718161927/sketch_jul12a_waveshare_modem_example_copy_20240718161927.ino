#define TINY_GSM_MODEM_SIM7600
// See all AT commands, if wanted
#define DUMP_AT_COMMANDS

// Set serial for debug console (to the Serial Monitor, default is 9600)
#define SerialMon Serial

// Set serial for AT commands (to the GSM module)
#define SerialAT Serial1


// Define the serial console for debug prints, if needed
#define TINY_GSM_DEBUG SerialMon

// #include <TinyGsmClient.h>
#include <TinyGSM.h>
#include <ArduinoHttpClient.h>
#include <SSLClient.h>
#include "certs.h"
#include <UrlEncode.h>

#include <WiFi.h>
#include "arduino_secrets.h"


static const int RXPin = 17, TXPin = 18;
static const uint32_t GPSBaud = 115200;



// Your SIM card credentials
const char apn[] = "vodafone";  // Set your APN
const char user[] = "";         // Set your username, if any
const char pass[] = "";         // Set your password, if any

// Server details
// const char server[] = "httpbin.org"; // Server URL
// const char server[] = "eo7ogadymqhz0vo.m.pipedream.net";
const char server[] = "timelapse-dev.azurewebsites.net";
// const char server[] = "webhook.site";
const char resource[] = "/api/Test";
// const char resource[] = "/f31a289a-e938-46f3-b3a7-2851e5605779";

const int port = 80;                 // Server port

// Initialize the TinyGSM modem
TinyGsm modem(SerialAT);

// Add GovoroxSSLClient
// https://github.com/govorox/SSLClient
// esp32 board v2.0.17 - v3.0.1 gives https://github.com/govorox/SSLClient/issues/93 (fatal error: mbedtls/net.h: No such file or directory)
// TinyGsmClient client(modem);
// TinyGsmClient transport(modem);
// SSLClient client(&transport);

// From https://github.com/govorox/SSLClient/tree/master/examples/Esp32-Arduino-IDE/https_post_sim7600
// HTTPS Transport

// TinyGsmClient base_client(modem, 0);
// SSLClient secure_layer(&base_client);
// HttpClient client = HttpClient(secure_layer, server, port);

WiFiClient wifi;
HttpClient client = HttpClient(wifi, server, port);
int status = WL_IDLE_STATUS;

String rev;

void SentSerial(const char *p_char) {
  for (int i = 0; i < strlen(p_char); i++) {
    SerialAT.write(p_char[i]);
    delay(10);
  }
  SerialAT.write('\r');
  delay(10);
  SerialAT.write('\n');
  delay(10);
}

bool SentMessage(const char *p_char, unsigned long timeout = 2000) {
  SentSerial(p_char);

  unsigned long start = millis();
  while (millis() - start < timeout) {
    if (SerialAT.available()) {
      rev = SerialAT.readString();
      if (rev.indexOf("OK") != -1) {
        SerialMon.println("Got OK!");
        return true;
      }
    }
  }
  SerialMon.println("Timeout!");
  return false;
}

bool SendDebugMessageToServer(String message){
  SerialMon.println("SendDebugMessageToServer()");
  SerialMon.println(message);
  // SerialMon.flush();

  // if(!client.connected()){
  //   SerialMon.print("Connecting to ");
  //   SerialMon.print(server);

  //   // client.setInsecure();  // Disable SSL certificate verification - doesn't speed anything up

  //   if (!client.connect(server, port)) {
  //     SerialMon.println(" failed");
  //     return false;
  //   }
  //   SerialMon.println(" success");
  // }

  // SerialMon.println("connected");

  // String boundary = "----WebKitFormBoundary" + String(random(0xFFFFFF), HEX);

  // String start_request = "--" + boundary + "\r\n";
  // start_request += "Content-Disposition: form-data; name=\"message\"\r\n\r\n";
  // start_request += message;
  // start_request += "\r\n";
  
  // String end_request = "\r\n--" + boundary + "--\r\n";

  // int fileLength = 0; //file.size();
  // int contentLength = start_request.length() + fileLength + end_request.length();

  // client.printf("POST /api/Test HTTP/1.1\r\n");
  // client.printf("Host: %s\r\n", serverName);
  // client.printf("Content-Type: multipart/form-data; boundary=%s\r\n", boundary.c_str());
  // client.printf("Content-Length: %d\r\n", contentLength);
  // client.printf("Connection: close\r\n\r\n");

  // client.print(start_request);
  // client.print(end_request);

  // SerialMon.println(start_request);

  // SerialMon.println("Request sent");

  // // Wait for server response
  // unsigned long timeout = millis();
  // while (client.connected() && millis() - timeout < 10000) {
  //   while (client.available()) {
  //     String line = client.readStringUntil('\r');
  //     SerialMon.print(line);
  //     timeout = millis();
  //   }
  //   SerialMon.print(".");
  // }

  // Retrieve Time
  String time;
    do {
      time = modem.getGSMDateTime(DATE_FULL).substring(0, 17);
      delay(100);
  } while (!time);
  DBG("Current Network Time:", time);

  // Retrieve Temperature
  float temp;
  do {
      temp = modem.getTemperature();
      delay(100);
  } while (!temp);
  DBG("Modem Temp:", temp);


  // if (!modem.isGprsConnected()) {
  //     DBG("... not connected");
  // } else {

    // DBG("Connecting to ", server);
    // Make a HTTPS POST request:
    Serial.println("Making POST request securely");
    // String contentType = "Content-Type: application/json";
    String contentType = "Content-Type: x-www-form-urlencoded";
    // String postData = "{\"time\": \""+ time + "\", \"temp\": \"" + temp + "\"}";
    // String postData = "Hi from ESP32 ArduinoHttpClient";
    String postData = "message="+message;
    Serial.println(postData);

    String URL = (String)resource + "?message=" + urlEncode(message);
    Serial.println(URL);
    client.post(URL);

    // client.post(resource, contentType, postData);

    int status_code = client.responseStatusCode();
    String response = client.responseBody();
    
    Serial.print("Status code: ");
    Serial.println(status_code);
    Serial.print("Response: ");
    Serial.println(response);

    client.stop();        

  // }

  SerialMon.println("Response received");

  return true;

  // Close the connection
  // client.stop();
  // SerialMon.println(F("Server disconnected"));
}

void setup() {
  SerialMon.begin(115200);
  SerialAT.begin(GPSBaud, SERIAL_8N1, RXPin, TXPin);

      SerialMon.print("Attempting to connect to Network named: ");
      SerialMon.println(ssid);                   // print the network name (SSID);
      // Serial.println(password);                   // print the network name (SSID);

      WiFi.begin(ssid, password);
      while ( WiFi.status() != WL_CONNECTED) {


        delay(3000);
        // displayMessageNoNewline(".");
        SerialMon.print(".");

      }

      // print the SSID of the network you're attached to:
      SerialMon.print("SSID: ");
      SerialMon.println(WiFi.SSID());

      // print your WiFi shield's IP address:
      IPAddress ip = WiFi.localIP();
      SerialMon.print("IP Address: ");
      SerialMon.println(ip);

      SendDebugMessageToServer("Sending debug message");



  while (!SentMessage("AT", 2000)) {
    delay(1000);
  }
  
  SentSerial("ATE1;");
  SentSerial("AT+COPS?");
  // //  14:46:03.547 -> +COPS: 0,2,"53001",7
  SentSerial("AT+CGDCONT?");
  // // 14:46:03.580 -> +CGDCONT: 1,"IP","vodafone","100.70.101.247",,,,,,,,,,,

  // SentSerial("AT+CNMP=?");
  // SentSerial("AT+CNMP?");

  // SentSerial("AT+CPSI=?");
  // SentSerial("AT+CPSI?");

  SentSerial("AT+CGPSINFO=?");

  SentSerial("AT+CGPSINFO?");


  SentSerial("AT+SIMCOMATI");
  // 14:46:03.580 -> Manufacturer: SIMCOM INCORPORATED
  // 14:46:03.580 -> Model: SIM7670G-MNGV
  // 14:46:03.580 -> Revision: 2360B01SIM767XM5A_M
  // 14:46:03.580 -> SIM767XM5A_B01V03_231207
  // 14:46:03.580 -> IMEI: 864643060060052
  // 14:46:03.580 -> 


  if (SerialAT.available()) {
    rev = SerialAT.readString();
    while(rev!=""){
      SerialMon.println(rev);
      rev = SerialAT.readString();
    }
  }

  //Add CA Certificate
  // secure_layer.setCACert(root_ca_azure_websites_net);
  // secure_layer.setCACert(root_ca_webhook_site);


  // This seems to sometimes be necessary - e.g. after removing SIM card. Maybe optimise later if we can?

  SerialMon.print(F("Connecting to "));
  SerialMon.print(apn);
  if (!modem.gprsConnect(apn, user, pass)) {
    SerialMon.println(" fail");

    SerialMon.println("Initializing modem...");
    modem.restart();
    
    SerialMon.print(F("Attempting to connect again to "));
    SerialMon.print(apn);
    if (!modem.gprsConnect(apn, user, pass)) {
      SerialMon.println(" fail");

      return;
    }
    SerialMon.println(" success (2nd time)");
  }
  SerialMon.println(" success");


  
  // return;

  // This bit always hangs for a minute and then fails...
  
  // SerialMon.print("Connecting to network...");
  // if (!modem.waitForNetwork()) {
  //   SerialMon.println(" fail");
  //   // while (true);
  //   return;
  // }
  // SerialMon.println(" success");

  // // GPRS connection
  // SerialMon.print(F("Connecting to "));
  // SerialMon.print(apn);
  // if (!modem.gprsConnect(apn, user, pass)) {
  //   SerialMon.println(" fail");
  //   // while (true);
  //   return;
  // }
  // SerialMon.println(" success");

  // Make HTTP GET request
  // Make a HTTP GET request


  // Serial.println("A");
  SendDebugMessageToServer("Enabling GPS");
  // Serial.println("B");
  // SendDebugMessageToServer("1");
  // Serial.println("C");
  // SendDebugMessageToServer("2");
  // Serial.println("D");
  // SendDebugMessageToServer("3");
  // Serial.println("E");



  // SerialMon.println(F("Enabling GPS"));
  modem.enableGPS();
  SerialMon.println(F("Waiting 15s"));
  delay(15000L);
  SerialMon.println("....>");
  float gps_latitude  = 0;
  float gps_longitude = 0;
  float gps_speed     = 0;
  float gps_altitude  = 0;
  int   gps_vsat      = 0;
  int   gps_usat      = 0;
  float gps_accuracy  = 0;
  int   gps_year      = 0;
  int   gps_month     = 0;
  int   gps_day       = 0;
  int   gps_hour      = 0;
  int   gps_minute    = 0;
  int   gps_second    = 0;
  for (int8_t i = 15; i; i--) {
    SerialMon.println("Requesting current GPS/GNSS/GLONASS location");
    if (modem.getGPS(&gps_latitude, &gps_longitude, &gps_speed, &gps_altitude,
                     &gps_vsat, &gps_usat, &gps_accuracy, &gps_year, &gps_month,
                     &gps_day, &gps_hour, &gps_minute, &gps_second)) {
      SerialMon.printf("\nLatitude:", String(gps_latitude, 8),
          "\tLongitude:", String(gps_longitude, 8));
      SerialMon.printf("\nSpeed:", gps_speed, "\tAltitude:", gps_altitude);
      SerialMon.printf("\nVisible Satellites:", gps_vsat, "\tUsed Satellites:", gps_usat);
      SerialMon.printf("\nAccuracy:", gps_accuracy);
      SerialMon.printf("\nYear:", gps_year, "\tMonth:", gps_month, "\tDay:", gps_day);
      SerialMon.printf("\nHour:", gps_hour, "\tMinute:", gps_minute, "\tSecond:", gps_second);
      SendDebugMessageToServer("Got GPS position");
      SendDebugMessageToServer("Latitude:");
      SendDebugMessageToServer(String(gps_latitude, 8));
      SendDebugMessageToServer("Longitude:");
      SendDebugMessageToServer(String(gps_longitude, 8));
      
    break;
    } else {
      SendDebugMessageToServer("Couldn't get GPS/GNSS/GLONASS location, retrying in 15s.");
      // SerialMon.println("Couldn't get GPS/GNSS/GLONASS location, retrying in 15s.");
      delay(15000L);
    }
  }
  SerialMon.println("gps_raw...");
  String gps_raw = modem.getGPSraw();
  SerialMon.println(gps_raw);

  SendDebugMessageToServer("gps_raw");
  SendDebugMessageToServer(gps_raw);


}

static const unsigned long REFRESH_INTERVAL = 5000; // ms
static unsigned long lastRefreshTime = 0;

void loop() {

  if(millis() - lastRefreshTime >= REFRESH_INTERVAL)
  {
  	lastRefreshTime += REFRESH_INTERVAL;
    Serial.print(".");

    SendDebugMessageToServer("Hello from ESP32");
  }

  if (SerialAT.available()) {
    rev = SerialAT.readString();
    SerialMon.println(rev);
  }
}