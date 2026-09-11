# ITAdmin Mimari, UI, Kod ve Lisans Yönetimi Nihai İnceleme Raporu

**Tarih:** 11 Eylül 2026  
**Kapsam:** Active Directory Yönetimi ve Lisans Yönetimi modülleri; ortak platform, güvenlik, veri modeli, kullanıcı deneyimi, test, ölçeklenebilirlik ve dağıtım  
**Yöntem:** Dokuz fazda kaynak kod incelemesi, iş kuralı izleme, tehdit ve yetki analizi, veri geçişi kontrolü, otomatik testler ve üretim işletim modelinin değerlendirilmesi

## 1. Yönetici özeti

ITAdmin'ın genel mimarisi anlaşılır, katman sınırları büyük ölçüde doğru ve iki modül de ortak kimlik, yetkilendirme, denetim ve bildirim altyapısını makul biçimde kullanıyor. Active Directory (AD) modülü işlevsel olarak daha olgun; Lisans Yönetimi modülü ise inceleme başlangıcında özellikle talep, koltuk ve yenileme yaşam döngülerinde birbiriyle çelişen kurallara sahipti.

Dokuz faz sonunda Lisans Yönetimi'nin çekirdek kurgusu tutarlı hale getirildi:

- Kullanıcıya atanmış lisans ile miktar esaslı lisans birbirinden ayrıldı.
- `NamedUser` taleplerinde miktarın seçili kullanıcı sayısından türemesi sağlandı.
- Talep karşılama; doğru kullanıcı, uygun lisans türü, yeterli kapasite ve kalan miktar üzerinden doğrulanıyor.
- Kısmi karşılama ve tekrar deneme davranışı güvenli ve idempotent hale getirildi.
- Paket durumları, tarihler ve yenileme zinciri için tek merkezli kurallar getirildi.
- Lisans anahtarları uygulama katmanında maskeleniyor, ayrı yetkiyle okunuyor ve korumalı biçimde saklanıyor.
- Kritik koltuk/paket işlemlerinde PostgreSQL satır kilidi ve işlem sınırı kullanılıyor.
- Liste ekranlarında sessiz ilk-100 kayıt sınırı kaldırıldı; sıralamalar kararlı hale getirildi.
- Yenileme tarihi yaklaşan paketler zamanlanmış worker ile taranıyor ve bildirimler idempotent biçimde kuyruğa alınıyor.
- Gerçek PostgreSQL bağlantılarıyla kapasite yarış testi ve kritik modüller için Playwright smoke testleri CI kalite kapısına eklendi.

**Nihai kanaat:** Sistem, yapılan düzeltmelerle günlük lisans envanteri, tahsis ve yenileme takibi için mantıklı bir çekirdeğe sahip. Otomatik yenileme bildirimi, gerçek PostgreSQL eşzamanlılık testi ve temel tarayıcı smoke kapsamı tamamlandı. Yüksek güvenli üretim kabulü önündeki başlıca dış bağımlılık boşluğu, izole bir gerçek AD/LDAP laboratuvarında mutasyon ve geri yükleme testlerinin bulunmamasıdır.

## 2. Sistem mimarisi

### 2.1 Backend

Backend ASP.NET Core üzerinde şu sorumluluklara ayrılmıştır:

- **Domain:** Varlıklar ve enumlar; AD dış sistem modeli ile lisans envanteri modeli.
- **Application:** Servis sözleşmeleri, ortak modeller, izin kodları ve merkezi yaşam döngüsü kuralları.
- **Persistence:** EF Core/PostgreSQL, sorgular, işlemler, satır kilitleri, veri koruma ve servis uygulamaları.
- **Infrastructure:** Dizin, bildirim ve işletim altyapısı gibi dış bağımlılıklar.
- **API:** HTTP sözleşmeleri, controller'lar, kimlik doğrulama ve izin politikaları.
- **Host Agent / Update Coordinator:** IIS uygulama havuzundan ayrılmış yüksek yetkili sunucu operasyonları.

Bu ayrım genel olarak doğru. Controller'ların çoğu iş kuralını servis katmanına bırakıyor. Yetkilendirme yalnızca UI görünürlüğüne bırakılmamış; API uçlarında da uygulanıyor. Host Agent'ın genel komut çalıştırma yüzeyi yerine dar ve tipli işlemler sunması önemli bir güvenlik artısıdır.

Başlıca mimari risk, uygulama servislerinin bazılarının büyüyerek çok sayıda iş kuralını aynı sınıfta taşımasıdır. Özellikle karşılama servisi, aday doğrulama, satın alma/paket üretme, atama ve talep durumunu ilerletme sorumluluklarını bir arada yürütüyor. İşlem bütünlüğü sağlanmış olsa da ileride “karşılama planı” ve “planı uygulama” adımlarına ayrılması okunabilirliği ve test edilebilirliği artırır.

### 2.2 Frontend

Frontend React/TypeScript tabanlı, özellik klasörleriyle ayrılmış ve rotalar modül bazında tanımlanmıştır. Sunucu verisi React Query, yerel UI durumu ilgili sayfa/bileşenlerde yönetiliyor. Sayfaların tembel yüklenmesi ve izin tabanlı rota/action kontrolü uygun.

Form doğrulamasının bir kısmı ortak saf fonksiyonlara taşındığı için backend kurallarıyla karşılaştırılabilir hale geldi. Bununla birlikte bazı frontend testleri gerçek DOM etkileşimi yerine kaynak kod kalıplarını kontrol ediyor. Bu testler regresyon sinyali verse de kullanıcı davranışını kanıtlamaz.

### 2.3 Üretim ve dağıtım

Üretim modeli IIS + PostgreSQL'dir. Web uygulaması düşük yetkide; dağıtım ve IIS işlemleri LocalSystem çalışan ayrı Host Agent/Coordinator üzerinden yapılır. Bu yetki ayrımı güçlüdür.

Dağıtım doğrudan değişebilir `main` dalından ve üretim sunucusunda derleme yoluyla yapılır. Bu, küçük ve tek sunuculu işletim için bilinçli bir sadelik tercihi olsa da yeniden üretilebilir/sabit artefakt, imzalı sürüm ve kontrollü sürüm yükseltme zinciri sağlamaz. Veritabanı geçişleri ileri yönlüdür; uygulama binary'si geri alınsa bile şema geri alınmaz. Dolayısıyla her migration eski binary ile uyumluluk açısından ayrıca değerlendirilmelidir.

## 3. Active Directory Yönetimi modülü

### 3.1 Kapsam ve akış

Modül; kullanıcı, grup, bilgisayar ve OU yönetimi ile silinmiş nesne/geri yükleme akışlarını kapsar. Liste, detay, oluşturma, düzenleme, grup üyeliği ve OU taşıma rotaları ayrı tutulmuştur. Modül hazırlık durumu ve gerekli izinler, kullanıcı işlemi başlamadan kontrol edilir.

### 3.2 Güçlü yönler

- Nesne türleri ve eylemler için ayrı izin kontrolleri bulunuyor.
- Tehlikeli/geri dönüşü zor eylemler onay adımıyla sunuluyor.
- Aday doğrulama ile kaydedilmiş nesne doğrulamasının ayrılması yanlış pozitifleri azaltıyor.
- Arama, LDAP sayfalama ve limit yaklaşımı büyük dizinler düşünülerek tasarlanmış.
- Başarılı/başarısız operasyon kayıtları işletimsel izlenebilirlik sağlıyor.
- Silinmiş nesne geri yükleme akışı için önkoşul ve hazır olma kontrolleri mevcut.

### 3.3 Kalan riskler

| Öncelik | Bulgu | Etki | Öneri |
|---|---|---|---|
| Yüksek | CI içinde gerçek AD/LDAP ortamına karşı entegrasyon testi yok. | Şema, izin, forest/domain davranışı ve geri yükleme farklılıkları ancak üretimde görülebilir. | İzole test domain'i üzerinde kritik mutasyonlar için gece koşan entegrasyon paketi oluşturun. |
| Orta | AD değişikliği ile portal audit/bildirim kaydı tek atomik işlem olamaz. | AD işlemi başarıp yerel kayıt başarısız olabilir veya tersi görülebilir. | Her operasyona korelasyon kimliği verin; uzlaştırma işi ve “dış sistem sonucu bilinmiyor” durumu ekleyin. |
| Orta | Temel Playwright smoke testi var ancak AD mutasyonları ve geri yükleme uçtan uca çalıştırılmıyor. | Oluşturma, üyelik, OU taşıma ve geri yüklemedeki ortam-bağımlı kırılmalar kaçabilir. | İzole test domain'inde bu mutasyonları kapsayan gece testi ekleyin. |

## 4. Lisans Yönetimi modülü

### 4.1 Önerilen ve uygulanan alan modeli

```text
Firma → Ürün/Kategori
          │
          ▼
       Satın Alma → Lisans Paketi → Koltuk Ataması
                           │
                           └── önceki paket → tek yenileme paketi

Lisans Talebi → Talep Kalemi → Karşılama
                                ├── mevcut paketten ata
                                └── satın alma/paket oluştur ve ata
```

- **Satın alma**, ticari işlemi ve belge/maliyet bağlamını temsil eder.
- **Paket**, kullanılabilir lisans hakkını, türü, kapasiteyi, tarihleri ve anahtarı temsil eder.
- **Koltuk ataması**, yalnızca kullanıcıya atanabilir `NamedUser` paketlerinde gerçek kullanıcı tahsisini temsil eder.
- **Talep**, onay/triage/karşılama sürecidir; envanter kaydının kendisi değildir.
- **Yenileme**, eski paketi değiştirmeden yeni paketi ona bağlar; bir paketin en fazla bir doğrudan yenilemesi olabilir.

Bu ayrım iş alanı açısından mantıklıdır. `Subscription` ve `Perpetual` değerlerinin hem lisans türü hem de tarih/yenileme davranışı çağrıştırması ileride kafa karıştırabilir; bugün doğrulamalar çelişkiyi engelliyor ancak uzun vadede “tahsis modeli” (`NamedUser`, `Concurrent`, `Device`, `Server`, `Site`) ile “ticari süre modeli” (`Subscription`, `Perpetual`, `Trial`, `Free`) iki ayrı alan olmalıdır.

### 4.2 Düzeltilen kurgusal hatalar

1. **Talep miktarı ve kullanıcı listesi çelişkisi:** `NamedUser` miktarı artık kullanıcı sayısından türetiliyor; diğer türlerde kullanıcı taşınmıyor.
2. **Yanlış kullanıcıya karşılama:** Karşılama yalnızca talep kalemindeki kullanıcıları kabul ediyor; zaten karşılanan kullanıcılar tekrar atanamıyor.
3. **Kapasite aşımı:** Mevcut atamalar ve aynı işlemde planlanan atamalar birlikte değerlendirilerek koltuk kapasitesi korunuyor.
4. **Kısmi karşılama:** Talep, tüm kalemler tamamlanmadan tamamlandı durumuna geçirilmiyor; güvenli tekrar deneme mümkün.
5. **Tür uyumsuzluğu:** Kullanıcı koltuğu gerektiren talepler uyumsuz paket türüyle karşılanamıyor.
6. **Durum/tarih tutarsızlığı:** Aktiflik, tarih, süresiz lisans, yenileme gereksinimi ve yenileme tarihi merkezi kurallarla doğrulanıyor.
7. **Çoklu yenileme:** Aynı eski pakete birden fazla yeni paket bağlanması servis ve benzersiz indeks seviyesinde engellendi.
8. **Gizli veri sızıntısı:** Liste ve standart detay cevaplarında anahtar maskeleniyor; açık değer ayrı yetkiye tabi.
9. **Düz metin anahtar:** Mevcut ve yeni anahtarlar Data Protection ile korunuyor; migration çalıştırıcısı eski veriyi geri dolduruyor.
10. **Yetki ayrımı:** Görme, yönetme, talep, karşılama ve hassas veri görme izinleri backend seviyesinde ayrıldı.
11. **Sessiz kayıt kesilmesi:** Form seçeneklerinde ilk 100 kayda bağımlılık kaldırıldı; tüm sayfalar toplanıyor.
12. **Eşzamanlı tahsis yarışı:** Kritik paket/koltuk/karşılama işlemleri satır kilidi ve transaction altında çalışıyor.

### 4.3 UI/UX değerlendirmesi

Olumlu sonuçlar:

- Paket koltuk alanı yalnızca `NamedUser` için gösteriliyor.
- Atama penceresinde tüm kullanıcılar görünür; uygun olmayanlar açıklamalı biçimde devre dışı.
- Sunucu hata mesajları form üzerinde görünür durumda.
- Etiketler, native form gönderimi, klavye davranışı ve maskeli anahtar gösterimi iyileştirildi.
- Talep ve karşılama ekranlarında seçili kullanıcı/miktar ilişkisi kullanıcıya açık biçimde yansıtılıyor.
- Seçenek sorgusu başarısız olduğunda boş listeyle başarı izlenimi verilmiyor.

Kalan UX riski: Tüm sayfaları istemcide toplama yaklaşımı yüzlerce/düşük binlerce kayıt için güvenlidir fakat on binlerce firma, ürün, paket veya kullanıcı olduğunda açılış süresi ve bellek maliyeti büyür. Uzaktan arama ve sanallaştırılmış seçim bileşenleri bir sonraki ölçek adımı olmalıdır.

### 4.4 Güvenlik değerlendirmesi

- Hassas anahtarın ayrı `ViewSensitiveData` iznine bağlanması doğru.
- Liste cevaplarının maskeli olması gereksiz veri yayılımını azaltıyor.
- Koruma anahtarları veritabanıyla birlikte yedeklenmelidir; aksi halde geri yüklenen lisans anahtarları çözülemez.
- Yetkilendirme UI ile sınırlı değil, controller/policy seviyesinde uygulanıyor.

Kalan güvenlik/izlenebilirlik bulgusu: Hassas anahtarı başarıyla görüntüleme eylemi özel bir audit olayı üretmiyor. Yetkisiz denemeler engelleniyor fakat yetkili erişimin kim/tarih/kayıt bağlamında raporlanması uyumluluk için gereklidir.

### 4.5 Otomatik yenileme hatırlatması

`RenewalRequired`, `RenewalDate`, `DefaultRenewalReminderDays` ve alıcı ayarları artık zamanlanmış bir worker tarafından kullanılıyor. Zamanı gelen, iptal/arşiv durumunda olmayan ve henüz yenilenmemiş paketler taranıyor; aktif şablonla üretilen e-postalar bildirim kuyruğuna ekleniyor.

Her paket, yenileme tarihi ve alıcı birleşimi için deterministik korelasyon anahtarı ile kısmi benzersiz PostgreSQL indeksi kullanılıyor; aynı hatırlatma yeniden taramalarda veya eşzamanlı çalışmalarda çoğaltılmıyor. Alıcılar gizlilik için ayrı kuyruk kayıtları olarak oluşturuluyor. Son tarama zamanı, durum, bulunan paket ve kuyruğa alınan e-posta sayısı ayar ekranında görülebiliyor. Gönderim ve yeniden deneme mevcut bildirim outbox worker'ına bırakılıyor.

## 5. Kod kalitesi

### Güçlü noktalar

- Nullable reference types ve tipli DTO'lar yaygın kullanılıyor.
- Backend/frontend enum ve sözleşmeleri testlerle karşılaştırılıyor.
- Merkezi yaşam döngüsü ve izin yardımcıları kural tekrarını azalttı.
- Kritik sorgularda `AsNoTracking`, projeksiyon, kararlı sıralama ve uygun indeksler var.
- Transaction ve kilitler yalnızca ilişkisel sağlayıcıda açılarak birim test sağlayıcısıyla uyum korunuyor.
- Migration'lar elle kimliklendirilmiş, snapshot uyumu doğrulanabiliyor.

### Kalan teknik borç

| Öncelik | Bulgu | Sonuç |
|---|---|---|
| Orta | Firma, ürün, satın alma ve taslak talep düzenlemelerinde optimistic concurrency token yok. | İki editörün son kaydı önceki değişikliği sessizce ezebilir. |
| Orta | Karşılama servisi çok fazla sorumluluk taşıyor. | Yeni karşılama türlerinde hata yüzeyi ve test matrisi büyür. |
| Orta | `ViewReports` izni seed ediliyor fakat çalışan rapor uçları/ekranı yok. | Yönetici rolünde ürünün sunmadığı bir yetki görünür. |
| Düşük/Orta | `%aranan%` biçimli `ILIKE` sorguları için trigram indeks yok. | Büyük tablolarda arama tam taramaya dönebilir. |
| Düşük | `Subscription`/`Perpetual` tahsis türleriyle aynı enumda. | İş kavramları büyüdükçe geçersiz kombinasyon sayısı artar. |

## 6. Test ve kalite kapıları

Faz 9 sonunda tam regresyon sonucu:

- Backend unit: **1256/1256 başarılı**
- Backend integration (gerçek PostgreSQL dahil): **36/36 başarılı**, atlanan test yok
- Frontend unit/source: **1000/1000 başarılı**, atlanan test yok
- Playwright Chromium smoke: **2/2 başarılı**
- Frontend lint: başarılı
- Frontend production build: başarılı
- EF Core model snapshot/migration uyumu: temiz
- Migration SQL üretimi: başarılı

Faz 8 bağımlılık güvenliği:

- npm production dependency audit: **0 güvenlik açığı**
- NuGet doğrudan ve transit paket taraması: **raporlanan güvenlik açığı yok**
- Frontend dependency audit'i CI kalite kapısına eklendi.

Test stratejisindeki boşluklar:

- Gerçek AD/LDAP laboratuvarına karşı otomasyon yok.
- Playwright paketi temel yetki, hazırlık, arama ve sunucu verisi akışlarını kapsıyor; gerçek AD mutasyonları ile Lisans Yönetimi'nin tüm yazma senaryolarını kapsamıyor.

## 7. Migration ve üretime geçiş güvenliği

Yeni migration'lar çoğunlukla eklemeli ve geri uyumlu olsa da şu iki veri varsayımı önemlidir:

1. Eski talep kalemleri `NamedUser` varsayımıyla taşınıyor. Eski sistemin örtük davranışı buysa doğrudur; değilse üretim verisi iş sahibiyle örneklenmelidir.
2. Tek yenileme indeksi kurulmadan önce aynı `previous_package_id` değerini kullanan eski kayıtlar bulunmamalıdır. Migration artık bu durumda açık açıklama ve çözüm ipucuyla durur; veriyi otomatik silmez.

Faz 9'daki temiz PostgreSQL kurulumu, önceki migration'ın oluşturduğu büyük harfli `IX_license_packages_previous_package_id` adı ile sonraki migration'ın kaldırmaya çalıştığı küçük harfli ad arasındaki uyumsuzluğu ortaya çıkardı. Migration iki olası adı güvenli biçimde ele alacak şekilde düzeltildi ve tüm zincir boş veritabanında başarıyla uygulandı.

Üretim öncesi önerilen kontrol:

```sql
SELECT previous_package_id, COUNT(*)
FROM license_packages
WHERE previous_package_id IS NOT NULL
GROUP BY previous_package_id
HAVING COUNT(*) > 1;
```

Beklenen sonuç sıfır satırdır. Satır varsa hangi yenilemenin geçerli olduğuna iş sahibi karar vermeli; otomatik “ilk/son kaydı tut” yaklaşımı finansal ve tarihsel kaydı bozabilir.

Dağıtım kontrol listesi:

1. PostgreSQL yedeğini ve `%ProgramData%\ITAdmin\DataProtection-Keys` dizinini birlikte, geri yükleme testi yapılmış biçimde yedekleyin.
2. Yenileme çakışma sorgusunu çalıştırın ve eski talep kalemlerini örnekleyin.
3. CI'nin ilgili commit için tamamen yeşil olduğunu doğrulayın.
4. Bakım penceresinde `--migrate` adımını çalıştırın; düz metin lisans anahtarı backfill sonucunu kayıtlardan kontrol edin.
5. Hassas veri izni olan ve olmayan iki rolle maskeleme/görüntüleme smoke testi yapın.
6. `NamedUser` tam ve kısmi karşılama, miktar esaslı paket oluşturma, atama bırakma/aktarma ve yenileme smoke testlerini yapın.
7. Binary geri almanın migration'ı geri almadığını işletim ekibine açıkça bildirin.

## 8. Açık bulguların önceliklendirilmesi

### P1 — üretim güveni için sonraki adım

1. AD için izole canlı entegrasyon ortamı kurup kullanıcı/grup/OU mutasyonları ve silinmiş nesne geri yükleme akışını otomatik çalıştırma.
2. Mevcut Playwright smoke paketini kritik yazma akışları ve hata durumlarıyla genişletme.

### P2 — planlı teknik iyileştirme

1. Yetkili lisans anahtarı görüntülemelerini audit'e yazma.
2. Genel düzenlemelere optimistic concurrency/version alanı ekleme.
3. Büyük seçenek listelerinde sunucu taraflı arama ve sanallaştırma.
4. `ViewReports` özelliğini gerçekleştirme veya ürün yüzeyinden kaldırma.
5. Karşılama servisini planlama ve uygulama bileşenlerine ayırma.

### P3 — büyüme oluştuğunda

1. Arama metriklerini ölçüp gerekli tablolarda `pg_trgm`/GIN indeks kullanma.
2. Tahsis modeli ile ticari süre modelini ayrı alanlara ayırma.
3. Sabit, doğrulanabilir ve mümkünse imzalı release artefaktı üretme.

## 9. Sonuç

Lisans Yönetimi'nin temel kurgusu artık mantıklıdır: ticari satın alma, lisans hakkı, kullanıcı tahsisi ve talep süreci birbirinden ayrılmış; tür, kapasite, kullanıcı, durum ve tarih kuralları backend tarafından korunmaktadır. İnceleme başlangıcındaki kritik veri bütünlüğü, güvenlik, eşzamanlılık ve eksik yenileme otomasyonu sorunları giderilmiştir.

Sistem bugün yenileme verisini kaydetmenin yanında zamanı gelen kayıtları otomatik tarar, mükerrer üretimi veritabanı seviyesinde engeller ve bildirim outbox'ına bırakır. Gerçek DB yarış testi ile temel tarayıcı smoke testleri CI'da tekrarlanabilir hale gelmiştir. Kalan en önemli üretim güveni boşluğu Lisans Yönetimi çekirdeğinden çok, gerçek AD/LDAP laboratuvarında doğrulanması gereken dış sistem davranışlarıdır.
