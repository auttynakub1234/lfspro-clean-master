# 🏁 LFSPro Clean Master & Launcher Reborn

> Standalone, lightweight Live for Speed 0.6V master server, community web portal, and 31 KB client launcher. Completely rewritten from scratch from binary reverse-engineering.

[![Cloudflare Workers](https://img.shields.io/badge/Cloudflare_Workers-Deployed-F38020?logo=cloudflare&logoColor=white)](https://lfspro-master-portal.fixflxw24.workers.dev)
[![Live Demo](https://img.shields.io/badge/Live_Portal-ONLINE-00C3FF)](https://lfspro-master-portal.fixflxw24.workers.dev)
[![License Unlock](https://img.shields.io/badge/License-S3_Full_Unlock-00E676)](#)
[![Binary Size](https://img.shields.io/badge/Launcher_Size-31_KB-FF6B00)](#)

---

## 🌐 Live Cloudflare Worker Deployment

The Master Portal & API Server is live at the Cloudflare Edge:
* **Web Portal URL:** [https://lfspro-master-portal.fixflxw24.workers.dev](https://lfspro-master-portal.fixflxw24.workers.dev)
* **Launcher Master API:** `https://lfspro-master-portal.fixflxw24.workers.dev/api.php`
* **Direct Launcher Download:** [https://lfspro-master-portal.fixflxw24.workers.dev/download/launcher](https://lfspro-master-portal.fixflxw24.workers.dev/download/launcher)

---

## ✨ Features

1. **Lightweight Launcher (`ProLightClean.exe`)**:
   * Size reduced from **143 MB** down to just **31 KB** (99.98% reduction).
   * Direct hardware fingerprinting (Motherboard Serial, BIOS UUID, HDD Serial, MAC).
   * RFC 2898 PBKDF2 (100,000 iterations) + AES-256-CBC cryptography.
   * Built-in Master Server Bridge listening on `127.0.0.1:29339` for instant S3 license unlock.
   * Auto-patches `LFS.exe` 0.6V memory buffers at runtime (`0x1C830C`, `0x1C890C`, `0x1C6CD4`, `0x5B0AA4`).

2. **Web Portal & Community Dashboard**:
   * Modern motorsport dark glassmorphism UI.
   * Real-time server browser (Cruise, Drift, Dyno Lab) with car & track filters.
   * Driver registration & login with automatic S3 license & 1,000 credits.
   * 1-Click HWID reset capability.
   * Livery and skin download gallery.

3. **Cloudflare Worker Edge Runtime**:
   * Native **Web Crypto API** (`crypto.subtle`) for microsecond PBKDF2 & AES-256-CBC decryption.
   * Globally distributed state using **Cloudflare KV** (`LFSPRO_DATA`).
   * Embedded binary distribution serving `ProLightClean.exe` straight from edge cache.

---

## 📁 Repository Structure

```
prolight_clean/
├── bin/
│   ├── ProLightClean.exe          # Pre-compiled lightweight launcher (31 KB)
│   └── ProLightClean.exe.config   # Runtime binding configuration
├── src/                           # Pure C# launcher source code
│   ├── Program.cs                 # CLI and GUI entry points
│   ├── FormLauncher.cs            # Windows Forms modern UI
│   ├── SecureCrypto.cs            # Pure RFC 2898 PBKDF2-SHA256 & AES-CBC
│   ├── HardwareInfo.cs            # Hardware fingerprinting
│   ├── LFSMemory.cs               # Win32 process memory patching (0.6V)
│   ├── MasterServerBridge.cs      # TCP 29339 S3 Scrutineer & JRA interceptor
│   ├── ProfileManager.cs          # .ply, usr.bin, and cfg.txt manager
│   └── LoginClient.cs             # HTTPS client with TLS 1.2 support
├── web/                           # Standalone Python Localhost Server
│   ├── server.py                  # HTTP & SQLite backend
│   ├── templates/index.html       # Web dashboard HTML
│   ├── static/css/style.css       # Esports dark glassmorphism stylesheet
│   └── static/js/app.js           # Client-side reactivity & API integration
├── worker/                        # Cloudflare Worker deployment
│   ├── wrangler.toml              # Cloudflare Worker configuration & KV binding
│   └── src/                       # Edge worker source code
├── build.sh                       # Mono/C# compiler script
└── README.md
```

---

## 🚀 Getting Started

### Option A: Using the Cloudflare Worker (Zero Setup)
1. Download the launcher directly from:  
   `https://lfspro-master-portal.fixflxw24.workers.dev/download/launcher`
2. Run the launcher and enter your Driver credentials.
3. Select your Live for Speed 0.6V folder and click **Launch LFS Pro**.

### Option B: Running Localhost Server
```bash
# Start the local web portal and master API on port 8080
python3 web/server.py 8080

# In another terminal / Windows machine:
mono bin/ProLightClean.exe --login <username> <password> http://127.0.0.1:8080/api.php
```

### Option C: Compiling the Launcher from Source
On Linux (Mono):
```bash
./build.sh
```
On Windows:
```cmd
csc /target:winexe /out:bin\ProLightClean.exe src\*.cs
```

### Option D: Deploying to Cloudflare Workers
```bash
cd worker
npx wrangler deploy
```

---

## 🔒 Technical Specifications

* **LFS Target:** Live for Speed `0.6V` (32-bit Native PE Engine)
* **Master Bridge Port:** `TCP 127.0.0.1:29339`
* **License Payload:** 100-byte Scrutineer packet with `License=3 (S3)` and `Status=0`
* **Encryption:** AES-256-CBC with PKCS7 Padding
* **Key Derivation:** PBKDF2-HMAC-SHA256 (100,000 iterations, password: `config.sys.input=UTF8`)
