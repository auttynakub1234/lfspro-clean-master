// ====================================================================
// LFSPro Clean Master Portal - Frontend Logic
// ====================================================================

let currentUser = null;
let allServers = [];
let allSkins = [];

document.addEventListener("DOMContentLoaded", () => {
  initTabs();
  initAuthSession();
  fetchStats();
  fetchServers();
  fetchSkins();

  // Poll server status every 15s
  setInterval(() => {
    fetchStats();
    fetchServers();
  }, 15000);
});

// --- Tab Switching ---
function initTabs() {
  const navItems = document.querySelectorAll(".nav-item");
  navItems.forEach(item => {
    item.addEventListener("click", (e) => {
      e.preventDefault();
      const tab = item.getAttribute("data-tab");
      switchTab(tab);
    });
  });

  // Server Filter Buttons
  document.querySelectorAll(".filter-btn[data-filter]").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".filter-btn[data-filter]").forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
      renderServers(btn.getAttribute("data-filter"));
    });
  });

  // Skin Car Filter Buttons
  document.querySelectorAll(".filter-btn[data-car]").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".filter-btn[data-car]").forEach(b => b.classList.remove("active"));
      btn.classList.add("active");
      renderSkins(btn.getAttribute("data-car"));
    });
  });

  // Driver Login quick button in nav
  const btnAuth = document.getElementById("btn-open-auth");
  if (btnAuth) {
    btnAuth.addEventListener("click", () => switchTab("account"));
  }
}

function switchTab(tabId) {
  document.querySelectorAll(".tab-pane").forEach(pane => pane.classList.remove("active"));
  document.querySelectorAll(".nav-item").forEach(item => {
    if (item.getAttribute("data-tab") === tabId) {
      item.classList.add("active");
    } else {
      item.classList.remove("active");
    }
  });

  const targetPane = document.getElementById(`tab-${tabId}`);
  if (targetPane) {
    targetPane.classList.add("active");
    window.scrollTo({ top: targetPane.offsetTop - 80, behavior: "smooth" });
  }
}

// --- Fetch Stats ---
async function fetchStats() {
  try {
    const res = await fetch("/api/stats");
    if (!res.ok) return;
    const data = await res.json();

    document.getElementById("metric-status").textContent = data.status || "ONLINE";
    document.getElementById("metric-drivers").textContent = data.total_drivers ?? "--";
    document.getElementById("metric-servers").textContent = data.online_servers ?? "--";
    document.getElementById("metric-players").textContent = data.online_players ?? "--";
  } catch (err) {
    console.warn("Error fetching stats:", err);
  }
}

// --- Fetch & Render Servers ---
async function fetchServers() {
  try {
    const res = await fetch("/api/servers");
    if (!res.ok) return;
    allServers = await res.json();
    renderFeaturedServers();
    renderServers("all");
  } catch (err) {
    console.warn("Error fetching servers:", err);
  }
}

function renderFeaturedServers() {
  const container = document.getElementById("featured-servers-list");
  if (!container) return;

  if (allServers.length === 0) {
    container.innerHTML = `<p class="text-muted">No servers currently broadcasting.</p>`;
    return;
  }

  container.innerHTML = allServers.slice(0, 3).map(s => `
    <div style="display:flex; justify-content:space-between; align-items:center; padding:0.75rem 0; border-bottom:1px solid var(--border-color);">
      <div>
        <div style="font-weight:700; font-family:var(--font-heading); font-size:1.05rem;">${escapeHtml(s.name)}</div>
        <div style="font-size:0.8rem; color:var(--text-muted);">Track: ${escapeHtml(s.track)} • Cars: ${escapeHtml(s.cars)}</div>
      </div>
      <div style="text-align:right;">
        <span class="mode-badge ${getModeClass(s.mode)}">${escapeHtml(s.mode)}</span>
        <div style="font-size:0.8rem; color:var(--accent-green); margin-top:0.25rem;">${s.players}/${s.max} Players</div>
      </div>
    </div>
  `).join("");
}

function renderServers(filter) {
  const tbody = document.getElementById("servers-table-body");
  if (!tbody) return;

  const filtered = filter === "all" 
    ? allServers 
    : allServers.filter(s => s.mode.toLowerCase().includes(filter.toLowerCase()));

  if (filtered.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7" class="text-center" style="padding:2rem;">No active servers matching filter "${filter}".</td></tr>`;
    return;
  }

  tbody.innerHTML = filtered.map(s => {
    const pct = Math.min(100, Math.round((s.players / s.max) * 100));
    return `
      <tr>
        <td>
          <span style="display:inline-block; width:10px; height:10px; background:var(--accent-green); border-radius:50%; box-shadow:0 0 6px var(--accent-green);"></span>
        </td>
        <td>
          <strong style="color:#fff;">${escapeHtml(s.name)}</strong>
          <div style="font-size:0.78rem; color:var(--text-dim);">${s.ip}:${s.port}</div>
        </td>
        <td><span class="mode-badge ${getModeClass(s.mode)}">${escapeHtml(s.mode)}</span></td>
        <td><span style="color:var(--primary-cyan); font-weight:600;">${escapeHtml(s.track)}</span></td>
        <td><span style="font-size:0.85rem; color:var(--text-muted);">${escapeHtml(s.cars)}</span></td>
        <td>
          <div class="player-bar-container">
            <div class="player-bar-bg">
              <div class="player-bar-fill" style="width: ${pct}%;"></div>
            </div>
            <span style="font-size:0.82rem; font-weight:600;">${s.players}/${s.max}</span>
          </div>
        </td>
        <td>
          <button class="btn btn-outline btn-sm" onclick="copyJoinCommand('${s.ip}', ${s.port})">
            📋 Copy IP
          </button>
        </td>
      </tr>
    `;
  }).join("");
}

function getModeClass(mode) {
  const m = mode.toLowerCase();
  if (m.includes("drift")) return "mode-drift";
  if (m.includes("lab") || m.includes("dyno")) return "mode-lab";
  return "mode-cruise";
}

// --- Fetch & Render Skins ---
async function fetchSkins() {
  try {
    const res = await fetch("/api/skins");
    if (!res.ok) return;
    allSkins = await res.json();
    renderSkins("all");
  } catch (err) {
    console.warn("Error fetching skins:", err);
  }
}

function renderSkins(carFilter) {
  const container = document.getElementById("skins-grid-container");
  if (!container) return;

  const filtered = carFilter === "all"
    ? allSkins
    : allSkins.filter(s => s.car.toUpperCase() === carFilter.toUpperCase());

  if (filtered.length === 0) {
    container.innerHTML = `<div style="grid-column: 1 / -1; text-align:center; padding:3rem; color:var(--text-muted);">No skins available for car code ${carFilter}.</div>`;
    return;
  }

  container.innerHTML = filtered.map(s => `
    <div class="skin-card">
      <div class="skin-preview-banner" style="background: linear-gradient(135deg, ${s.color || '#00c3ff'} 0%, #101522 100%);">
        <span>${escapeHtml(s.car)}</span>
        <div class="skin-car-pill">${escapeHtml(s.car)}</div>
      </div>
      <div class="skin-card-body">
        <div class="skin-name" title="${escapeHtml(s.name)}">${escapeHtml(s.name)}</div>
        <div class="skin-meta">
          <span>By: <strong style="color:var(--text-main);">${escapeHtml(s.author)}</strong></span>
          <span>⬇️ ${s.downloads} dl</span>
        </div>
        <div class="skin-actions">
          <button class="btn btn-cyan btn-sm btn-block" onclick="downloadSkinMock('${escapeHtml(s.name)}')">
            Download Skin
          </button>
          <button class="btn btn-outline btn-sm" title="Copy in-game name" onclick="copyText('${escapeHtml(s.car)}_${escapeHtml(s.name.replace(/\\s+/g, ''))}.jpg')">
            📋
          </button>
        </div>
      </div>
    </div>
  `).join("");
}

// --- Auth Handling ---
function initAuthSession() {
  const saved = localStorage.getItem("lfspro_user");
  if (saved) {
    try {
      currentUser = JSON.parse(saved);
      showUserCard(currentUser);
    } catch {
      localStorage.removeItem("lfspro_user");
    }
  }
}

async function handleLoginSubmit(e) {
  e.preventDefault();
  const u = document.getElementById("login-username").value.trim();
  const p = document.getElementById("login-password").value.trim();
  const msgEl = document.getElementById("login-msg");

  try {
    const res = await fetch("/api/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: u, password: p })
    });
    const data = await res.json();
    if (data.success) {
      currentUser = data.user;
      localStorage.setItem("lfspro_user", JSON.stringify(currentUser));
      showUserCard(currentUser);
      msgEl.className = "form-msg success";
      msgEl.textContent = `Welcome back, ${u}!`;
    } else {
      msgEl.className = "form-msg error";
      msgEl.textContent = data.message || "Invalid credentials.";
    }
  } catch (err) {
    msgEl.className = "form-msg error";
    msgEl.textContent = "Server communication failure.";
  }
}

async function handleRegisterSubmit(e) {
  e.preventDefault();
  const u = document.getElementById("reg-username").value.trim();
  const p = document.getElementById("reg-password").value.trim();
  const msgEl = document.getElementById("reg-msg");

  try {
    const res = await fetch("/api/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: u, password: p })
    });
    const data = await res.json();
    if (data.success) {
      msgEl.className = "form-msg success";
      msgEl.textContent = data.message + " You can now sign in.";
      document.getElementById("form-register").reset();
      fetchStats();
    } else {
      msgEl.className = "form-msg error";
      msgEl.textContent = data.message || "Registration failed.";
    }
  } catch (err) {
    msgEl.className = "form-msg error";
    msgEl.textContent = "Server communication failure.";
  }
}

function showUserCard(user) {
  document.getElementById("account-guest-view").style.display = "none";
  document.getElementById("account-user-view").style.display = "block";

  document.getElementById("view-username").textContent = user.username.toUpperCase();
  document.getElementById("view-rank").textContent = user.rank || "Driver";
  document.getElementById("view-license").textContent = (user.license || "S3") + " License";
  document.getElementById("view-credits").textContent = (user.credits || 1000).toLocaleString() + " Credits";
  
  const hwidBadge = document.getElementById("view-hwid-status");
  if (user.hwid_bound) {
    hwidBadge.textContent = "🔒 HWID Status: LOCKED (Device Protected)";
    hwidBadge.style.color = "var(--accent-green)";
  } else {
    hwidBadge.textContent = "⚠️ HWID Status: UNBOUND (Locks on first launcher run)";
    hwidBadge.style.color = "var(--accent-yellow)";
  }

  // Update nav button
  const navUserArea = document.getElementById("nav-user-area");
  if (navUserArea) {
    navUserArea.innerHTML = `
      <button class="btn btn-outline btn-sm" onclick="switchTab('account')">
        👤 ${escapeHtml(user.username)}
      </button>
    `;
  }
}

function handleLogout() {
  localStorage.removeItem("lfspro_user");
  currentUser = null;
  document.getElementById("account-guest-view").style.display = "grid";
  document.getElementById("account-user-view").style.display = "none";
  
  const navUserArea = document.getElementById("nav-user-area");
  if (navUserArea) {
    navUserArea.innerHTML = `<button class="btn btn-primary btn-sm" id="btn-open-auth" onclick="switchTab('account')">Driver Login</button>`;
  }
}

async function handleHwidReset() {
  if (!currentUser) return;
  const msgEl = document.getElementById("hwid-msg");
  if (!confirm(`Are you sure you want to reset HWID binding for ${currentUser.username}?`)) return;

  try {
    const res = await fetch("/api/reset_hwid", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: currentUser.username })
    });
    const data = await res.json();
    if (data.success) {
      currentUser.hwid_bound = false;
      localStorage.setItem("lfspro_user", JSON.stringify(currentUser));
      document.getElementById("view-hwid-status").textContent = "⚠️ HWID Status: UNBOUND (Locks on next launcher run)";
      document.getElementById("view-hwid-status").style.color = "var(--accent-yellow)";
      msgEl.className = "form-msg success";
      msgEl.textContent = data.message;
    } else {
      msgEl.className = "form-msg error";
      msgEl.textContent = data.message || "Failed to reset HWID.";
    }
  } catch (err) {
    msgEl.className = "form-msg error";
    msgEl.textContent = "Server communication failure.";
  }
}

// --- Helpers ---
function copyJoinCommand(ip, port) {
  copyText(`${ip}:${port}`);
  alert(`Server address copied: ${ip}:${port}\nIn LFS Multiplayer select: Join Specific Host.`);
}

function downloadSkinMock(name) {
  alert(`Downloading livery "${name}" to LFS data/skins/ folder.`);
}

function copyText(txt) {
  navigator.clipboard.writeText(txt).catch(() => {
    const el = document.createElement("textarea");
    el.value = txt;
    document.body.appendChild(el);
    el.select();
    document.execCommand("copy");
    document.body.removeChild(el);
  });
}

function escapeHtml(str) {
  if (!str) return "";
  return str.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
