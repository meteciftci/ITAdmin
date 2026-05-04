# SAS Portal v2 Deployment Strategy

## Amaç

SAS Portal v2 uygulamasının production ortamında nasıl yayınlanacağını, backend ile frontend'in birlikte nasıl çalışacağını ve temel deployment kararlarını netleştirmek.

---

## Kaynak Kod Modeli

| Karar | Açıklama |
|-------|----------|
| Kapalı kaynak | Proje kapalı kaynak olarak ilerler. Kaynak kod müşteriye verilmez. |
| Dağıtım paketleri | Müşteriye kurulum/publish paketi, güncelleme (update) paketi ve lisans dosyası veya lisans verisi sağlanır. |

---

## Backend Katmanları ve Çalışma Modeli

Çözümde birden fazla .NET projesi bulunsa da **bu projeler ayrı ayrı IIS uygulaması değildir**:

- `SasPortal.Api`
- `SasPortal.Application`
- `SasPortal.Domain`
- `SasPortal.Infrastructure`
- `SasPortal.Persistence`

**Çalışan tek IIS bağlı uygulama:** `SasPortal.Api`.

Diğer projeler derlenmiş **DLL olarak** API projesi tarafından yüklenir ve tek process içinde çalışır; müşteri tarafında ayrı site veya pool ile yayınlanmazlar.

---

## IIS Yayın Modeli

**Önerilen model:**

- Tek IIS site
- **Site kökünde:** frontend statik dosyaları (`index.html`, `assets`, vb.)
- **`/api` altında:** backend API (aynı site üzerinden reverse proxy veya IIS alt uygulama ile yönlendirme)

Örnek URL'ler:

- `https://portal.customer.local/` → Frontend (SPA ve statik içerik)
- `https://portal.customer.local/api` → Backend API

**Bu modelin avantajları:**

- Tek domain ve tek kullanıcı deneyimi
- Tek SSL sertifikası
- CORS ve çok-origin yapılandırma ihtiyacının azalması
- Kurulum ve destek maliyetinin düşmesi

---

## Frontend ve Backend Haberleşmesi

Frontend, production ortamında API çağrılarını **göreceli path** ile yapmalıdır; örnekler:

- `/api/auth/login`
- `/api/auth/me`
- `/api/users`
- `/api/roles`

Development ortamında Vite proxy veya `baseUrl` gibi yönergeler kullanılabilir; **production build'de hardcoded localhost veya development ortamına özel sabit adres bulunmamalıdır.**

---

## Güvenlik Modeli

| Katman | Rol |
|--------|-----|
| Frontend | Güvenlik katmanı **değildir**; menü ve buton gizleme yalnızca UX içindir. |
| Backend | JWT authentication, izin tabanlı yetkilendirme (permission) ve lisans doğrulaması (license guard) asıl güvenliği burada sağlar. |

Endpoint adresini bilen bir kullanıcı, geçerli token ve uygun izin/lisans olmadan işlem yapamaz.

- Token yoksa veya geçersizse: **401 Unauthorized**
- Token var ancak izin/lisans uygun değilse: **403 Forbidden**

---

## Production Migration Kararı

| Karar | Gerekçe |
|-------|---------|
| API production'da başlangıçta otomatik migration çalıştırmaz | Şema güncellemeleri kontrollü ve izlenebilir olmalı; yanlışlıkla yan ortam şemayı değiştirme riski azalır. |
| Migration, setup/update aracı veya migration bundle / SQL script ile uygulanır | Release sürecine ve operasyonel runbook’a uyumluluk sağlar. |
| Uygulama, önceden hazır (migration uygulanmış) şema üzerinde çalışır | Beklenen durum ile gerçek veritabanı durumu uyumlu tutulur. |

---

## Önerilen Runtime Ortamı

- Windows Server
- IIS
- [.NET Hosting Bundle](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/hosting-bundle) / uyumlu runtime
- PostgreSQL
- Active Directory veya LDAP erişimi (ürün gereksinimine göre)

---

## Notlar

- Production’da uygulama veritabanı kullanıcısına **CREATE DATABASE**, süper kullanıcı veya şema düşürme gibi** gereksiz yönetim yetkileri** verilmemelidir; yalnızca uygulama şeması için gereken minimum yetki yeterlidir.
- Development ortamında hız için geniş yetkiler (ör. CREATEDB) verilebilir; production için önerilmez.
