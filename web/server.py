#!/usr/bin/env python3
"""
LFSPro Clean Master Portal & API Server
Serves:
  1. Full Web Dashboard (Live Servers, Skins, HWID Management, Registration)
  2. Master Authentication Endpoint (/api.php) for ProLightClean Launcher
  3. Persistent SQLite Database for drivers, accounts, and server state
"""

import os
import sys
import json
import sqlite3
import base64
import urllib.parse
from datetime import datetime
from http.server import HTTPServer, SimpleHTTPRequestHandler
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.primitives import hashes, padding

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DB_PATH = os.path.join(BASE_DIR, "portal.db")
LAUNCHER_EXE = os.path.join(BASE_DIR, "..", "bin", "ProLightClean.exe")

MASTER_PASSWORD = b"config.sys.input=UTF8"
ITERATIONS = 100000

# --- SQLite Database Initialization ---
def init_db():
    conn = sqlite3.connect(DB_PATH)
    c = conn.cursor()
    c.execute('''
        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT UNIQUE NOT NULL,
            password_hash TEXT NOT NULL,
            hwid_mac TEXT,
            hwid_mb TEXT,
            hwid_bios TEXT,
            hwid_hdd TEXT,
            rank TEXT DEFAULT 'Driver',
            credits INTEGER DEFAULT 1000,
            license TEXT DEFAULT 'S3',
            created_at TEXT
        )
    ''')
    c.execute('''
        CREATE TABLE IF NOT EXISTS servers (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            ip TEXT DEFAULT '127.0.0.1',
            port INTEGER NOT NULL,
            track TEXT NOT NULL,
            cars TEXT NOT NULL,
            players INTEGER DEFAULT 0,
            max_players INTEGER DEFAULT 32,
            mode TEXT DEFAULT 'Cruise'
        )
    ''')
    c.execute('''
        CREATE TABLE IF NOT EXISTS skins (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            car TEXT NOT NULL,
            name TEXT NOT NULL,
            author TEXT NOT NULL,
            downloads INTEGER DEFAULT 0,
            preview_color TEXT DEFAULT '#00C3FF'
        )
    ''')

    # Seed default mock servers if empty
    c.execute("SELECT COUNT(*) FROM servers")
    if c.fetchone()[0] == 0:
        c.executemany('''
            INSERT INTO servers (name, ip, port, track, cars, players, max_players, mode)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        ''', [
            ("LFSPro Official Cruise 1", "127.0.0.1", 50021, "Blackwood GP (BL1)", "XRT, RB4, FXO, RAC", 12, 32, "InSim Cruise"),
            ("LFSPro Official Cruise 2", "127.0.0.1", 50022, "South City Classic (SO4)", "XRT, FZR, XR GT", 8, 32, "InSim Cruise"),
            ("LFSPro ProDrift & Touge",  "127.0.0.1", 50023, "Aston Cadet (AS1)", "XF GTI, XR GT, XRT", 15, 32, "Drift"),
            ("LFSPro Dyno & Test Lab",   "127.0.0.1", 50024, "Autocross Custom (AU1)", "ALL S1/S2/S3", 4, 16, "Tuning Lab")
        ])

    # Seed sample skins if empty
    c.execute("SELECT COUNT(*) FROM skins")
    if c.fetchone()[0] == 0:
        c.executemany('''
            INSERT INTO skins (car, name, author, downloads, preview_color)
            VALUES (?, ?, ?, ?, ?)
        ''', [
            ("XRT", "Monster Energy Drift Edition", "ProRacer", 1420, "#28a745"),
            ("RB4", "Red Bull Rally Special", "ApexTuner", 980, "#dc3545"),
            ("FXO", "Cyberpunk Neon 2077", "NeonRider", 2150, "#00c3ff"),
            ("FZR", "Martini Racing GT", "ClassicGTR", 870, "#0078d7"),
            ("RAC", "HKS Time Attack Livery", "JDMKing", 1120, "#6f42c1"),
            ("XFR", "Castrol TOM'S Special", "SuperGT", 640, "#20c997")
        ])

    # Seed default demo account
    c.execute("SELECT COUNT(*) FROM users WHERE username = 'Pilot01'")
    if c.fetchone()[0] == 0:
        c.execute('''
            INSERT INTO users (username, password_hash, rank, credits, license, created_at)
            VALUES ('pilot01', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', 'Pro Pilot', 5000, 'S3', ?)
        ''', (datetime.utcnow().isoformat(),))

    conn.commit()
    conn.close()

# --- Cryptography Helpers ---
def decrypt_payload(b64_str: str) -> str:
    raw = base64.b64decode(b64_str)
    if len(raw) < 32:
        raise ValueError("Payload too short")
    salt = raw[:16]
    iv = raw[16:32]
    ct = raw[32:]

    kdf = PBKDF2HMAC(
        algorithm=hashes.SHA256(),
        length=32,
        salt=salt,
        iterations=ITERATIONS
    )
    key = kdf.derive(MASTER_PASSWORD)
    cipher = Cipher(algorithms.AES(key), modes.CBC(iv))
    dec = cipher.decryptor()
    padded = dec.update(ct) + dec.finalize()
    unpadder = padding.PKCS7(128).unpadder()
    return (unpadder.update(padded) + unpadder.finalize()).decode('utf-8').lstrip('\ufeff')

def sha256_text(text: str) -> str:
    import hashlib
    return hashlib.sha256(text.encode('utf-8')).hexdigest()

# --- HTTP Request Handler ---
class LFSProMasterPortalHandler(SimpleHTTPRequestHandler):
    def log_message(self, format, *args):
        # Clean terminal logging
        sys.stdout.write(f"[{datetime.now().strftime('%H:%M:%S')}] {args[0]} {args[1]}\n")
        sys.stdout.flush()

    def do_GET(self):
        url = urllib.parse.urlparse(self.path)
        path = url.path
        qs = urllib.parse.parse_qs(url.query)

        # 1. Launcher Master API: /api.php
        if path.endswith("/api.php") or path == "/api.php":
            self.handle_launcher_api(qs)
            return

        # 2. REST API: /api/stats
        if path == "/api/stats":
            self.send_json_response(self.get_stats())
            return

        # 3. REST API: /api/servers
        if path == "/api/servers":
            self.send_json_response(self.get_servers())
            return

        # 4. REST API: /api/skins
        if path == "/api/skins":
            self.send_json_response(self.get_skins())
            return

        # 5. Launcher Download: /download/launcher
        if path == "/download/launcher":
            if os.path.exists(LAUNCHER_EXE):
                with open(LAUNCHER_EXE, "rb") as f:
                    data = f.read()
                self.send_response(200)
                self.send_header("Content-Type", "application/octet-stream")
                self.send_header("Content-Disposition", 'attachment; filename="ProLightClean.exe"')
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)
            else:
                self.send_error(404, "Launcher binary not found.")
            return

        # 6. Web Page: Root index
        if path == "/" or path == "/index.html":
            html_path = os.path.join(BASE_DIR, "templates", "index.html")
            if os.path.exists(html_path):
                with open(html_path, "rb") as f:
                    content = f.read()
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(content)))
                self.end_headers()
                self.wfile.write(content)
                return

        # 7. Static file serving (CSS, JS, Images)
        if path.startswith("/static/"):
            rel_path = path[8:] # Strip /static/
            file_path = os.path.join(BASE_DIR, "static", rel_path)
            if os.path.exists(file_path) and os.path.isfile(file_path):
                ctype = "text/plain"
                if file_path.endswith(".css"): ctype = "text/css"
                elif file_path.endswith(".js"): ctype = "application/javascript"
                elif file_path.endswith(".png"): ctype = "image/png"
                elif file_path.endswith(".jpg"): ctype = "image/jpeg"
                elif file_path.endswith(".svg"): ctype = "image/svg+xml"

                with open(file_path, "rb") as f:
                    data = f.read()
                self.send_response(200)
                self.send_header("Content-Type", ctype)
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)
                return

        self.send_error(404, "Page Not Found")

    def do_POST(self):
        url = urllib.parse.urlparse(self.path)
        path = url.path
        length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(length)

        try:
            body = json.loads(post_data.decode('utf-8'))
        except:
            body = {}

        if path == "/api/register":
            self.send_json_response(self.handle_web_register(body))
            return

        if path == "/api/login":
            self.send_json_response(self.handle_web_login(body))
            return

        if path == "/api/reset_hwid":
            self.send_json_response(self.handle_hwid_reset(body))
            return

        self.send_error(404, "API endpoint not found")

    def send_json_response(self, data, code=200):
        res_bytes = json.dumps(data, ensure_ascii=False, indent=2).encode('utf-8')
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Content-Length", str(len(res_bytes)))
        self.end_headers()
        self.wfile.write(res_bytes)

    # --- Launcher Protocol Authentication (/api.php) ---
    def handle_launcher_api(self, qs):
        data_enc = qs.get("data", [""])[0]
        if not data_enc:
            self.send_json_response({
                "Server": "LFSPro Master Portal v5.0",
                "Status": "ONLINE",
                "LicenseUnlock": "S3-ENABLED"
            })
            return

        try:
            payload = decrypt_payload(data_enc)
            parts = payload.split('|')
            username = parts[0].strip().lower() if len(parts) > 0 else ""
            pass_hash = parts[1].strip() if len(parts) > 1 else ""
            hdd = parts[2].strip() if len(parts) > 2 else ""
            mb = parts[3].strip() if len(parts) > 3 else ""
            mac = parts[4].strip() if len(parts) > 4 else ""
            bios = parts[5].strip() if len(parts) > 5 else ""

            conn = sqlite3.connect(DB_PATH)
            c = conn.cursor()
            c.execute("SELECT password_hash, hwid_mac, hwid_mb, rank, credits, license FROM users WHERE username = ?", (username,))
            row = c.fetchone()

            if not row:
                # Auto-register new driver on first launcher login
                c.execute('''
                    INSERT INTO users (username, password_hash, hwid_mac, hwid_mb, hwid_bios, hwid_hdd, rank, credits, license, created_at)
                    VALUES (?, ?, ?, ?, ?, ?, 'Driver', 1000, 'S3', ?)
                ''', (username, pass_hash, mac, mb, bios, hdd, datetime.utcnow().isoformat()))
                conn.commit()
                conn.close()

                self.send_json_response({
                    "Status": "LOGINOK",
                    "Username": username,
                    "Rank": "Driver",
                    "Credits": 1000,
                    "License": "S3",
                    "ServerMessage": f"Welcome {username}! Your account has been registered with S3 License."
                })
                return

            db_pass, db_mac, db_mb, rank, credits, lic = row
            if db_pass != pass_hash:
                conn.close()
                self.send_json_response({
                    "Status": "WRONGPASS",
                    "Message": "Wrong password! Please try again!"
                })
                return

            # Update HWID if not set
            if not db_mac or not db_mb:
                c.execute("UPDATE users SET hwid_mac=?, hwid_mb=?, hwid_bios=?, hwid_hdd=? WHERE username=?",
                          (mac, mb, bios, hdd, username))
                conn.commit()

            conn.close()
            self.send_json_response({
                "Status": "LOGINOK",
                "Username": username,
                "Rank": rank,
                "Credits": credits,
                "License": lic,
                "ServerMessage": f"Welcome back {username}! Master authentication verified."
            })

        except Exception as e:
            self.send_json_response({"Status": "ERROR", "Message": f"Decryption error: {str(e)}"})

    # --- REST API Handlers ---
    def get_stats(self):
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()
        c.execute("SELECT COUNT(*) FROM users")
        total_users = c.fetchone()[0]
        c.execute("SELECT COUNT(*), SUM(players) FROM servers")
        s_count, p_count = c.fetchone()
        conn.close()

        return {
            "status": "ONLINE",
            "master_port": 29339,
            "api_port": 8080,
            "license": "S3 (All Tracks & Cars)",
            "total_drivers": total_users,
            "online_servers": s_count,
            "online_players": p_count or 0
        }

    def get_servers(self):
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()
        c.execute("SELECT id, name, ip, port, track, cars, players, max_players, mode FROM servers")
        rows = c.fetchall()
        conn.close()

        return [{
            "id": r[0], "name": r[1], "ip": r[2], "port": r[3],
            "track": r[4], "cars": r[5], "players": r[6], "max": r[7], "mode": r[8]
        } for r in rows]

    def get_skins(self):
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()
        c.execute("SELECT id, car, name, author, downloads, preview_color FROM skins")
        rows = c.fetchall()
        conn.close()

        return [{
            "id": r[0], "car": r[1], "name": r[2], "author": r[3],
            "downloads": r[4], "color": r[5]
        } for r in rows]

    def handle_web_register(self, data):
        u = data.get("username", "").strip().lower()
        p = data.get("password", "").strip()

        if len(u) < 3 or len(p) < 4:
            return {"success": False, "message": "Username min 3 chars, password min 4 chars."}

        p_hash = sha256_text(p)
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()
        try:
            c.execute('''
                INSERT INTO users (username, password_hash, rank, credits, license, created_at)
                VALUES (?, ?, 'Driver', 1000, 'S3', ?)
            ''', (u, p_hash, datetime.utcnow().isoformat()))
            conn.commit()
            conn.close()
            return {"success": True, "message": f"Driver account '{u}' created with S3 License!"}
        except sqlite3.IntegrityError:
            conn.close()
            return {"success": False, "message": "Username already exists."}

    def handle_web_login(self, data):
        u = data.get("username", "").strip().lower()
        p = data.get("password", "").strip()
        p_hash = sha256_text(p)

        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()
        c.execute("SELECT username, rank, credits, license, hwid_mac FROM users WHERE username = ? AND password_hash = ?", (u, p_hash))
        row = c.fetchone()
        conn.close()

        if row:
            return {
                "success": True,
                "user": {
                    "username": row[0],
                    "rank": row[1],
                    "credits": row[2],
                    "license": row[3],
                    "hwid_bound": bool(row[4])
                }
            }
        return {"success": False, "message": "Invalid username or password."}

    def handle_hwid_reset(self, data):
        u = data.get("username", "").strip().lower()
        conn = sqlite3.connect(DB_PATH)
        c = conn.cursor()
        c.execute("UPDATE users SET hwid_mac=NULL, hwid_mb=NULL, hwid_bios=NULL, hwid_hdd=NULL WHERE username=?", (u,))
        affected = c.rowcount
        conn.commit()
        conn.close()

        if affected > 0:
            return {"success": True, "message": f"HWID for '{u}' has been reset successfully."}
        return {"success": False, "message": "User not found."}

def run_server(host="0.0.0.0", port=8080):
    init_db()
    server_address = (host, port)
    httpd = HTTPServer(server_address, LFSProMasterPortalHandler)
    print("=" * 65)
    print(f"🏁 LFSPro Clean Master Web Portal & API")
    print(f"🌐 Web Dashboard URL:   http://localhost:{port}/")
    print(f"🔌 Launcher Master API: http://localhost:{port}/api.php")
    print(f"📦 Direct Launcher:     http://localhost:{port}/download/launcher")
    print("=" * 65)
    httpd.serve_forever()

if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
    run_server(port=port)
