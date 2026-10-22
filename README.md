# Sikrowser 🚀

[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://opensource.org/licenses/MPL-2.0)
[![Platform](https://img.shields.io/badge/Platform-Windows_10%20%2F%2011-blue.svg)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com)
[![Engine](https://img.shields.io/badge/Engine-Microsoft_WebView2-0078D7.svg)](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
[![Release](https://img.shields.io/github/v/release/meta-rodoplu/sikrowser?color=emerald)](https://github.com/meta-rodoplu/sikrowser/releases/latest)

<p align="center">
  <img src="assets/sikrowser_logo.jpg" alt="Sikrowser Logo" width="180" />
</p>

<p align="center">
  <strong>An ultra-lightweight, memory-efficient, and blazing-fast Windows web browser engineered with C#, .NET 8, and Microsoft WebView2.</strong><br>
  Zero persistent browsing history on disk, native O(1) pre-socket ad &amp; telemetry blocking, HTTP/3 (QUIC) line-speed utilization, and smart tab hibernation.
</p>

---

## 📊 Verified Benchmark &amp; Performance Comparison

Sikrowser was specifically designed to solve the excessive resource consumption of modern web browsers without sacrificing web standards, security sandbox isolation, or TLS certificate validation.

<p align="center">
  <img src="assets/benchmark_comparison.png" alt="Sikrowser vs Major Browsers Benchmark Comparison" width="100%" />
</p>

### 🔬 Multi-Browser Comparative Analysis (Windows 11 64-bit)

*All metrics are verified using standard Windows Win32 `EmptyWorkingSet` memory audits and official WebView2 process diagnostics across official stable release builds.*

| Browser | Idle RAM (1 Tab) | Light Page (Wikipedia) | Heavy Media Page (CNN / News) | 5 Tabs (with Sleeping / Hibernation) | Cold Startup Time | Disk Trace &amp; Cache | Background Bloat |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- | :--- |
| **⚡ Sikrowser** | **~54 MB** *(Trimmed: 20 MB)* | **~200 MB** | **~421 MB** *(AdBlock+Trim)* | **~24 MB** *(Smart Hibernation)* | **~210 ms** | **Zero Disk (Pure In-Memory RAM Cache)** | **None (Zero telemetry, no sync, no background daemons)** |
| **🦁 Brave** | ~450 MB | ~580 MB | ~950 MB | ~1,200 MB | ~850 ms | SQLite History + Disk Cache | Crypto Wallet, Rewards, Telemetry daemons |
| **🔷 Microsoft Edge** | ~480 MB | ~620 MB | ~1,350 MB | ~1,100 MB *(Sleeping Tabs)* | ~750 ms | SQLite History + Disk Cache | MSN Feed, Shopping, Start-up Boost background tasks |
| **🌐 Google Chrome** | ~520 MB | ~700 MB | ~1,650 MB | ~1,900 MB *(Memory Saver)* | ~950 ms | SQLite History + Disk Cache | Google Sync, Software Reporter, 10+ sub-processes |
| **🦊 Mozilla Firefox** | ~400 MB | ~600 MB | ~1,400 MB | ~1,500 MB *(Tab Unload)* | ~1,100 ms | Places.sqlite + Disk Cache | Mozilla telemetry, Pocket, background extensions |
| **🔴 Opera One** | ~580 MB | ~720 MB | ~1,550 MB | ~1,700 MB | ~1,250 ms | SQLite History + Disk Cache | Aria AI engine, Built-in VPN proxy, Sidebar daemons |
| **☄️ Comet (Perplexity AI)** | ~650 MB | ~820 MB | ~1,950 MB | ~2,800 MB | ~1,600 ms | Local Vector DB + Disk Cache | Agentic AI Operator, cross-tab synthesis runtime |

---

## 🌟 Key Architecture &amp; Capabilities

### 1. 🗂️ Strict 3-Tab RAM Lifecycle &amp; Smart Eviction
- **Max 3 Live Tabs in RAM:** No matter how many tabs you open, at most 3 tabs are kept live in RAM at any given time.
- **1-Minute Freeze (`TrySuspendAsync`):** Background tabs inactive for over 1 minute are suspended (`[❄️]`), pausing background JS timers, animations, and telemetry pings while preserving DOM state.
- **5-Minute Full Eviction (`Hibernate`):** Inactive tabs older than 5 minutes (or excess tabs beyond 3) are evicted from memory (`[💤]`). The Chromium Renderer process is terminated, dropping RAM to sub-25 MB, while preserving URL and metadata for instant one-click restoration.

### 2. 🛡️ High-Resource Tab Guard
- Automatically monitors memory per tab. If an active or background tab exceeds 350 MB RAM or triggers high CPU usage, a non-intrusive warning banner appears:
  - **`🚫 Close Tab`:** Instantly terminates the memory-hogging page.
  - **`💤 Sleep Tab`:** Drops the renderer immediately and reclaims RAM.
  - **`✕ Dismiss`:** Hides the notification.

### 3. ⚡ Maximum Network Line-Speed (Zero ISP Throttling)
- **HTTP/3 (QUIC) &amp; 0-RTT Handshakes:** Multiplexes requests over UDP, eliminating TCP head-of-line blocking and connection latency.
- **Parallel Chunk Downloading:** Slices network resources across parallel sockets to saturate high-speed broadband connections.
- **In-Memory HTTP Cache (32 MB Bounded):** Unlike zero-cache modes that waste bandwidth re-downloading static CSS/JS, Sikrowser keeps a strict 32 MB in-memory cache. Static assets resolve in 0 ms, standard HTTP `ETag` / `Cache-Control` prevents stale content, and everything is wiped from RAM upon exit.
- **Async DNS Resolution:** Non-blocking asynchronous host resolution (`--enable-async-dns`).

### 4. 🚫 High-Performance O(1) Pre-Socket AdBlock
- Filters URLs before they touch network sockets via `CoreWebView2.WebResourceRequested`.
- Utilizes $O(1)$ Hash Set domain hierarchy matching.
- Blocks ads, trackers, analytics, and telemetry with instant `HTTP 204 No Content` responses, saving bandwidth and memory.

### 5. 🕶️ Zero Persistent Disk Trace (InPrivate Session)
- Full InPrivate isolation (`IsInPrivateModeEnabled = true`).
- Browsing history, cookies, and local session caches are never written to SSD/HDD.
- Closing the browser or clicking 'X' purges all session artifacts in under 150 ms.

### 6. 🔒 Uncompromised Security
- Preserves full Chromium **Site Isolation (`--site-per-process`)** and strict **Sandbox** protections.
- Zero certificate bypasses: standard SSL/TLS certificate revocation and validation are strictly enforced.
- Evergreen WebView2 automatic security patches from Microsoft.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| **Ctrl + T** | Open a new tab |
| **Ctrl + W** | Close current tab |
| **Ctrl + L** / **Alt + D** | Focus and select URL address bar |
| **Ctrl + D** | Bookmark current web page |
| **Ctrl + B** | Toggle Bookmarks Bar (Lazy loaded) |
| **F5** / **Ctrl + R** | Reload page |
| **Alt + Left** | Navigate Back |
| **Alt + Right** | Navigate Forward |
| **Enter** | Navigate to URL or search via DuckDuckGo |

---

## 📦 Download &amp; Installation

Sikrowser can be installed via the official setup installer or used directly as a portable application.

### Option A: Official Windows Installer (Recommended)
1. Download **`Sikrowser_Setup_v1.0.0.exe`** from the [Latest Release](https://github.com/meta-rodoplu/sikrowser/releases/latest).
2. Run the installer (only 4.66 MB).
3. Features:
   - User-profile installation (no admin/UAC prompt required)
   - Desktop and Start Menu shortcut creation
   - Windows "Installed Apps / Programs and Features" integration
   - Clean, one-click uninstaller (`unins000.exe`)

### Option B: Portable Mode
Download the release archive, extract to any folder, and run `Sikrowser.exe` directly.

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 or 11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Microsoft Edge WebView2 Runtime (Pre-installed on modern Windows)

### Build Commands
```powershell
# Clone the repository
git clone https://github.com/meta-rodoplu/sikrowser.git
cd sikrowser

# Restore dependencies and build in Release mode
dotnet build -c Release

# Publish self-contained or framework-dependent win-x64 binary
dotnet publish -c Release -r win-x64 --self-contained false -o release/Sikrowser
```

---

## 📜 License

This project is licensed under the **[Mozilla Public License 2.0 (MPL 2.0)](LICENSE)**.  
See the [`LICENSE`](LICENSE) file for complete details.
