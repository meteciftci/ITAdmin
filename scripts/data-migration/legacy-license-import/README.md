# Eski Lisans Uygulaması Veri Aktarımı

Eski (MBB) lisans uygulamasından export edilen 8 CSV'yi ITAdmin veritabanına aktarır.

## Kolay yol — tek dosya (önerilen)

`build_import_sql.py` 8 CSV'yi okuyup **tek, kendi kendine yeten bir SQL dosyası**
üretir: içinde veriler ITAdmin şemasına dönüştürülmüş halde (UUID PK'ler, enum
adları, "1 satır = 1 kişi" talep kalemlerinin `(talep, ürün)` bazında toplanması,
`license_assignments` → yeni `license_seat_assignments`) gömülüdür.

```bash
cd scripts/data-migration/legacy-license-import
python3 build_import_sql.py ~/Downloads > itadmin_import.sql
```

`~/Downloads` = 8 CSV'nin (companies.csv, applications.csv, purchases.csv,
purchase_applications.csv, license_assignments.csv, license_requests.csv,
license_request_items.csv, license_request_users.csv) bulunduğu klasör.

Sonra `itadmin_import.sql` içeriğini **pgAdmin web → Query Tool**'a yapıştır ve
çalıştır. Dosya:

- Zaten aktarılmışsa `RAISE EXCEPTION` ile durur (çift aktarımı engeller).
- Tüm insert'leri tek transaction'da yapar, sonunda `COMMIT;`.
- Her satıra `created_by = 'legacy-import'` yazar.

Geri almak için `99_rollback.sql` (bu işaretçiye göre siler).

> **Not:** üretilen `itadmin_import.sql` kişisel veri (ad, TC no, e-posta) içerir;
> depoya **commit edilmez** (`.gitignore`). Ürettiğin makinede tut.

### Migration önkoşulu

`license_seat_assignments` tablosu var olmalı — migration
`20260907122312_AddLicenseSeatAssignments` veya sonrası. Normalde bir sonraki
`Deploy-ITAdmin.ps1` bunu uygular.

## Alternatif — staging şeması + CSV import

Python çalıştıramıyorsan: `01_staging_tables.sql` (legacy_import şeması) → 8 CSV'yi
pgAdmin Import ile o tablolara yükle → `02_transform.sql` (tek transaction,
otomatik COMMIT etmez) → sayımlar doğruysa `COMMIT;` → `03_verify.sql`.
Aynı dönüşüm kuralları, sadece giriş yolu farklı.

## Eşleştirme kararları

### Ürünler ← `applications`
Tek bir **"İçe Aktarılan"** kategorisi (ITAdmin'de `category_id` zorunlu).
`brand` aynen taşınır; orijinal id `description`'da iz olarak durur.

### Firmalar ← `companies`
`contact_person` → `contact_person_name`. İsim boşlukları kırpılır; yakın-kopya
kayıtlar (id 6 ve 12) ayrı kalır, elle birleştirebilirsin.

### Satın almalar ← `purchases`
`procurement_type`: **1 → Tender**, **3 → DirectPurchase**, diğer → Other. Belge
no türüne göre `tender_number` / `direct_purchase_number` / `notes`. Durum: Active.

### Paketler ← `purchase_applications`
`license_type`: **1 → Perpetual**, **2 → Subscription**. `user_count` → `quantity`.
`license_end_date` → `end_date` (doluysa `renewal_required = true`). `license_detail`
+ tedarikçi firma adı + legacy id → `license_notes`. Bitiş tarihi geçmişse
`status = Expired`, değilse `Active`. (ITAdmin'de firma paket düzeyinde
tutulamadığı için nota yazılır.)

### Zimmetler ← `license_assignments` → `license_seat_assignments`
`assigned_user_name` → `display_name`, `assigned_user_tcno` → `national_id`,
`assigned_email` → `mail` + `user_principal_name`. `ad_object_id` boş (AD bağı
yok) — uygulamadan tekrar eşleştirebilirsin. Eski uygulamada "iade" durumu
olmadığı için hepsi **Active**. "… yerine atandı" ifadeleri `note`'ta korunur;
devir zinciri (`replaces_assignment_id`) bundan sonra uygulamadaki **Devret**
aksiyonundan işler.

### Talepler ← `license_requests`
`source`: **1 → OfficialLetter**, **2 → CorporateRequestSystem**. `status`:
**1 → Pending**, **2 → Draft**, **3 → Fulfilled**, **4 → InReview**. Eski veride
talep eden birim yok → `requester_unit_display_name = "Bilinmiyor (içe aktarıldı)"`.

### Talep kalemleri ← `license_request_items` (**birleştirilir**)
Eski sistem "1 satır = 1 kişi" tutmuş. ITAdmin'de `(request_id, product_id)`
**UNIQUE**. Bu yüzden `(talep, ürün)` bazında gruplanır, adetler toplanır.
54 legacy satır → 28 kalem.

### Talep kalemi kullanıcıları ← `license_request_users`
Birleştirilen kaleme bağlanır. `ad_object_id = 'legacy:<tcno>'`.

## Sınırlamalar

- Maliyet/tutar eski export'ta yok — tüm tutar alanları NULL.
- Talep eden birim placeholder; gerçek birimi elle güncellemen gerekir.
- Devir zinciri geçmişe dönük kurulmaz; metin notu korunur.

## Doğrulanmış

Üretilen SQL, gerçek ITAdmin şeması yüklü bir PostgreSQL 17 üzerinde uçtan uca
test edildi: 1 kategori, 50 ürün, 11 firma, 3 satın alma, 39 paket, 190 zimmet,
17 talep, 28 (birleştirilmiş) kalem, 54 kullanıcı — 0 kısıt hatası, COMMIT
başarılı, çift-çalıştırma koruması ve `99_rollback.sql` çalışıyor.
