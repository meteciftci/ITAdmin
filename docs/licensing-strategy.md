# SAS Portal v2 Licensing Strategy

## Amaç

SAS Portal v2 için kapalı kaynak dağıtım modeli, **modül bazlı lisanslama**, imzalı lisans güven modeli ile **yetki (permission)** katmanından ayrımı ve destek/güncelleme politikasının çerçevesini yazılı hale getirmek.

---

## Ürün Modeli

- Uygulama **kapalı kaynak** olarak dağıtılır.
- Müşteriye kaynak kod verilmez.
- Müşteriye **kurulum paketi**, **güncelleme paketi**, **lisans dosyası veya lisans verisi** ve kullanıcı/dökümantasyon materyalleri sağlanır.

---

## Lisanslama Yaklaşımı

**Modül bazlı lisanslama** desteklenir; her özellik alanı ticari olarak ayrı açılıp kapanabilir.

Örnek modül isimleri (ürün roadmap’ine göre genişletilebilir):

| Modül örneği | Not |
|----------------|-----|
| Core | Çekirdek portal, auth, kullanıcı/rol/izin iskeleti |
| Active Directory Management | AD yönetimi (ürün fazında tanımlandığında) |
| Storage Management | Depolama yönetimi |
| Backup Management | Yedekleme yönetimi |
| Audit & Security Reports | Denetim ve güvenlik raporları |
| Advanced Integrations | Gelişmiş entegrasyonlar |

Gerçek modül kodları ve SKU’lar ürün yönetimi tarafından sabitlenir; dokümanda yaklaşım ve ayrım prensibi hedeflenir.

---

## Lisans Türleri

- **Kalıcı lisans** (sınırsız süre yazılı kullanım hakkı, destek güncelleme ayrı)
- Belirli süreli **support / update** hakkı
- Örnek: İlk yıl güncelleme ve destek pakete dahil; sonraki yıllarda support paketi ile devam

Lisans ticari paketleri sözleşme ve SKU ile netleştirilir.

---

## Lisans İçeriği

Lisans taşıyıcısı (dosya veya yapılandırılmış blob) teknik olarak aşağıdakileri **içerebilir**:

- Customer identifier
- Lisanslı modül listesi (`Licensed modules`)
- Lisans tipi (`License type`)
- `IssuedAt`
- `ExpiresAt` (varsa)
- `SupportUntil`
- `MaxUsers` veya kullanıcı sınırı (gerekiyorsa)
- Ürün/sürüm kısıtları (`Product version constraints`)
- Kriptografik **imza** (`Signature`)

Hangi alanın zorunlu olduğu ürün sürümüne göre tanımlanır.

---

## İmzalı Lisans Modeli

| Karar | Gerekçe |
|-------|---------|
| Lisans vendor **private key** ile imzalanır | Sahteciliği önlemek ve içeriği değiştirmeden doğrulamak için standart yaklaşım |
| Uygulama yalnızca **public key** ile doğrular | Çalışan ortamda imza oluşturma yetkisi bulunmaz |
| Private key uygulama, image veya müşteri sunucusunda **bulunmaz** | Anahtar sızıntısı riskini minimize eder |
| Modül aç/kapa işlemini yalnızca DB boolean alanlarına güvenmek **yeterli güvenlik sayılmaz** | Lisans doğrulaması imza ile zincir tamamlanmalıdır |

---

## License Guard

Backend’de endpoint veya iş akışları için **lisans kontrolü**, **izin kontrolünden ayrı** bir katman olarak düşünülmelidir.

**Örnek çift kontrol:**

1. İlgili modül için lisans geçerli mi? *(license guard)*  
2. Kullanıcının `ActiveDirectory.Users.View` gibi ilgili **permission**’ı var mı?

İkisi de uygun olduğunda işlem yapılır; biri eksikse **403** (veya lisans süresi/ geçerliliğine göre tasarlanmış kod) uygun şekilde döner.

---

## SuperAdmin ve Lisans İlişkisi

**Kritik karar:**

| Rol | Permission | License |
|-----|-------------|---------|
| SuperAdmin | **Bypass eder** (tasarımda süper yöneticiye özel işlemler; yine audit/security politika uyumu korunur) | **Bypass etmez** |

Yani SuperAdmin bile **lisanssız bir modülün** backend işlevlerine erişemez; ticari/modül kapısı lisans ile korunur.

---

## Permission ve License Ayrımı

| Katman | Kapsam | Amaç |
|--------|---------|------|
| **Permission** | Kullanıcı ve rol bazlı | Organizasyon içi “kim ne yapabilir” |
| **License** | Ürün/modül bazlı | “Bu müşteri hangi özellikleri satın almış” |

İkisi **üst üste bindirilen ayrı katmanlar** olarak ele alınır; karıştırılmaz.

---

## Frontend Davranışı

**Frontend:**

- Lisanssız modüllere ait menüleri **göstermez** (UX ve bilgi sızdırmama).
- İzni olmayan eylemler için ilgili butonları **göstermez** veya pasifleştirir.
- SuperAdmin için permission kaynaklı menüler **görünebilir** — ancak **lisanssız modüller** SuperAdmin dahil **görünmemelidir** (ticari özellik sızıntısı ve yanlış beklenti önlenir).

**Backend:**

- İstemci manipülasyonuna karşı **her zaman** hem license hem permission (ve auth) doğrulanır.

---

## Gelecek Teknik Bileşenler

Aşağıdaki bileşenler uygulamaya eklenerek strateji kod ile desteklenebilir:

- `ILicenseService`
- `LicenseValidationResult`
- `ModuleRegistry`
- `RequireLicense` attribute veya filtresi
- `RequirePermission` attribute veya filtresi
- SuperAdmin için **permission bypass** içeren yetki işleyici
- **License bypass içermeyen** modül koruması (module guard)

Uygulama zaman çizelgesi ve sıra ürün yönetimi ile planlanır; bu liste mimari hedef özeti olarak kalır.

---

## Destek ve Güncelleme

- Güncelleme paketleri, **support / update süresi aktif** müşterilere sunulur.
- Süresi dolan müşteriler mevcut **kurulu sürümü** kullanmaya devam edebilir (sözleşmeye bağlı olarak).
- Yeni modül veya ana sürüm erişimi, support ve ticari haklarla bağlanabilir.

Politika satış ve sözleşme ile kesinleşir; teknik doğrulama (ör. updater içinde tarih/modül kontrolü) buraya uyumlu uygulanır.
