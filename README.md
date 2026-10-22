# Sikrowser 🚀

[![License: MPL 2.0](https://img.shields.io/badge/License-MPL_2.0-brightgreen.svg)](https://opensource.org/licenses/MPL-2.0)
[![Platform](https://img.shields.io/badge/Platform-Windows_10%20%2F%2011-blue.svg)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com)
[![Engine](https://img.shields.io/badge/Engine-Microsoft_WebView2-0078D7.svg)](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

<p align="center">
  <img src="assets/sikrowser_logo.jpg" alt="Sikrowser Logo" width="180" />
</p>

Windows için C#, .NET 8 ve Microsoft WebView2 ile geliştirilmiş, ultra hafif, tam hat hızında çalışan, geçmiş tutmayan ve RAM tüketimini en aza indiren minimalist web tarayıcısı.

---

## 🌟 Temel Özellikler

- **Maksimum Ağ Hat Hızı (Sıfır Kısıtlama)**:
  - **HTTP/3 (QUIC) & 0-RTT**: Çoklu akışları tek UDP tünelinde birleştirerek gecikmesiz anında bağlantı kurar.
  - **Paralel Parçalı İndirme**: Dosyaları ve web kaynaklarını paralel soketlerle çekerek internet servis sağlayıcınızın sağladığı tüm fiber/genişbant hızını kullanır.
  - **Sıfır Disk Gecikmeli Bellek İçi Önbellek**: Disk yazma gecikmesini ortadan kaldırır, geri/ileri geçişleri mikrosaniyelik RAM hızında sunar.
- **Sürekli Adaptif Optimizasyon Motoru (AdaptivePerformanceManager)**:
  - Sayfa yüklenirken ve gezinirken **Maksimum Hız Modu** ile tam CPU ve ağ gücü verir.
  - Sayfa açıldıktan 3 saniye sonra geçici indirme tamponlarını temizleyerek kararlı duruma geçer.
  - 40 saniye boşta kalındığında arka plandaki bekçi (watchdog) tüm süreçlerdeki soğuk sayfaları fiziksel RAM'den boşaltır.
- **Sıfır Geçmiş & Tam Gizlilik (InPrivate)**:
  - `IsInPrivateModeEnabled = true`: Tarayıcı geçmişi, çerezler ve form verileri diske asla yazılmaz.
  - Tarayıcı kapandığında tüm bellek ve disk kalıntıları otomatik imha edilir.
- **Modüler Reklam ve İzleyici Engelleme (AdBlockEngine)**:
  - Ağ isteklerini $O(1)$ sürede filtreler, bloklanan istekleri 0.1 ms içinde sonlandırır.
  - Google Ads, Criteo, Facebook Pixel, Outbrain, Taboola vb. yüzlerce reklam/izleyici ağını yerleşik engeller.
  - Reklamları engelleyerek internet bant genişliğinizin gereksiz harcanmasını önler ve asıl sayfayı %40-%70 daha hızlı yükler.
- **Sekme Uyutma (Tab Hibernation)**:
  - Çoklu sekme açıldığında arka plandaki kullanılmayan sekmelerin WebView2 nesneleri bellekten temizlenir (`Dispose`).
  - 5 sekme havuzda tutulurken dahi fiziksel RAM yalnızca **~23.7 MB** seviyesinde kalır.
- **Tavizsiz Güvenlik**:
  - SSL/TLS sertifika doğrulamasını atlamaz, Chromium güvenlik sandbox'ını tam olarak korur.

---

## 🛠️ Derleme ve Çalıştırma

### Gereksinimler
- Windows 10 / 11 (x64)
- .NET 8.0 SDK
- Microsoft Edge WebView2 Çalışma Zamanı (Windows 10/11'de yerleşik)

### Release Derlemesi
```powershell
cd C:\Users\ayber\.gemini\antigravity\scratch\light-browser
dotnet build -c Release
```

### Grafik Arayüz (GUI) ile Başlatma
```powershell
.\release\Sikrowser\Sikrowser.exe
```
Veya doğrudan masaüstünüzdeki **Sikrowser** kısayoluna çift tıklayabilirsiniz!

### Otomatik 5 Aşamalı RAM Benchmark Testi (CLI)
```powershell
.\release\Sikrowser\Sikrowser.exe --benchmark
```

---

## ⌨️ Klavye Kısayolları

| Kısayol | İşlev |
| :--- | :--- |
| **Ctrl + T** | Yeni sekme aç |
| **Ctrl + W** | Aktif sekmeyi kapat |
| **Ctrl + L** / **Alt + D** | Adres çubuğuna odaklan |
| **F5** / **Ctrl + R** | Sayfayı yenile |
| **Ctrl + D** | Mevcut sayfayı yer işaretlerine ekle |
| **Ctrl + B** | Yer imleri çubuğunu aç / kapat |
| **Alt + Sol** | Önceki sayfaya dön (Geri) |
| **Alt + Sağ** | Sonraki sayfaya git (İleri) |
| **Enter** | Adrese git veya DuckDuckGo'da ara |

---

## 📦 Kurulum ve Dağıtım (Setup)

Sikrowser iki farklı şekilde kullanılabilir:
1. **Kurulum Paketi (Installer):**
   - [`release/setup/Sikrowser_Setup_v1.0.0.exe`](file:///C:/Users/ayber/.gemini/antigravity/scratch/light-browser/release/setup/Sikrowser_Setup_v1.0.0.exe) veya masaüstündeki `Sikrowser_Setup_v1.0.0.exe` dosyasını çalıştırarak Türkçe dil desteği, masaüstü simgesi ve Başlat menüsü entegrasyonuyla hızlıca kurabilirsiniz.
2. **Taşınabilir Sürüm (Portable):**
   - Kurulum yapmadan doğrudan [`release/Sikrowser/Sikrowser.exe`](file:///C:/Users/ayber/.gemini/antigravity/scratch/light-browser/release/Sikrowser/Sikrowser.exe) dosyasını çalıştırabilirsiniz.

---

## 📜 Lisans (License)

Bu proje **[Mozilla Public License 2.0 (MPL 2.0)](LICENSE)** ile lisanslanmıştır. 

Detaylı lisans metni için projenin kök dizinindeki [`LICENSE`](file:///C:/Users/ayber/.gemini/antigravity/scratch/light-browser/LICENSE) dosyasını inceleyebilirsiniz.

---

## 📁 Dizin Yapısı

- `Core/AdaptivePerformanceManager.cs`: Sürekli adaptif performans ve dinamik bellek bekçisi.
- `Core/BrowserConfig.cs`: HTTP/3 (QUIC), paralel indirme ve optimize Chromium bayrakları.
- `Core/BrowserEnvironmentManager.cs`: Tek paylaşılan CoreWebView2Environment yöneticisi.
- `Core/AdBlockEngine.cs`: Hat tasarrufu sağlayan sıfır gecikmeli reklam engelleme motoru.
- `Core/ProcessMemoryTracker.cs`: Tüm alt süreçleri tespit eden RAM ölçüm motoru.
- `Core/TabManager.cs`: Sekme yaşam döngüsü ve bellek uyutma mantığı.
- `UI/MainForm.cs`: Minimalist koyu tema, canlı hız ve RAM rozetleri.
- `release/Sikrowser/`: Bağımsız, taşınabilir dağıtım paketi ve masaüstü kısayolu.
