#include <Arduino.h>
#include <LedControl.h>
#include <ptpio.h>

// Arduino pin numbers
const uint8_t SW_pin = 2; // digital pin connected to switch output
const uint8_t X_pin = 0;  // analog pin connected to X output
const uint8_t Y_pin = 1;  // analog pin connected to Y output

// LED driver
const uint8_t PIN_LEDCTRL_DATA = 12;
const uint8_t PIN_LEDCTRL_CLK = 10;
const uint8_t PIN_LEDCTRL_CS = 11;
// Analog thrust lever
const uint8_t PIN_THROTTLE = A0;

// LED Controller
LedControl lc{/*data*/ PIN_LEDCTRL_DATA, /*clk*/ PIN_LEDCTRL_CLK, /*cs*/ PIN_LEDCTRL_CS};

enum ptp_in_address_t
{
  ADDRIN_LED0 = 0, // 0:SAS, 1:RCS, 2..5:???, 6:LGT, 7:CABIN LGT
  ADDRIN_LED1 = 1, // 0:???, 1: L/G DN, 2:CTRL, 3:STG LOCK, 4:???, 5:ENG THRUST, 6:BRAKE, 7:???
  ADDRIN_LED2 = 2, // 0-7:AP MODE
  ADDRIN_LED3 = 3, // 0-1:AP MODE
  ADDRIN_CUSTOM_ACTION_GROUP = 4,
};

enum ptp_out_address_t
{
  ADDROUT_AUTOPILOT = 8,
  ADDROUT_THROTTLE = 9,
  ADDROUT_SAS = 10,
  ADDROUT_RCS = 11,
};

void display_test()
{
  lc.setRow(0, 0, 0xFF);
  lc.setRow(0, 1, 0xFF);
  lc.setRow(0, 2, 0xFF);
  lc.setRow(0, 3, 0xFF);
  lc.setRow(0, 4, 0xFF);
  lc.setRow(0, 5, 0xFF);
  lc.setRow(0, 6, 0xFF);
  lc.setRow(0, 7, 0xFF);
  delay(1000);
  lc.setRow(0, 0, 0x00);
  lc.setRow(0, 1, 0x00);
  lc.setRow(0, 2, 0x00);
  lc.setRow(0, 3, 0x00);
  lc.setRow(0, 4, 0x00);
  lc.setRow(0, 5, 0x00);
  lc.setRow(0, 6, 0x00);
  lc.setRow(0, 7, 0x00);
}

void setup()
{
  pinMode(2, OUTPUT);
  pinMode(3, OUTPUT);
  pinMode(8, INPUT);
  digitalWrite(2, HIGH);
  //digitalWrite(3, HIGH);

  /*pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);

  // LED controller
  lc.shutdown(0, false);
  lc.setIntensity(0, 8);
  lc.clearDisplay(0);

  // setup joystick switch pin
  pinMode(SW_pin, INPUT_PULLUP);

  // perform display test
  display_test();

  // setup serial communication
  Serial.begin(115200);
  ptp_init();

  // blink LED to signify success
  digitalWrite(LED_BUILTIN, HIGH);
  delay(200);
  digitalWrite(LED_BUILTIN, LOW);
  delay(200);
  digitalWrite(LED_BUILTIN, HIGH);
  delay(200);
  digitalWrite(LED_BUILTIN, LOW);
  delay(200);
  digitalWrite(LED_BUILTIN, HIGH);
  delay(200);*/
}

void loop()
{
  /*static uint8_t payload[PTP_MAX_PAYLOAD_SIZE];

  static uint16_t cur_thrust = 0;
  static bool initialized = false;

  // process incoming packets
  while (true)
  {
    size_t payload_size;
    uint8_t verb;
    uint8_t addr;
    ptp_error_t err = ptp_read_packet(&verb, &addr, &payload_size, payload);
    if (err == PTP_ERROR_NO_DATA)
      break;
    if (err == PTP_ERROR_INVALID_PACKET)
      continue;
    if (verb == PTP_VERB_ACK)
      continue;
    else if (verb == PTP_VERB_SET)
    {
      switch (addr)
      {
      case ADDRIN_LED0:
        lc.setRow(0, 0, payload[0]);
        break;
      case ADDRIN_LED1:
        lc.setRow(0, 1, payload[0]);
        break;
      case ADDRIN_LED2:
        lc.setRow(0, 2, payload[0]);
        break;
      case ADDRIN_LED3:
        lc.setRow(0, 3, payload[0]);
        break;
      }
    }
    else if (verb == PTP_VERB_INIT)
    {
      ptp_write_packet(PTP_VERB_INIT_ACK, 0, 0, nullptr);
      initialized = false;
    }
  }
  // now process inputs
  uint16_t new_thrust = analogRead(PIN_THROTTLE);
  if (!initialized || new_thrust != cur_thrust)
  {
    cur_thrust = new_thrust;
    ptp_write_packet(PTP_VERB_SET, ADDROUT_THROTTLE, 2, &new_thrust);
  }

  initialized = true;*/
}
