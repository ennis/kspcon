#include "Arduino.h"
#include <KerbalSimpit.h>

// Arduino pin numbers
const int SW_pin = 2; // digital pin connected to switch output
const int X_pin = 0; // analog pin connected to X output
const int Y_pin = 1; // analog pin connected to Y output

const int PIN_LED_SAS = 8;
const int PIN_LED_RCS = 9;

KerbalSimpit kspit(Serial);

void kspit_handler(byte msg_type, byte* msg, byte msg_size) {
  switch (msg_type) {
    case ACTIONSTATUS_MESSAGE:
      // Update SAS and RCS leds
      digitalWrite(PIN_LED_SAS, (*msg & SAS_ACTION) ? HIGH : LOW);
      digitalWrite(PIN_LED_RCS, (*msg & RCS_ACTION) ? HIGH : LOW); 
      break;
  }
}


void display_test() {
  // flash all LEDs for 1 second 
  digitalWrite(PIN_LED_SAS, HIGH);
  digitalWrite(PIN_LED_RCS, HIGH);
  delay(1000);
  digitalWrite(PIN_LED_SAS, LOW);
  digitalWrite(PIN_LED_RCS, LOW);
}

void setup() {
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);

  // SAS/RCS leds
  pinMode(PIN_LED_SAS, OUTPUT);
  pinMode(PIN_LED_RCS, OUTPUT);

  // setup joystick switch pin
  pinMode(SW_pin, INPUT_PULLUP);

  // perform display test
  display_test();

  // setup kerbalsimpit (wait for KSP)
  Serial.begin(115200);
  while (!kspit.init());
  kspit.inboundHandler(&kspit_handler);
  kspit.registerChannel(ACTIONSTATUS_MESSAGE);

  // blink LED to signify success
  digitalWrite(LED_BUILTIN, HIGH); delay(200);
  digitalWrite(LED_BUILTIN, LOW); delay(200);
  digitalWrite(LED_BUILTIN, HIGH); delay(200);
  digitalWrite(LED_BUILTIN, LOW); delay(200);
  digitalWrite(LED_BUILTIN, HIGH); delay(200);  
}

void loop() {
  kspit.update();
}
