#pragma once
#include <stdlib.h>
#include <stdint.h>

#define PTP_MAX_PAYLOAD_SIZE 16

enum ptp_error_t
{
    PTP_SUCCESS = 0,
    PTP_ERROR_NO_DATA = 1,
    PTP_ERROR_INVALID_PACKET = 2,
};

enum ptp_verb_t
{
    PTP_VERB_SET,
    PTP_VERB_ACK,
    PTP_VERB_INIT,
    PTP_VERB_INIT_ACK
};

// Initializes the PTP protocol
void ptp_init();

// Reads a packet from the serial port.
// outPacket must point to a buffer large enough to store PTP_MAX_PAYLOAD_SIZE bytes
ptp_error_t ptp_read_packet(uint8_t *out_verb, uint8_t *out_address, size_t *out_payload_size, void *out_payload);

///
ptp_error_t ptp_write_packet(uint8_t verb, uint8_t address, size_t payload_size, void *payload);
