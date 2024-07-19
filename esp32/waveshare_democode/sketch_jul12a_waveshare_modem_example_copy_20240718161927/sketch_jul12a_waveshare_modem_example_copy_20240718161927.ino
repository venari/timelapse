#define TINY_GSM_MODEM_SIM7600
#include <TinyGsmClient.h>


// Set serial for debug console (to the Serial Monitor, default is 9600)
#define SerialMon Serial

// Set serial for AT commands (to the GSM module)
#define SerialAT Serial1

static const int RXPin = 17, TXPin = 18;
static const uint32_t GPSBaud = 115200;



// Your SIM card credentials
const char apn[] = "vodafone";  // Set your APN
const char user[] = "";         // Set your username, if any
const char pass[] = "";         // Set your password, if any

// Server details
const char server[] = "httpbin.org"; // Server URL
const int port = 80;                 // Server port

// Initialize the TinyGSM modem
TinyGsm modem(SerialAT);
TinyGsmClient client(modem);

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

void setup() {
  SerialMon.begin(115200);
  SerialAT.begin(GPSBaud, SERIAL_8N1, RXPin, TXPin);


  while (!SentMessage("AT", 2000)) {
    delay(1000);
  }
  
  // SentSerial("ATE1;");
  // SentSerial("AT+COPS?");
  // // //  14:46:03.547 -> +COPS: 0,2,"53001",7
  // SentSerial("AT+CGDCONT?");
  // // // 14:46:03.580 -> +CGDCONT: 1,"IP","vodafone","100.70.101.247",,,,,,,,,,,
  // SentSerial("AT+SIMCOMATI");
  // // 14:46:03.580 -> Manufacturer: SIMCOM INCORPORATED
  // // 14:46:03.580 -> Model: SIM7670G-MNGV
  // // 14:46:03.580 -> Revision: 2360B01SIM767XM5A_M
  // // 14:46:03.580 -> SIM767XM5A_B01V03_231207
  // // 14:46:03.580 -> IMEI: 864643060060052
  // // 14:46:03.580 -> 

  // This seems to sometimes be necessary - e.g. after removing SIM card. Maybe optimise later if we can?
  SerialMon.println("Initializing modem...");
  modem.restart();
  
  
  // return;

  // This bit always hangs for a minute and then fails...
  
  // SerialMon.print("Connecting to network...");
  // if (!modem.waitForNetwork()) {
  //   SerialMon.println(" fail");
  //   // while (true);
  //   return;
  // }
  // SerialMon.println(" success");

  // GPRS connection
  SerialMon.print(F("Connecting to "));
  SerialMon.print(apn);
  if (!modem.gprsConnect(apn, user, pass)) {
    SerialMon.println(" fail");
    // while (true);
    return;
  }
  SerialMon.println(" success");

  // Make HTTP GET request
  if (!client.connect(server, port)) {
    SerialMon.println("Connection to server failed");
    return;
  }

  // Make a HTTP GET request
  client.print(String("GET /get HTTP/1.1\r\n") +
               "Host: " + server + "\r\n" +
               "Connection: close\r\n\r\n");

  // Wait for server response
  unsigned long timeout = millis();
  while (client.connected() && millis() - timeout < 10000) {
    while (client.available()) {
      String line = client.readStringUntil('\r');
      SerialMon.print(line);
      timeout = millis();
    }
  }

  // Close the connection
  client.stop();
  SerialMon.println(F("Server disconnected"));




}

static const unsigned long REFRESH_INTERVAL = 5000; // ms
static unsigned long lastRefreshTime = 0;

void loop() {

  if(millis() - lastRefreshTime >= REFRESH_INTERVAL)
  {
  	lastRefreshTime += REFRESH_INTERVAL;
    Serial.print(".");
  }

  if (SerialAT.available()) {
    rev = SerialAT.readString();
    SerialMon.println(rev);
  }
}