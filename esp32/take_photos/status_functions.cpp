#include "status_functions.h"

int currentStatus = STATUS_INITIALISING;
Adafruit_NeoPixel pixels = Adafruit_NeoPixel(1, 38, NEO_RGB + NEO_KHZ800);

void setupStatus(){
  pixels.begin();
  pixels.setPixelColor(0, pixels.Color(0, 0, 255));
  pixels.show();
}

void flash(int R, int G, int B, int numberOfFlashes, int msDuration, int numRepeats){
    Serial.printf("numberOfFlashes: %d\n", numberOfFlashes);
    for(int repeatSequence = 0; repeatSequence <= numRepeats; ++repeatSequence){
      for(int i =0; i < numberOfFlashes; ++i){
        Serial.println("R");
        for(; R >=0 || G >= 0 || B >-0;  R-=30, G-=30, B-=30){
          pixels.setPixelColor(0, pixels.Color(R, G, B));
          pixels.show();
          delay(10);
        }
        pixels.setPixelColor(0, pixels.Color(0, 0, 0));
        pixels.show();
        Serial.println("-");
        delay(msDuration);
        // Serial.println("DONE 1");
      }
      delay(1000);
    }

}

void displayStatus() {
  pixels.clear();

  Serial.printf("currentStatus: %d\n", currentStatus);
  
  if(currentStatus>0){

    switch(currentStatus){
      case STATUS_INITIALISING:
        pixels.setPixelColor(0, pixels.Color(255, 255, 255));
        pixels.show();
        break;
      case STATUS_CONNECTING_TO_WIFI:
        pixels.setPixelColor(0, pixels.Color(0, 0, 255));
        pixels.show();
        break;
      case STATUS_UPLOADING:
        pixels.setPixelColor(0, pixels.Color(0, 0, 255));
        pixels.show();
        break;
      case STATUS_SAVING_PHOTO:
        pixels.setPixelColor(0, pixels.Color(0, 50, 50));
        pixels.show();
        break;
      case STATUS_SAVING_TELEMETRY:
        pixels.setPixelColor(0, pixels.Color(0, 50, 50));
        pixels.show();
        break;
      case STATUS_COMPLETE:
        pixels.setPixelColor(0, pixels.Color(0, 255, 0));
        pixels.show();
        break;
      default:
        pixels.setPixelColor(0, pixels.Color(255, 0, 0));
        pixels.show();
        break;
    }
  } else {
    // ERROR condition.
    int numberOfFlashes = abs(currentStatus);
    flash(255, 0, 0, numberOfFlashes, 300, 3);
  }
}

void clearStatus() {
  pixels.clear();
  pixels.show();
}
