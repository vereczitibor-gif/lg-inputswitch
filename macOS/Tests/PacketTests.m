@import Foundation;
@import IOKit;
#include "i2c.h"
#include <assert.h>
static unsigned calls;
static UInt32 length, address, source;
static UInt8 sent[6];
IOReturn IOAVServiceWriteI2C(IOAVServiceRef service, uint32_t chip, uint32_t dataAddress, void *buffer, uint32_t size) {
    (void)service;
    calls++; length = size; address = chip; source = dataAddress;
    assert(size == 6); memcpy(sent, buffer, size); return 0;
}
IOReturn IOAVServiceReadI2C(IOAVServiceRef service, uint32_t chip, uint32_t dataAddress, void *buffer, uint32_t size) {
    (void)service; (void)chip; (void)dataAddress; (void)buffer; (void)size;
    assert(!"No read should occur during switching"); return kIOReturnError;
}
int main(void) {
    @autoreleasepool {
        for (unsigned v = 0; v <= 65535; v++) {
            DDCPacket p = createDDCPacket(INPUT_ALT);
            prepareDDCWrite(&p, (UInt16)v);
            assert(p.inputAddr == 0x50);
            assert(p.data[0] == 0x84 && p.data[1] == 3 && p.data[2] == 0xf4);
            assert(p.data[3] == v >> 8 && p.data[4] == (v & 255));
            UInt8 xor = 0x6e ^ 0x50;
            for (unsigned i=0; i<6; i++) xor ^= p.data[i];
            assert(xor == 0);
        }
        unsigned values[] = {144,210,208,0x4d};
        for (unsigned i=0; i<4; i++) {
            DDCPacket p = createDDCPacket(INPUT_ALT); prepareDDCWrite(&p, values[i]);
            calls = 0; assert(performDDCWrite(NULL, &p) == 0);
            assert(calls == 1 && length == 6 && address == 0x37 && source == 0x50);
            assert(memcmp(sent,p.data,6) == 0);
        }
        assert(sent[5] == 0); // A zero checksum must still be transmitted.
        puts("PacketTests: 65536 values + exact transport framing + single-write checks passed; no hardware used.");
    }
}
