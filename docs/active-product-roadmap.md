# Kahve — aktif ürün ve tasarım yol haritası

**Karar tarihi:** 19 Ekim 2026

**Durum:** Planlama tamamlandı; aşağıdaki yeni geliştirme paketleri henüz uygulanmadı.

**Hedef:** QR ile açılan gel-al menüsünü canlı, sade, premium ve telefonda rahat kullanılan bir ürüne dönüştürmek; ardından tek şubelik kontrollü pilota hazırlamak.

Bu belge bundan sonraki iş sırasının ana kaynağıdır. Kullanıcının sağladığı `kahve_menu_ui_ux_analiz_ve_yol_haritasi_2026-10-19.md` raporu güncel kaynak kodla karşılaştırılarak uyarlanmıştır. Rapordaki M01–M18 numaraları izlenebilirlik için korunur. Raporun sonundaki örnek uygulama komutları bu planlama görevinin talimatı olarak yürütülmedi. Bu çalışmada uygulama kodu, veri modeli ve çalışan ortam değiştirilmedi.

[Önceki deneyim raporu](product-experience-roadmap-2026-10-19.md) başlangıç bulguları ve ilk iki teslimin tarihsel kaydıdır. [Müzik akışı](music.md) ve [ödeme durumları](payment-state-machine.md) kendi alanlarının teknik açıklamalarını korur; yeni öncelikler bu belgeden takip edilir.

## 1. Başlangıç noktası ve kanıt sınırı

Yerel MVP; menü, ürün seçenekleri, sepet, sunucu fiyat doğrulaması, iyzico sandbox, personel panosu, takip ve işletme yönetimini içeriyor. 24 temsili ürün fotoğrafı ve 6 kategori mevcut. Gerçek ödeme alan üretim hizmeti ve gerçek işletme pilotu tamamlanmış değil.

`99f8556` sürümünde 102 backend ve 12 frontend testi ile web derlemesi yerelde geçti. Aynı sürümün [GitHub kontrolü](https://github.com/erksbars/kahve-self-servis/actions/runs/39245924393) başarılı. Bu sonuçlar yeni tasarım paketlerinin doğrulandığı anlamına gelmez. Bu incelemede yeni cihaz testi, performans ölçümü veya dönüşüm ölçümü yapılmadı.

**Zaten tamamlanan ve korunacaklar:** şubeye doğru logo dönüşü; şubeye bağlı sepet/şarkı taslağı; doğrulanmış ödemede temizlik; kendi siparişinde şarkı metni ve kararını görme; takipte son doğrulama/eski veri uyarısı; tek ana ödeme eylemi ve toplam değişiminde onay; istek panelinde sunucu filtresi, sayfalama ve bekleyen sayacı. Bunlar yeniden yapılacak özellikler değildir. Demo/canlı ayrımı ise yalnız kısmen tamamlandı.

Kaynakta doğrulanan davranışlar ile test edilmesi gereken riskler ayrılır. Örneğin sepet içi toplam sınırı eksikliği doğrulandı; iOS klavyesinin düğmeyi örtmesi henüz yeniden üretilmiş bir hata değildir. Renk ve yerleşim kararları tasarım yönüdür; tek başlarına dönüşüm artışı kanıtı değildir.

## 2. Sabit ürün kararları

| Konu | Bu yol haritasının kararı |
|---|---|
| Temel hizmet | Hesapsız QR menü → kişiselleştirme → gel-al sipariş → ödeme → takip → teslim |
| Görsel yön | Sıcak krem, koyu kahve metin, terracotta ana eylem, zeytin seçili/ikincil vurgu; fotoğraf ve tipografi ön planda |
| Mobil üst alan | **Kısa işletme/şube bandı + doğrudan ürün keşfi.** Büyük hero başlangıç tasarımından çıkar; küçük kapak daha sonraki kimlik paketinde opsiyonel |
| Marka | Müşteride işletme adı ve şube adı baskın. İlk pakette mevcut `merchant`/şube verisi kullanılır; logo alanı varmış gibi davranılmaz |
| Personel | Müşteri menüsü, ödeme ve takip üst alanında personel bağlantısı görünmez. `/staff`, `/admin` ve davet erişimi korunur |
| Ürün detayı | Modal kalır. Sepet düzenlemede üst üste modal açılmaz; aynı dialog içinde kontrollü görünüm ve geri dönüş kullanılır |
| Birincil eylem | Menüde ürün seçimi, üründe sepete ekleme, sepette **Ödemeye devam**. Normal fiyat kontrolü ayrı zorunlu adım değildir |
| Şarkı | Siparişte isteğe bağlı, en fazla 240 karakter metin. Küçük açılır alan; mevcut taslak varsa metnin varlığı görünür. Paid sonrası işletmeye iletilir, çalınma sözü verilmez |
| Kişiselleştirme | Çekirdek deneyimden sonra logo, tek kapak ve sınırlı hazır vurgu paleti. Serbest font/CSS/layout editörü bu teslimde yok |
| Abonelik | İptal; paket, faturalama, deneme süresi veya abonelik ekranı planlanmaz |
| Diğer kapsam sınırı | Dış müzik kataloğu/oynatıcı/senkronizasyon, zorunlu üyelik, sadakat, oyun, kupon motoru, teslimat ve masa servisi eklenmez |

Renkler müşteri alanına uygulanır; personel panosunun operasyonel yoğunluğu bu görsel çalışmayla yeniden tasarlanmaz. Sahte popülerlik, geri sayım ve işletmenin doğrulamadığı içerik iddiaları kullanılmaz.

## 3. Hedef ekran ve etkileşim sözleşmesi

### Menü ve keşif

- Üst sıra: işletme → şube → gel-al bağlamı → gerçek sipariş kabul durumu. Logo yoksa metin fallback. Yanlış/henüz doğrulanmamış şube tahmin edilmez.
- Arama hemen erişilir. Yazmaya başlanınca kategori **Tümü** olur ve bütün menü aranır. Arama sırasında kategori seçmek aramayı temizler ve seçilen kategoriyi açar. `×` aramayı temizler; **Tüm ürünleri göster** hem arama hem kategoriyi sıfırlar. Türkçe harf davranışı korunur; ilk sürümde bulanık arama veya yeni katalog API'si gerekmez.
- Menü boş, kategori boş, arama sonucu yok ve menü yüklenemedi durumları ayrı metin ve uygun eylem alır. Ağ hatası boş menü gibi gösterilmez; hata sepeti silmez.
- Kategori şeridi yatay kayar, seçili öğe görünür kalır. Pasif kategoriler nötr, seçili durum tek belirgin vurgu alır; gereksiz sayaçlar kaldırılır. Büyük header ile ikinci bir sticky katman yığını oluşmaz.
- 320–767 px başlangıç düzeni tek sütun yatay kart; 768–1099 px iki, 1100+ px üç sütun. Uzun metin ve büyütmede içerik için daha az sütuna düşmek serbesttir. Görsel kutusu telefonda yaklaşık 104–124 px; metni sıkıştırmak için zorunlu sabit ölçüye dönüşmez.
- Kartta fotoğraf → isim → kısa açıklama → **satın alınabilir en düşük varyant fiyatı** → eylem hiyerarşisi. Zorunlu seçenek ücretleri bu alt sınıra dahil edilir; seçilen varyantın ve tahsilatın kesin fiyatı yine sunucudan gelir. Geçerli zorunlu seçim kombinasyonu yoksa fiyat satın alma vaadi oluşturmaz.
- Fotoğraf ve ad semantik düğmeyle aynı ürün detayını açar; iç içe düğme/link ve tıklanabilir sahte `div` olmaz. Kartın tamamını tek dev etkileşim alanına çevirmek gerekmez.
- Fotoğraflar kutuya boşluksuz oturur; `cover` kadrajı anlamlı ürünü kesmemeli. Eksik veya bozuk görselde de detay ve seçim kullanılabilir.

### Ürün seçimi ve sepet

- Seçenek gruplarında zorunluluk, sınır, ek fiyat ve kullanılamama nedeni görünür. İsteğe bağlı tek seçim grubunda **Yok / Seçimi kaldır** mümkündür. Menü yenilenince artık bulunmayan seçim sessizce başka seçeneğe dönüşmez.
- Sepet satırında ürün, seçenekler, adet, ara toplam, **Düzenle** ve **Kaldır** bulunur. Düzenleme mevcut adet/seçeneklerle açılır; iptal eski satırı korur. Kaydetme tek atomik değişimdir.
- Aynı ürün + aynı seçeneklere dönüşen satırlar birleşir. Sonuç 10/satır, 20 farklı satır veya 50 toplam adedi aşarsa değişiklik uygulanmaz; mevcut sepet korunur ve açık hata gösterilir. Sessiz kırpma, otomatik bölme veya önce silip sonra ekleme yoktur.
- Önceden oluşmuş 50+ sepet sessizce boşaltılmaz. Düzeltilebilir uyarı, azaltma/silme ve güncel sınır bilgisi sunulur. `70 → 69 → 50` gibi azaltan ara adımlar 50 üstünde de kabul edilir; bu sırada yeni ekleme, artırma ve ödeme kapalıdır. Diğer yapısal olarak bozuk yerel veriler mevcut güvenli yükleme davranışıyla ele alınır.
- Aynı ürünün farklı varyantlarını sırayla düzenlerken önceki modalın state'i taşınmaz. Kaydet/iptal sonrasında sepet ve ilgili düğmeye odak geri gelir.
- Sepetin alt alanında teslim şubesi, toplam, kısa şarkı özeti ve ana ödeme eylemi vardır. Normal quote tüm kalemleri ikinci kez uzun paragrafta anlatmaz. Manuel yeniden kontrol küçük ikincil eylem olabilir.
- Onaylanan görünür sepet snapshot'ı ile sunucu quote'u **kalem bazında** karşılaştırılır. Aynı toplamda birbirini götüren fiyat değişimleri de gösterilip onaylanır. Eşleme yalnız ürün ID ile değil, ürün + sıralanmış seçenek ID'leriyle yapılır. Değişiklik yoksa normal akışa ek onay ekranı konmaz.
- Ödeme başlatılırken sepet/şarkı kilidi ve aynı işlem anahtarıyla güvenli tekrar korunur. Belirsiz sonuçta ikinci ödeme açtıran eylem gösterilmez. Ekranın sadeleşmesi ödeme durum makinesinin yeniden yazılması değildir.

### Satın alınabilirlik ve hata durumları

| Durum | Müşteri davranışı |
|---|---|
| Açık şube + hazır ödeme + uygun ürün/seçenek | Detay, seçim, sepet ve ödeme açık |
| Ürün tükendi veya zorunlu seçenekler tamamlanamıyor | Detay incelenebilir; neden görünür; ekleme kapalı, bekleniyor görünümü yok |
| Şube sipariş almıyor | Menü ve detay incelenebilir; yeni sipariş/ekleme kapalı, mevcut sepet korunur. Sebep bilinmiyorsa genel mesaj kullanılır |
| Yalnız online ödeme kullanılamıyor | Menü ve taslak sepet erişilir; ödeme başlamaz. Ödeme hizmetinin kapalı olduğu baştan söylenir; alternatif ödeme varmış gibi sunulmaz |
| Güncel katalogda ürün/seçenek kaldırıldı | Satır açıklaması ve kaldırma/düzeltme yolu korunur; kendiliğinden başka ürün/seçenek seçilmez |
| API/ağ/CSRF isteği bağlantı hatası | Anlaşılır hata + tekrar dene; sepet korunur. Doğrulanmamış işlem başarılı gösterilmez |
| Ödeme sonucu Unknown/Review | Mevcut işlemi kontrol et ve işletmenin doğrulanmış destek yolu; güvenli sonuç gelmeden yeni tahsilat önerilmez |

Menü şu an çalışma saati dışı ile manuel durdurmayı tek boolean'a indiriyor. Bu nedenle ilk UI paketinde yalnız doğrulanabilen genel kapalı mesajı kullanılacak. Ayrıntılı neden/sonraki açılış bilgisi küçük DTO paketi olmadan uydurulmayacak.

## 4. Uygulama sırası ve tamamlanma kapıları

Çaba: **S** küçük, **M** orta, **L** birden fazla bileşen/veri katmanı; takvim veya kesin süre taahhüdü değildir. Her paket küçük, incelenebilir commit/PR dilimlerine ayrılır. Aşağıdaki sıra ana yoldur; isteğe bağlı özellikler ana yolu bloke etmez.

| Sıra | Paket / rapor karşılığı | Somut çıktı | Bağımlılık ve çıkış ölçütü |
|---|---|---|---|
| **K0** | Başlangıcı sabitle / Faz 0 · S–M | Mevcut sürümde 320/390/768/1440 referansları, akış/test başlangıcı, CSS envanteri ve örnek veri durumları | Yeni koddan önce. Şube, fiyat değişimi, ödeme tekrarı, şarkı ve takip regresyonları kayıtlı; ölçülmemiş noktalar açık |
| **K1** | Sepet bütünlüğü / M08 · M | Ekleme, adet değiştirme ve yükleme için ortak sınır sözleşmesi; eski aşılmış sepetin toparlanması. K4 düzenlemesinin de kullanacağı doğrulama temeli; düzenleme arayüzü bu pakette değil | K0. Geçerli sepeti 10/satır–20 satır–50 toplam üstüne çıkaran işlem reddedilir; eski limit üstü kayıt adım adım azaltılabilir, veri kaybolmaz. Görsel cilayı beklemez |
| **K2** | Ürün öncelikli ilk ekran / Faz 1 · M | İşletme/şube bandı, müşteri-personel ayrımı, büyük hero yerine kompakt giriş, arama ve kategori temizleme, sayaç azaltma | K0–K1. Normal açık şubede 390×844'te arama/kategori ve ilk ürün adı/fiyatı/eylemi kaydırmadan erişilir. 320 px taşmaz, şube dönüşü korunur |
| **K3** | Kartlar ve görsel sistem / Faz 2 · M | Okunabilir tipografi, foto/ad ile detay, doğru minimum fiyat/uygunluk, nötr kategori dili, müşteri token'ları | K2. Uzun isim, zorunlu ücretli seçenek, eksik görsel, tükendi ve kapalı şube örnekleri geçer. Çalışan staff/admin stilleri bozulmaz |
| **K4** | Ürün ve sepet ergonomisi / Faz 3 · L | Önseçimli düzenleme, atomik birleşme, seçimden vazgeçme, kompakt şarkı, kalem fiyat farkları, kontrollü modal dönüşü | K1–K3. Kaydet/iptal ve çakışma veri kaybettirmez; aynı toplamda değişen kalem de onaylanır; eski seçenekler sessizce değiştirilmez; çift ödeme koruması geçer |
| **K5** | Durumlar, erişilebilirlik, gerçek cihaz / Faz 4 · M–L | Tipli hata/ilgili satır sözleşmesi, gerekli şube durum alanları, ağ hataları, modal/klavye/safe-area, performans ölçümü ve gerekli optimizasyon | K2–K4. Gerçek iOS Safari ve Android Chrome senaryoları, klavye/zoom ve hata toparlanması kayıtlı. Şüpheli riskler kanıtla kapanır; ölçümsüz optimizasyon eklenmez |
| **K6** | Hafif işletme kimliği / Faz 5 · M–L, isteğe bağlı | Owner kontrollü logo, opsiyonel tek kapak, sınırlandırılmış hazır vurgu paleti; önizleme ve varsayılana dön | K3–K5. Tenant/sürüm/yükleme kontrolleri, logosuz/kapaksız/uzun isim fallback. Çekirdek pilotun zorunlu koşulu değil; tam editöre genişlemez |
| **R** | Yayın ve ödeme operasyonu · L | Gerçek mod ayrımı, kalıcı HTTPS/callback, kalıcı veri/anahtarlar, yedek/geri yükleme, hata izleme, belirsiz ödeme ve iade/iptal prosedürü | Tasarımı etkilemeyen planlama K0'dan başlayabilir; canlıya geçiş K5 ve aşağıdaki yayın kapısına bağlı. K6'yı beklemez |
| **K7** | Tek şubelik pilot ve karar / Faz 6'nın başlangıcı · M | 5–8 kişiyle görev testi, ardından sınırlı operasyon pilotu; ölçüm ve sorun öncelik listesi | Test oturumu K5 sonrası sandbox'ta yapılabilir. Gerçek ücretli pilot yalnız R tamamlanınca. Yeni özellikler kullanım verisiyle seçilir |

**İlk geliştirme talebindeki başlangıç:** K0 → K1. Ardından K2 → K3 → K4 → K5. K6 ayrı, sınırlı bir özellik paketidir. Tasarımın tamamlandığı sürüm K5; gerçek pilot için ayrıca R gerekir. Bu planlama turu kendiliğinden K0/K1 kodlamasını başlatmaz.

## 5. Kaynak raporla izlenebilirlik ve ek bulgular

| Kaynak | Karar / paket |
|---|---|
| M01, M02, M03, M13 | K2: kısa band, mevcut işletme adı, personel ayrımı, sayaç azaltma. Logo/kapak kısmı K6 |
| M04, M05 | K3: okunaklı metinler ve semantik ürün açma |
| M06, M17 | K2: global arama, temizleme, bağlama uygun boş durum |
| M07 | K4: atomik sepet düzenleme ve birleşme |
| M08 | **K1'e öne çekildi**; yalnız sayı input'u değil yerel sepetten yükleme de kapsanır |
| M09, M10 | K3: disabled ile yükleniyor ayrımı ve doğru uygunluk; ayrıntılı kapalı neden DTO'su K5 |
| M11 | K4: mevcut tek CTA korunur, şarkı/quote küçülür; kalem farkları görünür |
| M12, M18 | Her pakette temel modal/focus kontrolü; K5'te gerçek cihaz, klavye ve görsel fallback kapanışı |
| M14, M16 | K3'ten itibaren kademeli token/CSS konsolidasyonu; toplu yeniden yazım yok |
| M15 | K5: ölçüm sonrası font/görsel optimizasyonu |
| Ek E01 | K1: `readCart` mevcut toplam 50 üstünü ayrıca yakalamıyor; sepeti silmeyen toparlanma |
| Ek E02 | K4: `minSelect=0,maxSelect=1` için seçimden vazgeçme |
| Ek E03 | K3: taban fiyat yerine minimum geçerli varyant fiyatı; tamamlanamayan zorunlu seçeneklerin uygunluk etkisi |
| Ek E04 | K4: toplam değişmese bile kalem fiyatı değişimi için görünür onay |
| Ek E05 | K4: katalog yenilenmesi ve aynı ürünün farklı satırlarında modal state/selection doğruluğu |
| Ek E06 | K5: API hata `code` bilgisini istemcide koruma ve yapılandırılmış satır/option kimliği; CSRF ağ hatasının da tutarlı iletişimi |

E03'te minimum fiyat ilk aşamada mevcut menü seçeneklerinden türetilebilir; sunucu quote'u son otorite kalır. İleride seçenek bağımlılıkları eklenirse hesap sözleşmesi sunucuya taşınarak yeniden değerlendirilir. E06 ile ayrıntılı kapalı neden, rapordaki “çoğu UI işi” ifadesine istisnadır: küçük API sözleşmeleri gerekir, ödeme sağlayıcısının yeniden yazılması gerekmez.

## 6. Görsel sistemin uygulanacak sınırları

- Başlangıç paleti: zemin `#FAF8F3`, kart `#FFFFFF`, yumuşak alan `#F0E9DE`, ana metin `#332B25`, ikincil metin `#665F56`, sıcak vurgu `#A94325`, zeytin `#384934`. Bunlar hedef tasarım değerleri; her gerçek kullanım çifti kontrast kontrolünden geçer.
- Mevcut font ailesi korunur. Ürün adı yaklaşık 17–19 px, açıklama 14 px, yardımcı metin en az 12 px; telefon form alanları yaklaşık 16 px hedeflenir. Daha küçük yazıyla kartı sığdırma yapılmaz. İsim/fiyat/eylem kesilmez; açıklama kartta iki satır, detayda tam metin.
- Ana düğmeler, adet kontrolü ve modal kapatma için ürün hedefi en az 44×44 CSS px dokunma alanıdır. Bu ürün standardı, WCAG AA'nın her durumda zorunlu ölçüsü olarak sunulmaz; [W3C hedef boyutu açıklaması](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) istisnalarıyla ayrı değerlendirilir.
- Normal metinde en az 4,5:1, büyük metinde 3:1 kontrast kontrolü yapılır. Kaynak: [W3C metin kontrastı](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html). Seçim, stok ve hata yalnız renkle anlatılmaz. Bu kontroller tek başına tam erişilebilirlik uygunluğu iddiası değildir.
- Odak görünürdür; açılan modalda odak, Esc, Tab/Shift+Tab ve açan öğeye dönüş doğrulanır. Ürün düzenleme çıkışında sepetin odağı ve konumu korunur. Eksik görsel fallback'inin erişilebilir anlamı ayrıca kontrol edilir.
- Hover/basılma yaklaşık 120–220 ms, sade ekleme geri bildirimi; azaltılmış harekete uyum. Otomatik carousel, video, intro ve kritik metni geciktiren animasyon yoktur.
- CSS token'ları ve müşteri bileşenleri, dokunulan pakette kademeli ayrıştırılır. `CustomerMenu` ve tüm CSS tek seferde yeniden yazılmaz; sessionStorage/checkout sözleşmeleri sırf isim temizliği için değiştirilmez.

## 7. Ek özellikler için seçilmiş sıra

Yeni fikirlerin ana akışa katkısı, veri gereksinimi ve bakım yükü değerlendirilir. Liste, hepsini ekleme sözü değildir.

| Fikir | Karar ve zaman | Tamamlanma / açılma koşulu |
|---|---|---|
| Net teslim noktası + işletme iletişimi | **Çekirdeğe al** | K2'de mevcut doğrulanmış adres/şube kullanılır; yeni iletişim alanı R'de veri kaynağı ve güncelliğiyle tamamlanır. Uydurma destek adresi yok |
| Kalıcı küçük ekleme onayı / sepet satırına ulaşma | **K3–K4'e al** | Erişilebilir kısa bildirim; zorunlu popup veya otomatik sepet açma yok |
| Manuel durdurma / saat dışında ayrımı | **K5'e al** | Sunucunun gerçek durum alanları; bilinmeyen yeniden açılış saati yazılmaz |
| Logo, tek kapak, sınırlı palet | **K6'da seçici olarak ekle** | Owner verisi, güvenli yükleme, yetki, fallback; abonelik veya serbest tasarım editörü gerektirmez |
| İçerik/alerjen açıklaması | **Pilot içeriğine bağlı ayrı veri paketi** | İşletmenin doğruladığı bilgi ve güncelleme sorumlusu gerekir. Boş bilgi “alerjen yok” değildir; kaynaksız vegan/glutensiz rozeti gösterilmez |
| İşletmenin seçtiği öne çıkan ürünler/sıralama | **Pilot sonrası ilk aday** | Açık Owner kontrolü; “İşletmenin seçimi” gibi doğru başlık, satış verisiz “çok satıyor” yok |
| Aynı siparişi yeniden hazırla | **Pilot sonrası ikinci aday** | Yeni taslak oluşturur; güncel ürün/fiyat/seçenek uygunluğu doğrulanır, otomatik tahsilat veya eski fiyat yok |
| Hesapsız favoriler | **Tekrar gelen müşteri ihtiyacı doğrulanırsa** | Yerel cihaz tercihi; hesap/senkronizasyon projesine dönüşmez |
| Tek tamamlayıcı ürün önerisi | **Sepet başarısı ölçüldükten sonra** | İşletme eşleştirmesi, stok ve fiyat gerçek; tek küçük öneri, zorla açılan satış popup'ı yok |
| PWA / son menüyü görüntüleme | **Pilot sonrası ayrı cache projesi** | Menü güncelliği açık; offline ödeme/sipariş başarı iddiası yok. Ödeme, takip capability'si, personel ve kişisel veriler genel cache'e girmez |
| Hazır bildirimi / bekleme tahmini | **Operasyon verisi ve cihaz ihtiyacı oluşursa** | Bildirim izni/teslim başarısızlığı ve ölçüme dayalı süre ayrı tasarlanır; sahte dakika tahmini gösterilmez |

İlk farklılaştırma odağı; hızlı ve doğru sipariş, güçlü fotoğraf dili, işletme kimliği ve güvenilir geri bildirimdir. Yeni bir sosyal/üyelik veya müzik platformu bu sıraya dahil değildir.

## 8. Ölçüm ve kabul planı

Her pakette ilgili mevcut testler çalışır; yeni davranış için anlamlı test eklenir. Sadece renk/boşluk değişimine uygulamayı tekrar eden yeni test yazmak yerine görsel karşılaştırma yapılır. Ödeme, yetki, sepet sınırı ve fiyat davranışı değişince ilgili otomasyon zorunludur. K5 ve R çıkışında tam regresyon kaydı alınır.

| Kontrol alanı | Asgari kabul |
|---|---|
| Görsel | 320/360/390/430/600/768/1024/1440 px; uzun Türkçe adlar, büyük fiyatlar, görselsiz ürün, 6+ kategori. Yatay sayfa taşması ve dock altında kaybolan son içerik yok |
| Büyütme | 200% metin/zoom denemesi ve ayrıca 320 CSS px yeniden akış. Sabit hero/kart yüksekliği metni kırpmaz; büyütmede ilk ekran ürün hedefinden önce okunabilirlik korunur |
| Mobil ilk ekran | Normal açık şubede 390×844'te arama, kategori ve ilk ürün adı/fiyatı/eylemi görünür. Uzun ad veya gerçek uyarıda bilgiyi gizleyerek hedef tutturulmaz; 320 px'de ilk ürün keşfi ve kaydırma engelsiz |
| Ürün/sepet | 0/1/10 adet, 20 satır, toplam 49/50/51; aynı varyantta birleşme, iptal, eski seçenek, iki farklı satır düzenleme, yenileme; 70→69→50 eski sepet toparlanması ve bu sırada artırma/ekleme/ödeme engeli |
| Fiyat | Zorunlu ücretli seçenek; aynı toplamda değişen iki kalem; artan/azalan fiyat; quote sonrası katalog değişmesi. Görünür fark/onay ve sunucuda checkout doğrulaması korunur |
| Durumlar | Kapalı şube, sadece ödeme kapalı, tükenen ürün, zorunlu seçenek kalmadı, boş arama, API/CSRF ağ hatası; mesaj doğru ve toparlanma yolu kullanılabilir |
| Klavye/cihaz | Tab/Shift+Tab/Enter/Space/Esc; odak geri dönüşü; iOS Safari ve Android Chrome'da klavye, adres çubuğu ve safe-area. Ödeme/şarkı alanına ulaşım örtülmez |
| Ödeme ve izolasyon | Çift dokunma, yanıt kaybı, callback gecikmesi, Unknown/Review, geri tuşu, yanlış tenant/branch/capability. Çift tahsilat, sahte başarı ve yanlış şubeye dönüş yok |
| Şarkı ve takip | Boş/240/241 karakter, açılır alan taslağı, yenileme, Paid öncesi görünmezlik, karar geri bildirimi, eski veri uyarısı ve eski Pending sayfalaması korunur |

### Performans

K0'da ölçüm ortamı ve başlangıç kaydı, K5'te aynı cihaz/ağ/koşullarda önce-sonra kıyası alınır. Soğuk açılış ve etkileşim kaydı; LCP, CLS, arama/ürün/sepet tepki süreleri ve ilk görsellerin ağ trafiğini kapsar. Fontu barındırma veya `srcSet`/thumbnail ekleme kararı dar boğaza göre verilir; toplu ve ölçümsüz optimizasyon yapılmaz.

Saha hedefleri LCP ≤2,5 sn, INP ≤200 ms, CLS ≤0,1; mobil/masaüstü ayrı 75. yüzdelik düzeyinde değerlendirilir. Bunlar ulaşılmış değerler değildir. Tek Lighthouse puanı veya küçük görev testi saha INP/75. yüzdelik başarısını kanıtlamaz. Kaynak: [Google Web Vitals](https://web.dev/articles/vitals).

### Kullanılabilirlik ve analitik

K7 görevleri: QR'dan doğru şubeyi tanı → farklı kategoride ürün ara → boy/süt seç → sepette değiştir → opsiyonel seçimi kaldır → ödeme başlat; ayrıca boş aramadan çıkış ve kapalı şube durumunu anlama. İlk hedef 5–8 kişide gözetimli sandbox denemesidir; bu istatistiksel dönüşüm araştırması değildir. Kritik bir görev engeli varsa ücretli pilot öncesi düzeltilir.

Ölçümler: ilk ürün etkileşimine süre, sepete ekleme ve checkout başlangıcı, sıfır sonuçtan toparlanma, düzenleme başarısı, fiyat/uygunluk reddi, belirsiz ödeme ve çözülme süresi. Dönüşüm artışı yüzdesi başlangıç verisi olmadan taahhüt edilmez.

Olaylar gerekirse `menu_view`, `product_open`, `add_to_cart`, `cart_line_edited`, `checkout_started`, `checkout_price_changed`, `payment_verified`, `checkout_error` ile sınırlandırılır. Doğrulanmış ödeme sunucu olayından sayılır; dönüş URL'sinden değil. Arama sorgusu/şarkı metni, kişi bilgisi, parola, takip/ödeme token'ı ve tam yetki içeren URL analitiğe yazılmaz. Otomatik üçüncü taraf analitik kurulumu bu planda kararlaştırılmadı; sağlayıcı, veri erişimi ve saklama süresi R'de belirlenmeden gönderim yapılmaz.

## 9. Gerçek ücretli pilot için yayın kapısı

Bu kapı tasarımın güzel görünmesinden bağımsızdır. Pilot öncesi işletme/ürün sahibi bilgileri, geliştirme ve operasyon sorumluları atanır; aşağıdaki her madde için kanıt ve sorumlu kaydedilir.

1. **Çalışma modu:** API, müşteri ve personel gerçek demo/sandbox/live durumunu doğru anlatır. Mevcut Development/demo compose dosyası üretim dağıtımı sayılmaz; temsili katalog/personel ve sandbox sonuç seçicileri canlı ortama sızmaz.
2. **Ödeme kabulü:** Her kahveci kendi iyzico hesabına ödeme alır. Canlı hesap kabulü, gerekli alıcı alanları ve anahtarlar gerçek test/sağlayıcı koşullarıyla doğrulanır; sohbet veya Git'e anahtar girilmez.
3. **Kalıcı ortam:** HTTPS alan adı, callback, kalıcı DB/görsel depolama/şifreleme anahtarları, yeniden başlatma ve geri alma planı. Geçici bağlantı ücretli pilotun kalıcı altyapısı olmaz.
4. **Belirsiz işlem ve iade/iptal:** Kim kontrol eder, hangi sipariş/sağlayıcı referansıyla, müşteri ne görür ve destek nasıl ulaşılır belirlenir. Sağlayıcı paneliyle manuel iade tercih edilirse doğrulama ve kayıt eşleştirmesi test edilir; uygulamada olmayan iade durumu varmış gibi sunulmaz. Yeterli prosedür yoksa canlı pilot açılmaz; tam otomatik iade API'si ayrı iş olarak büyütülebilir.
5. **Dayanıklılık:** Yedekten geri yükleme denenir; ödeme/outbox uzlaştırma, hataların fark edilmesi ve yetkili erişim doğrulanır. Hassas metin/token loglanmaz. İki tenant ve personel yetkileri yeniden test edilir.
6. **Gerçek içerik ve işletme:** Gerçek ürün/fiyat/görsel, teslim noktası ve iletişim işletme tarafından onaylanır; çalışma saatleri ve sipariş durdurma yolu denenir. Gerekli işletme metinleri/kayıt ve veri saklama kararlarının sorumlusu atanır; bu belge mevzuat veya sağlayıcı kabulü onayı değildir.
7. **Pilot sınırı:** İlk etap tek şube, sınırlı saat/hacim, destek verecek kişi ve başarısızlıkta sipariş kabulünü kapatma/geri alma adımı belli. Bloke eden bulgu yok ve sandbox görevleri geçmiş olmalı. Sonraki şubeye geçiş gerçek sonuçlarla değerlendirilir.

## 10. Çalışma ve teslim düzeni

Uygulama sonraki kodlama talebinde K0–K1'den başlar. Her paket için kapsam, kabul ölçütü, değişen dosyalar, test/görsel kanıt ve kalan risk kaydı tutulur. Tamamlanan anlamlı işler açıklayıcı commitlerle `erksbars/kahve-self-servis` reposuna gönderilir; PR açıklaması sonuç ve doğrulamayı anlatır. Aktivite grafiği için boş veya yapay bölünmüş commit yapılmaz.

Her paket sonunda uygulama çalışır kalır. Başarısız kontrol kapatılmadan paket tamamlandı sayılmaz; yeni bağımlılık veya kapsam değişimi bu belgede kaydedilir. Erişilebilirlik, hata durumları ve güvenli ödeme korumaları son pakete ertelenmez; K5 bunların cihazlarla kapanışıdır.

Bu yol haritası için üretim tasarımı ve aşama sırası belirlenmiştir. Kalan dış girdiler açıkça R'ye aittir: gerçek işletme içeriği, destek sorumlusu, kalıcı ortam/alan adı, canlı ödeme kabulü ve operasyon kararı. Bunlar çekirdek UI çalışmalarını durdurmaz; tamamlanmadan gerçek ücretli pilot başlatılmaz.
