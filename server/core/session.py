import socket
import threading
import json
import uuid
import urllib.request
from datetime import datetime

# ISO 3166-1 alpha-2 → Unicode regional indicator flag emoji
def _country_to_flag(code: str) -> str:
    if not code or len(code) != 2:
        return '🌐'
    code = code.upper()
    return chr(0x1F1E6 + ord(code[0]) - ord('A')) + \
           chr(0x1F1E6 + ord(code[1]) - ord('A'))

def _lookup_country(ip: str) -> tuple[str, str]:
    """Returns (country_code, flag_emoji). Falls back to ('??', '🌐') on error."""
    try:
        # Skip private/loopback addresses
        if ip.startswith(('127.', '10.', '192.168.', '::1')):
            return 'LO', '🖧'
        parts = ip.split('.')
        if len(parts) == 4 and int(parts[0]) in (172,):
            second = int(parts[1])
            if 16 <= second <= 31:
                return 'LO', '🖧'

        url  = f'http://ip-api.com/json/{ip}?fields=countryCode'
        req  = urllib.request.urlopen(url, timeout=4)
        data = json.loads(req.read().decode())
        code = data.get('countryCode', '??')
        return code, _country_to_flag(code)
    except:
        return '??', '🌐'


class ClientSession:
    def __init__(self, socket: socket.socket, address: tuple):
        self.id         = str(uuid.uuid4())
        self.socket     = socket
        self.address    = address
        self.guid       = None
        self.hostname   = None
        self.username   = None
        self.os_version = None
        self.ip_address = address[0]
        self.is_online  = True
        self.created_at = datetime.now()
        self.last_seen  = datetime.now()
        self.lock       = threading.Lock()

        # Geo
        self.country_code = '??'
        self.flag         = '🌐'

        # Resolve country in background (don't block accept loop)
        threading.Thread(target=self._resolve_geo, daemon=True).start()

    def _resolve_geo(self):
        self.country_code, self.flag = _lookup_country(self.ip_address)

    def update_last_seen(self):
        self.last_seen = datetime.now()

    def set_system_info(self, info: dict):
        self.guid       = info.get('guid')
        self.hostname   = info.get('hostname')
        self.username   = info.get('username')
        self.os_version = info.get('os_version')

    def send(self, data: bytes):
        try:
            self.socket.sendall(data)
            return True
        except:
            return False

    def recv(self, bufsize: int = 8192) -> bytes:
        try:
            return self.socket.recv(bufsize)
        except:
            return b''

    def close(self):
        try:
            self.socket.close()
        except:
            pass
        self.is_online = False

    def to_dict(self) -> dict:
        return {
            'id':           self.id,
            'guid':         self.guid,
            'hostname':     self.hostname,
            'username':     self.username,
            'os_version':   self.os_version,
            'ip_address':   self.ip_address,
            'is_online':    self.is_online,
            'country_code': self.country_code,
            'flag':         self.flag,
            'last_seen':    self.last_seen.isoformat() if self.last_seen else None,
            'created_at':   self.created_at.isoformat() if self.created_at else None
        }

    def __repr__(self):
        return f'<ClientSession {self.guid or self.address} - {"online" if self.is_online else "offline"}>'