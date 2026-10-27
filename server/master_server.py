#!/usr/bin/env python3
"""
Clean Standalone Master Server for ProLight Clean & LFS 0.6V
Runs on localhost:8080 or custom port without external dependencies.
"""

import json
import base64
import urllib.parse
from http.server import HTTPServer, BaseHTTPRequestHandler
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.primitives import hashes, padding

MASTER_PASSWORD = b'config.sys.input=UTF8'
ITERATIONS = 100000

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

USERS = {}

class MasterHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        url = urllib.parse.urlparse(self.path)
        qs = urllib.parse.parse_qs(url.query)

        if url.path.endswith("/api.php") or url.path == "/":
            data_enc = qs.get("data", [""])[0]

            if not data_enc:
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(json.dumps({
                    "Server": "ProLight Clean Master API",
                    "Status": "ONLINE"
                }).encode('utf-8'))
                return

            try:
                decrypted = decrypt_payload(data_enc)
                parts = decrypted.split('|')
                username = parts[0] if len(parts) > 0 else "Driver"
                pass_hash = parts[1] if len(parts) > 1 else ""

                print(f"[MasterServer] Login attempt: {username}")

                if username not in USERS:
                    USERS[username] = pass_hash
                    print(f"[MasterServer] Registered new user '{username}'")

                if USERS[username] != pass_hash:
                    resp_obj = {"Status": "WRONGPASS", "Message": "Password does not match."}
                else:
                    resp_obj = {
                        "Status": "LOGINOK",
                        "Username": username,
                        "License": "S3",
                        "Message": f"Welcome {username}! Authenticated on Localhost."
                    }
            except Exception as e:
                resp_obj = {"Status": "ERROR", "Message": str(e)}

            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.end_headers()
            self.wfile.write(json.dumps(resp_obj).encode('utf-8'))

def run(host="0.0.0.0", port=8080):
    server = HTTPServer((host, port), MasterHandler)
    print(f"[*] ProLight Clean Master Server running on http://{host}:{port}/api.php")
    server.serve_forever()

if __name__ == "__main__":
    run()
