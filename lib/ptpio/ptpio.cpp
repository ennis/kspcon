#include <ptpio.h>
#include <HardwareSerial.h>
#include <Arduino.h>

#define PTP_ESCAPE 0x7D
#define PTP_FRAME_FLAG 0x7E
#define PTP_MAX_PACKET_SIZE (2 + PTP_MAX_PAYLOAD_SIZE)
#define PTP_MAX_ESCAPED_PACKET_SIZE (PTP_MAX_PACKET_SIZE * 2)
#define PTP_MAX_FRAME_SIZE (2 + PTP_MAX_ESCAPED_PACKET_SIZE)

static int pkt_size = -1;
static bool pkt_escaped = false;
static uint8_t pkt_buf[PTP_MAX_PACKET_SIZE];

void ptp_init()
{
  uint8_t reply_verb;
  do
  {
    // send INIT on the wire
    ptp_write_packet(PTP_VERB_INIT, 0, 0, nullptr);
    // Wait for INIT_ACK
    int retry = 0;
    do
    {
      delay(200);
      ptp_read_packet(&reply_verb, nullptr, nullptr, nullptr);
      retry++;
    } while (reply_verb != PTP_VERB_INIT_ACK && retry < 10);
  } while (reply_verb != PTP_VERB_INIT_ACK);
}

size_t ptp_read_packet_raw()
{
  while (Serial.available() > 0)
  {
    int b = Serial.read();
    if (pkt_size == -1)
    {
      if (b == PTP_FRAME_FLAG)
      {
        pkt_size = 0;
      }
      continue;
    }

    if (pkt_size >= PTP_MAX_PACKET_SIZE)
    {
      // packet too big, reset
      pkt_size = -1;
      pkt_escaped = false;
      continue;
    }

    if (b == PTP_ESCAPE)
    {
      pkt_escaped = true;
    }
    else if (b == PTP_FRAME_FLAG)
    {
      // TODO error if escaped
      if (pkt_size != 0)
      {
        // full packet received
        size_t size = pkt_size;
        pkt_size = 0;
        return size;
      }
    }
    else if (pkt_escaped)
    {
      pkt_buf[pkt_size++] = 0x20 ^ b;
      pkt_escaped = false;
    }
    else
    {
      pkt_buf[pkt_size++] = b;
    }
  }
  return 0;
}

// Writes a packet to the serial port.
void ptp_write_packet_raw(const uint8_t *packet, size_t byte_size)
{
  static uint8_t write_buf[PTP_MAX_FRAME_SIZE];
  size_t wptr = 0;
  write_buf[wptr++] = PTP_FRAME_FLAG;
  // escape contents
  for (size_t i = 0; i < byte_size; ++i)
  {
    if (packet[i] == PTP_ESCAPE || packet[i] == PTP_FRAME_FLAG)
    {
      write_buf[wptr++] = PTP_ESCAPE;
      write_buf[wptr++] = packet[i] ^ 0x20;
    }
    else
    {
      write_buf[wptr++] = packet[i];
    }
  }
  write_buf[wptr++] = PTP_FRAME_FLAG;
  Serial.write(write_buf, wptr);
}

ptp_error_t ptp_read_packet(uint8_t *out_verb, uint8_t *out_address, size_t *out_payload_size, void *out_payload)
{
  size_t recv_size = ptp_read_packet_raw();
  if (recv_size == 0)
    return PTP_ERROR_NO_DATA;
  if (recv_size < 3)
    return PTP_ERROR_INVALID_PACKET; // packet too small or no data received

  *out_verb = pkt_buf[0];
  if (out_address)
    *out_address = pkt_buf[1];
  if (out_payload)
    memcpy(out_payload, pkt_buf + 2, recv_size - 2);
  return PTP_SUCCESS;
}

ptp_error_t ptp_write_packet(uint8_t verb, uint8_t address, size_t payload_size, void *payload)
{
  static uint8_t write_buf[PTP_MAX_PAYLOAD_SIZE + 2];
  size_t wptr = 0;
  write_buf[wptr++] = verb;
  if ((verb == PTP_VERB_SET) || (verb == PTP_VERB_ACK))
  {
    write_buf[wptr++] = address;
    memcpy(write_buf + wptr, payload, payload_size);
    wptr += payload_size;
  }
  ptp_write_packet_raw(write_buf, wptr);
  return PTP_SUCCESS;
}
