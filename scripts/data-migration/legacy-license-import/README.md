# Eski Lisans Uygulaması Veri Aktarımı

Eski (MBB) lisans uygulamasından export edilen 8 CSV'yi ITAdmin veritabanına
aktarır. Tüm aktarım **tek transaction** içinde çalışır; sayımlar beklendiği gibi
değilse `ROLLBACK` ile hiçbir şey yazılmadan çıkılır.

## Dosyalar

| Dosya | Ne yapar |
|---|---|
| `01_staging_tables.sql` | `legacy_import` şemasında CSV başlıklarıyla birebir kolonlu tablolar oluşturur |
| `02_transform.sql` | Staging → ITAdmin tabloları dönüşümü (tek transaction, otomatik COMMIT etmez) |
| `03_verify.sql` | Salt-okunur doğrulama; legacy vs aktarılan sayılar + düşen satırlar |
| `99_rollback.sql` | `created_by = 'legacy-import'` olan tüm satırları FK sırasına göre siler |

## Adımlar (pgAdmin4)

1. **Yedek al.** `pg_dump` ya da en azından lisans tablolarının snapshot'ı.

2. `01_staging_tables.sql` dosyasını Query Tool'da çalıştır.

3. Her CSV'yi ilgili staging tablosuna aktar:
   `legacy_import.<tablo>` üzerine sağ tık → **Import/Export Data…** → Import,
   Format `csv`, Header `Yes`, Delimiter `,`, Quote `"`, Encoding `UTF8`.
   Kolon sırası zaten eşleştiği için Columns sekmesine dokunma.

   | CSV | Tablo |
   |---|---|
   | `companies.csv` | `legacy_import.companies` |
   | `applications.csv` | `legacy_import.applications` |
   | `purchases.csv` | `legacy_import.purchases` |
   | `purchase_applications.csv` | `legacy_import.purchase_applications` |
   | `license_assignments.csv` | `legacy_import.license_assignments` |
   | `license_requests.csv` | `legacy_import.license_requests` |
   | `license_request_items.csv` | `legacy_import.license_request_items` |
   | `license_request_users.csv` | `legacy_import.license_request_users` |

4. (İsteğe bağlı) `03_verify.sql` içindeki 2–4 numaralı sorguları şimdi çalıştır —
   sadece staging'e bakarlar, düşecek yetim satır var mı önceden görürsün.

5. `02_transform.sql` dosyasını çalıştır. Sonunda bir sayım tablosu döner ve
   transaction **açık kalır**. Sayımlar doğruysa ayrı bir sorgu olarak:
   ```sql
   COMMIT;
   ```
   değilse:
   ```sql
   ROLLBACK;
   ```

6. `03_verify.sql` dosyasının tamamını çalıştır, sonuçları gözden geçir.

7. Her şey yolundaysa staging'i kaldır:
   ```sql
   DROP SCHEMA legacy_import CASCADE;
   ```

Yeniden aktarım gerekirse: `99_rollback.sql` → `COMMIT` → `02_transform.sql`.

## Eşleştirme kararları

### Ürünler ← `applications`
Tek bir **"İçe Aktarılan"** kategorisi oluşturulur (ITAdmin'de `category_id`
zorunlu). `brand` aynen taşınır. Orijinal id `description` içinde iz olarak durur.

### Firmalar ← `companies`
`contact_person` → `contact_person_name`. İsimdeki baştaki/sondaki boşluklar
kırpılır (ör. "Digitolia  " → "Digitolia"); yakın-kopya kayıtlar (id 6 ve 12)
ayrı satır olarak kalır, elle birleştirebilirsin.

### Satın almalar ← `purchases`
`procurement_type`: **1 → Tender**, **3 → DirectPurchase**, diğer → Other.
Belge numarası türüne göre `tender_number` veya `direct_purchase_number`
alanına yazılır; eşleşmeyen türlerde `notes` içine düşer. Durum: **Active**.

### Paketler ← `purchase_applications`
- `license_type`: **1 → Perpetual**, **2 → Subscription**, diğer → Other
- `user_count` → `quantity`
- `license_end_date` → `end_date`; doluysa `renewal_required = true`
- `license_detail` + tedarikçi firma adı + legacy id → `license_notes`
- Bitiş tarihi geçmişse `status = Expired`, değilse `Active`
- Tedarikçi firma paket düzeyinde tutulamadığı için (ITAdmin'de firma satın alma
  düzeyinde) not alanına yazılır

### Zimmetler ← `license_assignments`  → `license_seat_assignments` (yeni tablo)
- `assigned_user_name` → `display_name`, `assigned_user_tcno` → `national_id`,
  `assigned_email` → `mail` + `user_principal_name`
- `ad_object_id` boş bırakılır (AD bağı yok) — uygulamadan tekrar eşleştirebilirsin
- Eski uygulamada "iade/serbest" durumu olmadığı için tüm zimmetler **Active**
- "… yerine atandı" ifadeleri `note` alanında korunur; `replaces_assignment_id`
  devir zinciri NULL bırakılır ve bundan sonra uygulamadaki **Devret** aksiyonu
  doldurur
- Paketi bulunamayan zimmet satırları **atlanır** (`03_verify.sql` bölüm 2 listeler)

### Talepler ← `license_requests`
- `source`: **1 → OfficialLetter**, **2 → CorporateRequestSystem**, diğer → Other
- `status`: **1 → Pending**, **2 → Draft**, **3 → Fulfilled**, **4 → InReview**
- Eski veride talep eden birim yok → `requester_unit_display_name =
  "Bilinmiyor (içe aktarıldı)"`, DN/GUID boş

### Talep kalemleri ← `license_request_items` (**birleştirilir**)
Eski uygulama "1 satır = 1 kişi" tutmuş. ITAdmin'de `(request_id, product_id)`
**UNIQUE**, kalem = ürün + adet. Bu yüzden `(talep, ürün)` bazında gruplanır,
adetler toplanır. Talep karşılanmışsa kalem durumu `Fulfilled`, değilse `Pending`.

### Talep kalemi kullanıcıları ← `license_request_users`
Birleştirilen kaleme bağlanır. `ad_object_id = 'legacy:<tcno>'` (ITAdmin'de
zorunlu; `(request_item_id, ad_object_id)` UNIQUE — aynı kişi tekrarında ilk
satır alınır).

## Sınırlamalar

- **Maliyet/tutar** eski export'ta yok — tüm tutar alanları NULL.
- **Talep eden birim** eski veride olmadığından placeholder yazılır; gerçek
  birimi elle veya ayrı bir eşleştirmeyle güncellemen gerekir.
- **Devir zinciri** geçmişe dönük kurulmaz (isim eşleştirmesi güvenilmez); metin
  notu korunur, ileriye dönük zincir uygulamadan işler.
- Zimmet `assigned_date` alanı, kayıt tarihinden türetilir; saat bilgisi atılır.
