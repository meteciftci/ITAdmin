# SAS Portal v2 Installation Flow

## Amaç

SAS Portal v2’nin müşteri sunucusuna nasıl kurulacağını, ilk kurulum akışının hangi adımlardan oluşacağını ve güncelleme ile yedekleme ilkelerini tanımlamak.

---

## Kurulum Tipleri

### Standalone Kurulum

- PostgreSQL, vendor veya müşteri tarafından installer ile kurulabilir (kurulum tasarımına göre paket içinde veya müşteri yönetiminde).
- Veritabanı, kullanıcı, şema ve migration adımları **setup süreci** tarafından yönetilir.
- Küçük ve orta ölçek müşteriler için **varsayılan/önerilen** senaryodur.

### External PostgreSQL Kurulumu

- Müşterinin mevcut PostgreSQL sunucusu kullanılır.
- DBA tarafından oluşturulmuş veritabanı ve kullanıcı bilgileri setup’a girilir veya bağlantı testi ile doğrulanır.
- Kurumsal müşteriler için **desteklenen** seçenektir.

---

## Installer Sorumlulukları

Kurulum aracı (installer) en azından aşağıdaki adımları yönetmelidir:

1. Sistem gereksinimlerini kontrol etmek (OS, disk, RAM, ağ vb.)
2. IIS ve uygun .NET runtime / Hosting Bundle kurulumunu kontrol etmek
3. PostgreSQL’in mevcut olup olmadığını tespit etmek; tasarıma göre gerekirse kurulumu yapmak veya atlama seçeneği sunmak
4. Veritabanı bağlantı bilgilerini almak veya (standalone’da) oluşturmak
5. Bağlantıyı test etmek
6. Gerekliyse veritabanı ve uygulama kullanıcısını oluşturmak
7. **Migration bundle** veya **onaylı SQL script** ile şemayı güncellemek
8. Backend publish çıktılarını hedef klasöre kopyalamak
9. Frontend statik çıktılarını site köküne kopyalamak
10. IIS site ve application pool oluşturmak / yapılandırmak
11. Ortam ve yapılandırma (`appsettings` veya environment variables) değerlerini yazmak
12. Uygulamayı başlatılabilir hale getirmek
13. Sağlık kontrollerini çalıştırmak: `/health`, `/api/setup/status` (ürün içi sözleşmeye uygun olarak)

Installer’ın kapsamı ve otomatik/etkileşimli adımlar ürün sürümüne göre netleştirilir; bu dokümanda beklenen **sorumluluk kümesi** tanımlanır.

---

## Installer Tarafından Üretilecek veya Sorulacak Yapılandırma Anahtarları

Aşağıdaki yapılandırmalar tipik olarak kurulumda üretilir veya operatörden alınır (isimlendirme ASP.NET Core konvansiyonuna uygun):

| Alan | Örnek / not |
|------|--------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL bağlantı dizesi |
| `Jwt__Key` | İmzalama anahtarı (güvenli üretim ve saklama) |
| `Jwt__Issuer` | Token issuer |
| `Jwt__Audience` | Token audience |
| `Jwt__AccessTokenMinutes` | Erişim token süresi |
| `Jwt__RefreshTokenDays` | Yenileme token süresi |
| `Setup__SetupKey` | Kurulum sırasında `/setup` gibi yüzeylerin korunması için (tasarıma göre) |
| Lisans | Dosya yolu veya yapılandırmadan okunan lisans içeriği |
| DataProtection | Anahtar yolu veya güvenli depolama (gerekiyorsa, çoklu sunucu/cluster senaryosu için özellikle) |

Gerçek anahtar isimleri codebase ile uyumlu tutulmalıdır; bu tablo işlevsel gereksinimi özetler.

---

## Application Setup Wizard

Installer tamamlandıktan sonra kullanıcı tarayıcıdan **`/setup`** (veya üründe tanımlı ilk kurulum URL’si) yönlendirilir.

**Wizard’ın yapması gerekenler:**

- Lisans bilgisini almak veya doğrulamak (imza, süre, modüller)
- LDAP ayarlarını toplamak
- LDAP bağlantı testi yapmak
- İlk **SuperAdmin** kullanıcısını oluşturmak
- Varsayılan sistem rollerini oluşturmak
- Varsayılan izin kayıtlarını (permission seed) oluşturmak
- `Setup:IsCompleted` (veya eşdeğeri) yapılandırmasını **`true`** yaparak kurulumu kilitlemek

---

## Database Kurulum Prensibi

- Normal işletmede kullanıcıların doğrudan veritabanına bağlanması **beklenmez**.
- Şema ve veri düzeyinde değişiklikler **uygulama**, **setup/update aracı** veya **resmi destek script’leri** üzerinden yapılır.
- Manuel veritabanı müdahalesi, destek politikası gereği **kapsam dışı** sayılabilir veya müşteri sorumluluğunda değerlendirilir.

---

## Update Flow

Güncelleme sırasında **önerilen akış:**

1. Lisans ve support güncelleme hakkının geçerliliğini kontrol etmek (politika uyarınca)
2. Veritabanı yedeği almak veya operatörden açık onay almak
3. IIS application pool’u durdurmak
4. Yeni backend ve frontend dosyalarını kopyalamak
5. Migration bundle veya SQL script ile şema güncellemesini uygulamak
6. Application pool’u başlatmak
7. `/health` (ve gerekiyorsa diğer health uçları) ile kontrol
8. Kısa smoke test (giriş, kritik bir API, temel liste vb.)

---

## Backup ve Rollback Notu

- **Dosya geri alma:** Eski dosyaların ve konfigürasyonun yedeği varsa relatif olarak kolaydır.
- **Veritabanı geri alma:** Şema güncellemesi sonrası “geri migration” karmaşıktır ve her zaman güvenilir değildir.

**Karar:** Production ortamında geri alma stratejisi öncelikle **yedekten restore** üzerine kurulmalıdır; kritik güncelleme öncesi **tam veritabanı yedeği** zorunlu kabul edilmelidir.
