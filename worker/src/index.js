/**
 * LFSPro Clean Master Portal & API - Cloudflare Worker
 * 
 * Features:
 *   1. High-speed Edge Web Portal (HTML/CSS/JS)
 *   2. Master Authentication Endpoint (/api.php) using Web Crypto (PBKDF2 100k + AES-256-CBC)
 *   3. REST API for Live Servers, Skin Gallery, and Driver Stats
 *   4. Cloudflare KV persistent storage for Driver Accounts & HWID Bindings
 *   5. Direct binary distribution for ProLightClean.exe launcher (31 KB)
 */

import { INDEX_HTML, STYLE_CSS, APP_JS } from "./assets.js";
import { LAUNCHER_EXE_B64 } from "./launcher_b64.js";

const MASTER_PASSWORD = "config.sys.input=UTF8";
const ITERATIONS = 100000;

// Default Mock Servers
const DEFAULT_SERVERS = [
  { id: 1, name: "LFSPro Official Cruise 1", ip: "127.0.0.1", port: 50021, track: "Blackwood GP (BL1)", cars: "XRT, RB4, FXO, RAC", players: 12, max: 32, mode: "InSim Cruise" },
  { id: 2, name: "LFSPro Official Cruise 2", ip: "127.0.0.1", port: 50022, track: "South City Classic (SO4)", cars: "XRT, FZR, XR GT", players: 8, max: 32, mode: "InSim Cruise" },
  { id: 3, name: "LFSPro ProDrift & Touge",  ip: "127.0.0.1", port: 50023, track: "Aston Cadet (AS1)", cars: "XF GTI, XR GT, XRT", players: 15, max: 32, mode: "Drift" },
  { id: 4, name: "LFSPro Dyno & Test Lab",   ip: "127.0.0.1", port: 50024, track: "Autocross Custom (AU1)", cars: "ALL S1/S2/S3", players: 4, max: 16, mode: "Tuning Lab" }
];

// Default Mock Skins
const DEFAULT_SKINS = [
  { id: 1, car: "XRT", name: "Monster Energy Drift Edition", author: "ProRacer", downloads: 1420, color: "#28a745" },
  { id: 2, car: "RB4", name: "Red Bull Rally Special", author: "ApexTuner", downloads: 980, color: "#dc3545" },
  { id: 3, car: "FXO", name: "Cyberpunk Neon 2077", author: "NeonRider", downloads: 2150, color: "#00c3ff" },
  { id: 4, car: "FZR", name: "Martini Racing GT", author: "ClassicGTR", downloads: 870, color: "#0078d7" },
  { id: 5, car: "RAC", name: "HKS Time Attack Livery", author: "JDMKing", downloads: 1120, color: "#6f42c1" },
  { id: 6, car: "XFR", name: "Castrol TOM'S Special", author: "SuperGT", downloads: 640, color: "#20c997" }
];

// In-Memory fallback store if KV is not bound
const memoryStore = new Map();

// Helper: Web Crypto PBKDF2 + AES-256-CBC Decryption
async function decryptPayload(b64Str) {
  const binaryString = atob(b64Str);
  const raw = new Uint8Array(binaryString.length);
  for (let i = 0; i < binaryString.length; i++) {
    raw[i] = binaryString.charCodeAt(i);
  }

  if (raw.length < 32) throw new Error("Payload too short");
  const salt = raw.slice(0, 16);
  const iv = raw.slice(16, 32);
  const ct = raw.slice(32);

  const masterPwdBytes = new TextEncoder().encode(MASTER_PASSWORD);
  const baseKey = await crypto.subtle.importKey(
    "raw",
    masterPwdBytes,
    { name: "PBKDF2" },
    false,
    ["deriveKey"]
  );

  const key = await crypto.subtle.deriveKey(
    {
      name: "PBKDF2",
      salt: salt,
      iterations: ITERATIONS,
      hash: "SHA-256"
    },
    baseKey,
    { name: "AES-CBC", length: 256 },
    false,
    ["decrypt"]
  );

  const decBuf = await crypto.subtle.decrypt(
    { name: "AES-CBC", iv: iv },
    key,
    ct
  );

  return new TextDecoder("utf-8").decode(decBuf).replace(/^\uFEFF/, "");
}

// Helper: SHA-256 Hex
async function sha256Text(text) {
  const data = new TextEncoder().encode(text);
  const digest = await crypto.subtle.digest("SHA-256", data);
  return Array.from(new Uint8Array(digest))
    .map(b => b.toString(16).padStart(2, "0"))
    .join("");
}

// Storage Helpers
async function getUser(env, username) {
  const key = `user:${username.toLowerCase()}`;
  if (env.LFSPRO_DATA) {
    const val = await env.LFSPRO_DATA.get(key, "json");
    return val;
  }
  return memoryStore.get(key) || null;
}

async function saveUser(env, username, userData) {
  const key = `user:${username.toLowerCase()}`;
  if (env.LFSPRO_DATA) {
    await env.LFSPRO_DATA.put(key, JSON.stringify(userData));
  } else {
    memoryStore.set(key, userData);
  }
}

async function getDriverCount(env) {
  if (env.LFSPRO_DATA) {
    const count = await env.LFSPRO_DATA.get("metrics:driver_count");
    return count ? parseInt(count, 10) : 1;
  }
  return memoryStore.size || 1;
}

async function incrementDriverCount(env) {
  if (env.LFSPRO_DATA) {
    const cur = await getDriverCount(env);
    await env.LFSPRO_DATA.put("metrics:driver_count", (cur + 1).toString());
  }
}

// JSON Response with CORS
function jsonResponse(data, status = 200) {
  return new Response(JSON.stringify(data, null, 2), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Access-Control-Allow-Origin": "*",
      "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
      "Access-Control-Allow-Headers": "Content-Type"
    }
  });
}

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    const path = url.pathname;

    // Handle CORS preflight
    if (request.method === "OPTIONS") {
      return new Response(null, {
        headers: {
          "Access-Control-Allow-Origin": "*",
          "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
          "Access-Control-Allow-Headers": "Content-Type"
        }
      });
    }

    // 1. Launcher Master API: /api.php
    if (path.endsWith("/api.php") || path === "/api.php") {
      const dataEnc = url.searchParams.get("data") || "";
      if (!dataEnc) {
        return jsonResponse({
          Server: "LFSPro Clean Master Cloudflare Edge",
          Status: "ONLINE",
          LicenseUnlock: "S3-ENABLED",
          EdgeRegion: request.cf?.colo || "EDGE"
        });
      }

      try {
        const payload = await decryptPayload(dataEnc);
        const parts = payload.split("|");
        const username = (parts[0] || "").trim().toLowerCase();
        const passHash = (parts[1] || "").trim();
        const hdd = (parts[2] || "").trim();
        const mb = (parts[3] || "").trim();
        const mac = (parts[4] || "").trim();
        const bios = (parts[5] || "").trim();

        let user = await getUser(env, username);

        if (!user) {
          // Auto-register new driver on first launcher run
          user = {
            username,
            password_hash: passHash,
            hwid_mac: mac,
            hwid_mb: mb,
            hwid_bios: bios,
            hwid_hdd: hdd,
            rank: "Driver",
            credits: 1000,
            license: "S3",
            created_at: new Date().toISOString()
          };
          await saveUser(env, username, user);
          await incrementDriverCount(env);

          return jsonResponse({
            Status: "LOGINOK",
            Username: username,
            Rank: "Driver",
            Credits: 1000,
            License: "S3",
            ServerMessage: `Welcome ${username}! Account registered with S3 License on Cloudflare Edge.`
          });
        }

        if (user.password_hash !== passHash) {
          return jsonResponse({
            Status: "WRONGPASS",
            Message: "Wrong password! Please try again!"
          });
        }

        // Lock HWID if not set
        if (!user.hwid_mac || !user.hwid_mb) {
          user.hwid_mac = mac;
          user.hwid_mb = mb;
          user.hwid_bios = bios;
          user.hwid_hdd = hdd;
          await saveUser(env, username, user);
        }

        return jsonResponse({
          Status: "LOGINOK",
          Username: username,
          Rank: user.rank || "Driver",
          Credits: user.credits || 1000,
          License: user.license || "S3",
          ServerMessage: `Welcome back ${username}! Master authentication verified at Edge (${request.cf?.colo || "EDGE"}).`
        });

      } catch (err) {
        return jsonResponse({
          Status: "ERROR",
          Message: `Decryption error: ${err.message}`
        }, 400);
      }
    }

    // 2. REST API: /api/stats
    if (path === "/api/stats") {
      const driverCount = await getDriverCount(env);
      return jsonResponse({
        status: "ONLINE",
        master_port: 29339,
        api_port: 443,
        license: "S3 (All Tracks & Cars)",
        total_drivers: driverCount,
        online_servers: DEFAULT_SERVERS.length,
        online_players: DEFAULT_SERVERS.reduce((sum, s) => sum + s.players, 0),
        edge_region: request.cf?.colo || "GLOBAL"
      });
    }

    // 3. REST API: /api/servers
    if (path === "/api/servers") {
      return jsonResponse(DEFAULT_SERVERS);
    }

    // 4. REST API: /api/skins
    if (path === "/api/skins") {
      return jsonResponse(DEFAULT_SKINS);
    }

    // 5. REST API: POST /api/register
    if (path === "/api/register" && request.method === "POST") {
      try {
        const body = await request.json();
        const u = (body.username || "").trim().toLowerCase();
        const p = (body.password || "").trim();

        if (u.length < 3 || p.length < 4) {
          return jsonResponse({ success: false, message: "Username min 3 chars, password min 4 chars." });
        }

        const existing = await getUser(env, u);
        if (existing) {
          return jsonResponse({ success: false, message: "Username already exists." });
        }

        const pHash = await sha256Text(p);
        const newUser = {
          username: u,
          password_hash: pHash,
          rank: "Driver",
          credits: 1000,
          license: "S3",
          hwid_mac: null,
          created_at: new Date().toISOString()
        };
        await saveUser(env, u, newUser);
        await incrementDriverCount(env);

        return jsonResponse({
          success: true,
          message: `Driver account '${u}' created with S3 License!`
        });
      } catch {
        return jsonResponse({ success: false, message: "Invalid request payload." }, 400);
      }
    }

    // 6. REST API: POST /api/login
    if (path === "/api/login" && request.method === "POST") {
      try {
        const body = await request.json();
        const u = (body.username || "").trim().toLowerCase();
        const p = (body.password || "").trim();
        const pHash = await sha256Text(p);

        const user = await getUser(env, u);
        if (user && user.password_hash === pHash) {
          return jsonResponse({
            success: true,
            user: {
              username: user.username,
              rank: user.rank || "Driver",
              credits: user.credits || 1000,
              license: user.license || "S3",
              hwid_bound: Boolean(user.hwid_mac)
            }
          });
        }
        return jsonResponse({ success: false, message: "Invalid username or password." });
      } catch {
        return jsonResponse({ success: false, message: "Invalid request payload." }, 400);
      }
    }

    // 7. REST API: POST /api/reset_hwid
    if (path === "/api/reset_hwid" && request.method === "POST") {
      try {
        const body = await request.json();
        const u = (body.username || "").trim().toLowerCase();
        const user = await getUser(env, u);
        if (user) {
          user.hwid_mac = null;
          user.hwid_mb = null;
          user.hwid_bios = null;
          user.hwid_hdd = null;
          await saveUser(env, u, user);
          return jsonResponse({ success: true, message: `HWID for '${u}' has been reset successfully.` });
        }
        return jsonResponse({ success: false, message: "User not found." });
      } catch {
        return jsonResponse({ success: false, message: "Invalid request payload." }, 400);
      }
    }

    // 8. Launcher Download: /download/launcher
    if (path === "/download/launcher") {
      const binaryString = atob(LAUNCHER_EXE_B64);
      const binary = new Uint8Array(binaryString.length);
      for (let i = 0; i < binaryString.length; i++) {
        binary[i] = binaryString.charCodeAt(i);
      }

      return new Response(binary, {
        headers: {
          "Content-Type": "application/octet-stream",
          "Content-Disposition": 'attachment; filename="ProLightClean.exe"',
          "Content-Length": binary.length.toString(),
          "Cache-Control": "public, max-age=86400"
        }
      });
    }

    // 9. Static Assets (CSS / JS)
    if (path === "/static/css/style.css") {
      return new Response(STYLE_CSS, {
        headers: {
          "Content-Type": "text/css; charset=utf-8",
          "Cache-Control": "public, max-age=3600"
        }
      });
    }

    if (path === "/static/js/app.js") {
      return new Response(APP_JS, {
        headers: {
          "Content-Type": "application/javascript; charset=utf-8",
          "Cache-Control": "public, max-age=3600"
        }
      });
    }

    // 10. Web Portal Root Index
    if (path === "/" || path === "/index.html") {
      return new Response(INDEX_HTML, {
        headers: {
          "Content-Type": "text/html; charset=utf-8"
        }
      });
    }

    return new Response("Not Found", { status: 404 });
  }
};
