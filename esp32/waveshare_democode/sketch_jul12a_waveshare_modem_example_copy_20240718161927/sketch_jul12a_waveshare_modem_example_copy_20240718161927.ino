#define TINY_GSM_MODEM_SIM7600
#include <TinyGsmClient.h>


// Set serial for debug console (to the Serial Monitor, default is 9600)
#define SerialMon Serial

// Set serial for AT commands (to the GSM module)
#define SerialAT Serial1

static const int RXPin = 17, TXPin = 18;
static const uint32_t GPSBaud = 115200;



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
  
  SentMessage("ATD10086;", 2000);
  SentSerial("ATE1;");
  SentSerial("AT+COPS?");
  //  14:46:03.547 -> +COPS: 0,2,"53001",7
  SentSerial("AT+CGDCONT?");
  // 14:46:03.580 -> +CGDCONT: 1,"IP","vodafone","100.70.101.247",,,,,,,,,,,
  SentSerial("AT+SIMCOMATI");
  // 14:46:03.580 -> Manufacturer: SIMCOM INCORPORATED
  // 14:46:03.580 -> Model: SIM7670G-MNGV
  // 14:46:03.580 -> Revision: 2360B01SIM767XM5A_M
  // 14:46:03.580 -> SIM767XM5A_B01V03_231207
  // 14:46:03.580 -> IMEI: 864643060060052
  // 14:46:03.580 -> 
}

void loop() {
  if (SerialAT.available()) {
    rev = SerialAT.readString();
    SerialMon.println(rev);
  }
}