# Kahve MVP — ürün, tasarım ve deneyim raporu

**Tarih:** 19 Ekim 2026  
**Hedef:** Mobilde hızlı sipariş, ödemede güven, işletmede hatasız operasyon.  
**Kapsam:** Mevcut gel-al MVP'sini iyileştirmek. Abonelik iptal; kapsamlı tasarım editörü, dış şarkı kataloğu, Spotify/YouTube oynatma ve canlı müzik senkronizasyonu bu yol haritasına dahil değil.

### İlk uygulama paketi — 19 Ekim güncellemesi

Uygulandı: B01 şube bağlamlı logo; B03 şubeye bağlı şarkı taslağı ve doğrulanmış ödemede temizlik; B04 capability ile kendi siparişinin istek metni/kararını takip etme; B06 son doğrulama saati ve eski veri uyarısı; B07 tek ana ödeme eylemi, fiyat değişiminde açık onay ve erişilebilir alt eylem alanı; B09 textarea/select odak kapsamı. “Sepeti doğrula” isteğe bağlı ikincil kontrol olarak kaldı, ödeme için artık zorunlu değil. Fiyat ve checkout sunucuda ayrıca doğrulanıyor.

B06 açıklaması düzeltildi: takipte genel hata mesajı zaten vardı; eksik olan, eldeki durumun son doğrulanmış veri olduğunu açıkça anlatmaktı. Aşağıdaki tablo başlangıç incelemesinin kaydıdır; bu güncelleme uygulanan maddelerin durumunu belirtir.

B02 kısmen: müşteri sepet/takip demo mesajları demo bilgisine göre koşullandı. API/iyzico hâlâ sandbox aşamasında; canlı ortam modelinin ve personel demo metinlerinin ayrımı tamamlanmış sayılmaz. B05 istek filtre/sayfalama, B08 sayaç, sepette seçenek düzenleme, kompakt hero ve pilot/yayın işleri sırada.

### İkinci uygulama paketi — istek paneli

B05 ve B08 uygulandı: sunucuda durum filtresi, 20 kayıtlık sayfalar, filtre/sayfadan bağımsız bekleyen sayısı, personel panosunun üstünde sayaçlı kısayol ve karar sonrası onay mesajı. Eski Pending kayıtlar yeni işlenmiş kayıtların arkasında kaybolmaz; son sayfa boşalınca sunucu son geçerli sayfayı döndürür. Güncel API/arayüz ayrıntısı [müzik akışında](music.md). Yukarıdaki ilk paket kalan listesi tarihsel durumdur; bu güncelleme B05/B08'i kapatır.

## 1. Genel değerlendirme

Ürün, uçtan uca sipariş alınabilen bir yerel demo seviyesinde. Fotoğraflı menü, ürün seçenekleri, sepet, iyzico sandbox, barista panosu, takip ve işletme yönetimi mevcut. Şarkı isteği sipariş sırasında metin olarak alınıyor; ödeme doğrulanınca yetkili işletme/personel ekranına düşüyor.

Öncelik ürün sayısını veya ekran sayısını artırmak değil; doğru şubede kalmayı, siparişin kolay verilmesini ve hata olduğunda müşterinin ne yapacağını netleştirmek. Mevcut sıcak krem/kayısı/zeytin görsel dili korunmalı. Premium hissi, daha fazla animasyon yerine tutarlı tipografi, daha iyi görsel hiyerarşi ve güvenilir durum mesajlarıyla güçlendirilmeli.

**Yayına hazır değerlendirmesi yapılmadı.** Canlı ödeme, kalıcı HTTPS, iade/uzlaştırma, yedekleme ve gerçek cihaz pilotu ayrıca tamamlanmalı. Tamamlanma yüzdesi vermek bu aşamada yanıltıcı olur.

### İncelemenin dayanağı

- Mevcut müşteri menüsü tarayıcıda görsel olarak incelendi; kaynak kodunda menü, ürün, sepet, ödeme, takip, personel ve istek paneli akışları okundu.
- Önceki uygulama dilimindeki mobil sepet/istek paneli görüntüleri ve test kapsamı referans alındı. Son kayıt: **101 backend ve 28 tarayıcı testi başarılı**. Bu rapor hazırlanırken tüm testler tekrar çalıştırılmadı.
- Bu çalışma kullanılabilirlik araştırması, penetrasyon testi veya erişilebilirlik uygunluk sertifikası değildir. Gerçek müşteri oturumu, yoğun saat operasyonu ve sahadaki telefonlar henüz ölçülmedi.
- Aşağıda **doğrulanmış** kaynak/UI bulguları ile **test edilmesi gereken riskler** ayrı belirtilir. Tasarım tercihleri hata olarak sunulmaz.

## 2. Güçlü taraflar: korunacaklar

| Alan | Mevcut değer | Değişiklik sırasında korunacak davranış |
|---|---|---|
| Menü | Tutarlı ürün fotoğrafları, 6 kategori/24 demo ürün, belirgin fiyat | Görsel yüklenmese de ürün seçilebilir kalmalı |
| Mobil | Kategori kaydırma, küçük ekranda ürün kartları, sepet erişimi | Dar ekranda yatay sayfa taşması olmamalı |
| Seçenekler | Boy/süt/ekstra, zorunlu ve maksimum seçim kuralları | Ürüne uygun olmayan seçenek grubu çıkmamalı |
| Fiyat | Sunucudan quote, checkout'ta tekrar kontrol | İstemci fiyatına güvenilmemeli; eski fiyat sessizce tahsil edilmemeli |
| Ödeme | Tekrarlı işlem koruması, sunucu doğrulaması, inceleme/belirsiz sonuç durumları | Belirsiz sonuçta müşteriye ikinci ödeme başlatılmamalı |
| Operasyon | Yeni/Hazırlanıyor/Hazır, canlı güncelleme ve polling | Bağlantı yokken işlem tamamlandı gibi görünmemeli |
| Yetki | Tenant/şube sınırı, Owner/Barista rolleri, sürüm kontrolü | Başka işletme veya yetkisiz şube kaydı açılmamalı |
| İstek parçalar | İsteğe bağlı metin; yalnızca Paid siparişler; sipariş numarası | Müzik oynatma sözü verilmemeli; boş alan siparişi engellememeli |

## 3. Doğrulanmış bulgular ve eksikler

Öncelik tanımı: **P0** canlıya çıkmadan çözülmeli; **P1** pilot öncesi ana deneyim; **P2** pilot verisiyle geliştirme. Öncelik bir güvenlik açığı iddiası değildir.

| ID | Öncelik | Bulgu ve kanıt | Kullanıcıya etkisi | Öneri / kabul koşulu |
|---|---|---|---|---|
| B01 | P0 | Üst logo bağlantısı `App.tsx / Header` içinde sabit `/b/demo-moda`. Kaynakta doğrulandı. | Başka şubedeki müşteri logoya basınca Moda menüsüne gider. Sunucu izolasyonu mevcut; bu bir gezinme/yanlış şube riski. | Logo mevcut sipariş/menü şubesine dönsün. Beşiktaş → ürün → sepet → logo senaryosunda şube korunmalı. |
| B02 | P0 | Demo/sandbox dili bazı yerlerde koşulsuz: sepet altı demo metni, ödeme düğmesi adları ve Track yanıtındaki `demo = true`. Kaynakta doğrulandı. | Canlıya geçildiğinde yanlış güvence veya kafa karışıklığı doğurabilir. | Demo/canlı bilgisi sunucudaki gerçek çalışma modundan gelsin; canlı modda sandbox kontrolleri ve temsili içerik görünmesin. |
| B03 | P1 | Şarkı metni `CustomerMenu.tsx` içinde yalnızca component state'inde; sepet satırları ise sessionStorage'da. | Sayfa yenilenince ürünler kalırken yazılan istek kaybolur. | Şubeye bağlı taslak olarak koru; ödeme tamamlanınca temizle. Başka şubeye taşınmamalı. |
| B04 | P1 | Şarkı isteği checkout'ta kaydediliyor; mevcut Track DTO/UI'sında istek metni ve karar durumu yok. | Müşteri gönderildiğini veya işletmenin kararını takip edemiyor. | Sipariş özetinde “Şarkı isteğin iletildi”; takipte yalnızca bu siparişin metni/durumu. Kabul, “çalıyor” olarak gösterilmemeli. |
| B05 | P1 | İstek endpoint'i tüm durumlardan son 100 kaydı alıyor; “yalnızca yeni” filtresi istemcide. | Yüksek hacimde daha eski bir Pending istek son 100'ün dışında kalıp görünmeyebilir. Koddan çıkarılan risk; yoğun veriyle yeniden üretim yapılmadı. | Pending filtresi ve sayfalama sunucuda; toplam bekleyen sayısı ayrı. 100 yeni işlenmiş kayıt, eski bekleyen isteği erişilemez yapmamalı. |
| B06 | P1 | Takipte veri varken genel FetchError zaten gösteriliyordu; son doğrulama saati ve eldeki verinin eski olabileceğini anlatan açık mesaj yoktu. | Müşteri hata mesajıyla birlikte gösterilen durumun güncelliğini anlayamayabilir. | “Son doğrulama…” ve açık eski veri uyarısı; son doğrulanmış veri korunmalı. İlk pakette uygulandı. |
| B07 | P1 | Sepette önce “Sepeti doğrula”, sonra ayrı ödeme düğmesi; başarılı quote'ta teknik özet ve yeniden doğrulama düğmesi öne çıkıyor. | Gereksiz karar ve kaydırma; özellikle kısa mobil ekranda ödeme eylemi geride kalıyor. Bu bir kullanılabilirlik bulgusu. | Tek birincil “Ödemeye devam” eylemi; sunucu kontrolü arka planda. Fiyat değişirse farkı açıkça onaylat. |
| B08 | P2 | İstek paneli personel panosunun altında; yeni istek sayacı üst alanda yok. | Yoğun siparişte personel yeni isteği fark etmeyebilir. | “İstekler · 3 yeni” kısayolu; sipariş panosunun önceliğini bozmayan bildirim. |
| B09 | P2 | Genel `focus-visible` stilleri button/a/input kapsıyor; yeni müşteri textarea'sı için aynı kapsam yok. | Tarayıcı varsayılanı kalabilir ama görsel odak dili tutarsız. Klavyeyle erişilemediği iddiası değildir. | Select/textarea dahil tutarlı odak; Tab, Shift+Tab, Escape ve odak geri dönüşü testleri. |
| B10 | P2 | Eski müzik katalog API'leri/rehberleri uyumluluk için korunmuş; bazı eski yol haritası notları artık geçerli değil. | Bakımda kapsam karışıklığı; eski özelliği yanlışlıkla yeniden geliştirme riski. | Aktif doküman tek kaynak olsun; eski içerik açıkça arşivlensin. Mevcut kayıtlar kontrolsüz silinmesin. |

## 4. Tasarım yönü

**Görsel hedef:** Canlı, vurucu ve sade. Sıcak kayısı vurgu, kırık beyaz zemin, koyu zeytin ana eylem, okunaklı koyu metin. Her ekranın en fazla bir baskın eylemi olmalı.

### Menü ve keşif

- Mobilde hero'yu daha kompakt bir sürümle karşılaştır. İlk ekranda ürün keşfine daha erken ulaşılması hedeflensin; mevcut hero'nun satışa etkisi henüz ölçülmedi.
- Şube adı ve teslim türü görünür olsun; personel bağlantısı müşteri menüsüyle yarışmasın.
- Arama ve kategori gezinmesi birlikte çalışsın; aktif kategori/arama temizleme kolay olsun. Sonuç yoksa “Aramayı temizle” eylemi verilsin.
- Kartlarda ürün adı → kısa açıklama → fiyat → eylem sırası sabitlensin. Gereksiz etiket ve dekoratif rozetler azaltılsın.
- Fotoğraf kenar boşlukları geri gelmesin; `cover` kadrajı korunurken ürünün kesilip kesilmediği örneklerle kontrol edilsin.
- “En çok satan” yalnızca gerçek veriyle; “vegan/alerjen” yalnızca işletmenin doğruladığı içerikle gösterilsin.

### Ürün detayı ve sepet

- Seçeneklerde seçili/uygunsuz/zorunlu ayrımı net; ek ücret doğrudan seçenek yanında.
- Mobilde toplam ve ana eylem erişilebilir yerde; klavye açıldığında metin alanı ve düğmeler örtülmemeli. Modalın tüm ekranını sabitlemek yerine kaydırma alanı ve alt eylem birlikte tasarlansın.
- Sepet satırında seçilen boy/süt/ekstra ve birim fiyat anlaşılır gösterilsin. “Düzenle” ile yeniden ürün seçimine dönülebilsin.
- Şarkı alanı ikinci planda, isteğe bağlı ve kısa kalsın; alışverişin önüne geçmesin. Karakter sınırı ve gönderilme anı anlaşılır olsun.
- Başarılı fiyat doğrulamasında uzun teknik satır yerine anlaşılır toplam; değişiklikte eski/yeni tutar ve etkilenen ürünler gösterilsin.

### Ödeme ve takip

- Adımlar: “Sepet → Güvenli ödeme → Sipariş takibi”. Yeni ekran eklendikçe ilerleme geriye gidiyor gibi hissettirmesin.
- Alıcı formunda sağlayıcının gerçekten gerektirdiği alanlar doğrulansın. Kimlik/adres alanları doğrulanmadan kaldırılamaz; gereksiz bilgi de istenmemeli. Alan bazlı hata, doğru klavye ve otomatik doldurma iyileştirilsin.
- Bekleme, başarısızlık, inceleme ve belirsiz sonuç ayrı görseller/metinlerle anlatılsın. “Tekrar ödeme yapma” uyarısının yanında güvenli kontrol ve destek adımı bulunsun.
- Takipte sipariş numarası, gerçek durum, ürün özeti ve teslim noktası; ardından şarkı isteği. Doğrulanmamış hazırlanma süresi gösterilmesin.
- Tamamlandı ekranında “Aynı siparişi yeniden hazırla” ileride eklenebilir; güncel fiyat/uygunluk kontrolü zorunlu kalır.

### İşletme ve personel

- Yönetim paneli içerik düzenlemeye, personel panosu hızlı operasyona odaklansın. Personel ekranını rapor kartlarıyla doldurma.
- Sipariş kartlarında numara, geçen süre, ürün/seçenek ve sonraki işlem bir bakışta okunmalı.
- İstek kararından sonra kısa başarı bildirimi verilsin; varsayılan filtre yüzünden kaybolan kart “işlem olmadı” hissi yaratmasın.
- Uzun listelerde durum/şube/tarih filtreleri ve sayfalama; filtre değişince hangi kapsamın görüldüğü açık olsun.
- Temel işletme kimliği (gerçek logo/adres/iletişim) pilot için değerlendirilebilir; Canva benzeri editör veya abonelik akışı bu işin parçası değil.

## 5. Hedef akışlar

### Normal sipariş

QR → doğru işletme/şube → ürünü seç → seçenekler ve toplam → sepete ekle → sepeti düzenle → isteğe bağlı şarkı metni → **Ödemeye devam** → sunucuda fiyat/uygunluk kontrolü → güvenli ödeme → sunucudan doğrulama → sipariş numarası ve canlı takip → teslim.

**Koruma:** Fiyat değişmediyse fazladan doğrulama düğmesi gerekmez. Değiştiyse ödeme başlatılmadan önce açık onay alınır. Aynı checkout tekrarı yeni tahsilat oluşturmaz.

### Fiyat veya bulunurluk değişmesi

Sepet → doğrulama → değişen ürünleri işaretle → güncel toplam / ürün çıkarma / seçenek düzeltme → müşteri onayı → ödeme. Hata bütün sepeti silmemeli.

### Bağlantı veya belirsiz ödeme

Bağlantı kesildi → taslak korunur → “Henüz tamamlanmadı” durumu → bağlantı gelince mevcut işlem sorgulanır → kesin sonuç → takip veya güvenli yeniden deneme. Sunucu sonucu kesinleştirmeden yeni ödeme oturumu açılması önerilmez.

### İstek parçalar

Sepette metin → sipariş/ödeme kaydıyla birlikte saklama → ödeme doğrulandı → personel panelinde sipariş numarası → kabul/reddet → müşterinin sipariş takibinde ilgili karar. Siparişsiz gönderim, dış katalog ve otomatik oynatma yok.

## 6. Olmaması gerekenler

1. Logo/geri dönüş nedeniyle şube değiştirmek; sepet ve işletme kimliğini karıştırmak.
2. Ödeme yönlendirmesinden dönüldü diye sunucu doğrulaması olmadan “ödendi” yazmak.
3. Çift dokunma, yenileme veya bağlantı kesintisinde ikinci tahsilat oluşturmak.
4. Fiyat/ürün değişikliğini gizlemek; sunucu hatasında sepeti silmek.
5. Eski takip verisini güncelmiş gibi sunmak; bağlantı yokken işlem başarılı göstermek.
6. Online ödeme kesinleşmeden siparişi hazırlık kuyruğuna veya şarkı isteğini işletme paneline düşürmek.
7. Şarkı alanını zorunlu yapmak, “kabul edildi”yi “çalıyor” olarak göstermek veya çalınma sözü vermek.
8. Görsel hatası yüzünden ürün seçimini engellemek; fotoğrafın anlamlı kısmını gelişigüzel kırpmak.
9. İşletmenin doğrulamadığı alerjen/vegan veya veriye dayanmayan popülerlik iddiası kullanmak.
10. Kapalı/tükenmiş ürünlerde yalnızca soluk düğme bırakıp nedenini açıklamamak.
11. Uzun formlarda veya modalda ödeme düğmesini klavyenin altında erişilemez bırakmak.
12. Demo kartları/temsili fiyatları gerçek ürün gibi sunmak; canlı sistemde sandbox güvence metni bırakmak.

## 7. Önceliklendirilmiş uygulama yol haritası

Çaba sınıfları **S: küçük**, **M: orta**, **L: çok ekranlı/operasyonel**; takvim taahhüdü değildir. Dilimler kullanıcı tarafından ayrıca başlatıldığında uygulanır. Bu raporda ürün kodu değiştirilmedi.

| Sıra | Dilim | Öncelik / çaba | Bağımlılık | Çıkış ölçütü |
|---|---|---|---|---|
| 1 | Doğru şube ve hata iletişimi: B01, B02'nin UI kapsamı, B06 | P0–P1 / M | Mevcut akış | Tüm dönüşlerde aynı şube; eski veri uyarısı; çalışma moduna uygun metin |
| 2 | Mobil ürün/sepet: kompakt hero, tek ödeme eylemi, sepet satırını düzenleme, erişilebilir alt eylem | P1 / L | 1; mevcut quote/checkout korumaları | 320/390px ve klavye açıkken baştan sona tamamlanabilir; fiyat değişikliği onayı korunur |
| 3 | Şarkı isteğini tamamla: taslak koruma, takipte metin/durum, bekleyen sayacı, sunucuda filtre/sayfalama | P1 / M | Sipariş capability ve mevcut panel | İstek yenilemede kaybolmaz; yalnızca kendi siparişinde görünür; eski Pending kaydı erişilebilir |
| 4 | Operasyon ve dayanıklılık: güncellik göstergesi, offline deneyim, iade/iptal/uzlaştırma prosedürü | P0–P1 / L | 1–3; sağlayıcı/işletme kararları | Bağlantı kesintisi ve belirsiz ödeme senaryoları güvenle kapanır; personelin izleyeceği adım belli |
| 5 | Yayın hazırlığı ve küçük pilot: kalıcı HTTPS, canlı hesap kabulü, kalıcı anahtarlar/depolama, yedek/geri yükleme, izleme, gerçek cihazlar | P0 / L | 4 ve gerekli ödeme operasyonları | Bir şubede gerçek kontrollü pilot; kritik açık yok; geri alma ve destek planı var |
| 6 | Ölçümle büyüt: favori/tekrar sipariş, ürün eşleştirme, basit işletme kimliği | P2 / S–M | Pilot verisi | Ölçülen bir sorunu çözen, fiyat/uygunluk kurallarını koruyan özellik |

İlk uygulanacak paket: **yanlış şubeye yönlenmeyi düzelt → takipte güncellik uyarısı → mobil sepet/tek ana eylem → şarkı taslağı ve takip onayı**. Canlı demo metinleri için mod ayrımı da yayın öncesi kontrol listesinde kapanmalı.

## 8. Eklenebilecek özellikler

| Özellik | Sağladığı değer | Öncelik / koşul |
|---|---|---|
| Sepette ürün seçeneklerini düzenleme | Silip yeniden eklemeyi azaltır | P1; sunucu fiyat kontrolü korunur |
| Şarkı isteği alındı/karar bildirimi | Müşterinin tekrar gönderme ihtiyacını azaltır | P1; yalnızca ilgili sipariş capability'si |
| İşletme iletişim ve teslim noktası | Ödeme/teslim belirsizliğinde yol gösterir | P1; işletmenin doğruladığı bilgiler |
| Doğrulanmış içerik/alerjen alanları | Ürün seçiminde açıklık | P1; boş bilgi “alerjen yok” anlamına gelmez |
| Favoriler ve tekrar sipariş | Düzenli müşterinin işini hızlandırır | P2; hesap şartı getirmeden tasarlanabilir; eski fiyatla ödeme yok |
| İşletmenin seçtiği öne çıkanlar | Menüde keşfi kolaylaştırır | P2; “en çok satan” iddiasından ayrı |
| PWA kurulumu ve son menüyü görüntüleme | Tekrar erişim ve zayıf ağ deneyimi | P1–P2; offline ödeme/sipariş gönderildi iddiası yok; ödeme/token/kişisel veriler kontrolsüz cache'lenmez |
| Kapasiteye dayalı bekleme aralığı | Yoğunluk beklentisini yönetir | P2; gerçek operasyon verisi ve doğrulanmış hesaplama olmadan gösterilmez |
| İsteğe bağlı hazır bildirimi | Ekranı açık tutma ihtiyacını azaltır | P2; izin, teslimat başarısızlığı ve cihaz desteği ayrıca tasarlanır |

Kupon, sadakat, masa servisi ve teslimat ayrı kapsam kararlarıdır; mevcut gel-al akışına farkında olmadan eklenmemeli. Abonelik ve müzik sağlayıcısı entegrasyonları kapsam dışıdır.

## 9. Kabul ve test matrisi

| Alan | Kontrol |
|---|---|
| Görsel | 320/390/768/1440px, uzun ürün adı, eksik görsel, çok seçenek; yatay taşma ve örtülen ana eylem yok |
| Dokunma/klavye | Telefon klavyesi açık, 200% yakınlaştırma, Tab/Shift+Tab/Escape, görünür odak, anlamlı alan etiketleri |
| Menü/sepet | Şube değişimi, boş sepet, 10 adet/20 satır/50 toplam sınırları, tükenen ürün, değişen fiyat, yenileme ve geri dönüş |
| Ödeme | Çift dokunma, yanıt kaybı, geri tuşu, süre dolması, başarısızlık, inceleme, Unknown; çift tahsilat ve sahte başarı yok |
| Takip | Paid→Received gecikmesi, reconnect, API hatası varken eski veri, Ready/Completed, geçersiz capability |
| İstek | Boş/240/241 karakter, yenileme, checkout tekrarı, farklı şarkı aynı key, Paid öncesi görünmezlik, başka sipariş/tenant erişimi, eski Pending ve sayfalama |
| Personel | İki personelin aynı kayda basması, şube yetkisinin kaldırılması, bağlantı kesilmesi, yanlış durum geçişi, filtre sonrası geri bildirim |
| Yayın | Demo/canlı ayrımı, kalıcı HTTPS/callback, uygulama restart'ı, yedekten geri dönme, izleme ve destek prosedürü |

Ödeme/erişim değişikliklerinde anlamlı backend entegrasyon testleri zorunlu; yalnızca renk/boşluk düzeltmelerinde gereksiz test yazmak yerine görsel ve mevcut akış doğrulaması yapılmalı. Otomasyonun yanında gerçek iOS Safari ve Android Chrome pilotu gerekir; mevcut tarayıcı testleri cihaz sahasını tamamen kapsamaz.

## 10. Pilot ölçümleri

Başlangıç verisi yok; aşağıdakiler başarı iddiası değil, ölçülecek göstergeler:

- Menü açılışı → ilk sepete ekleme ve → ödeme başlangıcı süreleri.
- Ürün seçimi, sepet, ödeme başlangıcı ve doğrulanmış ödeme arasındaki terk noktaları.
- Fiyat/uygunluk düzeltmesine düşen sepet oranı; müşterinin düzeltip devam edebilmesi.
- Belirsiz ödeme sayısı, çözülme süresi ve destek ihtiyacı.
- Personelin siparişi fark etme/işleme süresi; yanlış veya yinelenen durum işlemleri.
- Şarkı isteği gönderildiği halde müşteri tarafından tekrar sorulması ve bekleyen isteğin fark edilme süresi.

Hedefler ilk küçük pilotun başlangıç ölçümüyle belirlenmeli. Serbest şarkı metni, alıcı bilgisi, parola, ödeme veya takip token'ı analitik olaylarına yazılmamalı.

## 11. Tamamlandı sayma koşulu

Bir dilim, ekranın güzel görünmesiyle bitmez: normal akış ve hata durumları çalışmalı, mobil/klavye kontrolü yapılmalı, ilgili testler geçmeli, kullanıcıya gösterilen metin gerçek sistem durumuyla tutarlı olmalı. Yayın dilimi ayrıca canlı ödeme kabulü, operasyonel geri dönüş, yedekleme ve pilot kanıtı gerektirir.

**Sonuç:** Mevcut görsel dili koruyup önce şube doğruluğu, ödeme/sepet kolaylığı, hata durumları ve şarkı isteğinin müşteri–işletme geri bildirimini tamamlamak; ardından kontrollü pilotla yeni özellik seçmek en uygun sıra.

### İncelenen ana kaynak dosyalar

`apps/web/src/App.tsx`, `CustomerMenu.tsx`, `ProductPicker.tsx`, `Dialog.tsx`, `Music.tsx`, `StaffBoard.tsx`, `PaymentPages.tsx`, `IyzicoPayment.tsx`, `index.css`, `premium-menu.css`; `services/api/Modules/Payments/CheckoutEndpoints.cs`, `Modules/Music/OrderSongRequests.cs`; `README.md`, `docs/music.md`, önceki teslim kayıtları ve ilgili API/tarayıcı testleri.
