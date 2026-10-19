# Premium menü ve metin tabanlı müzik önerileri yol haritası

> Tarihsel plan. Güncel öncelikler [19 Ekim ürün/deneyim raporundadır](product-experience-roadmap-2026-10-19.md). Abonelik iptal edildi; dış şarkı kataloğu ve müzik oynatma kapsam dışı. Güncel müzik özelliği yalnızca siparişe bağlı metin isteğidir: [akış](music.md). Aşağıdaki eski kapsam maddeleri aktif iş yetkisi değildir.

Güncelleme: 15 Ekim 2026. Görsel altyapısı, 6 kategoride 24 görselli ürün ve responsive müşteri menüsü hazır. PostgreSQL metin şarkı modülünün ilk sürümü uygulandı: demo işletme başına 49 şarkı, şubeye özel havuz, menüden sipariş/ödeme gerektirmeyen öneri ve Owner yönetimi. Güncel giriş noktası “Sıraya bir şarkı bırak” düğmesidir; ilk taslaktaki ödeme koşulu kullanıcı kararıyla kaldırıldı. Takip/ödeme ekranlarının premium tasarım güncellemesi tamamlandı. Katalog içe aktarma/raporlama ve PWA/yayına hazırlık sonraki dilimlerdir. Abonelik ve kapsamlı tasarım özelleştirmesi, kullanıcı kararıyla ayrı sonraki aşamada değerlendirilecek: [not](future-subscription.md).

## Hedef ve mevcut durum

Çalışan sipariş/iyzico sandbox altyapısının üzerine fotoğraflı, çeşitli ve mobilde kolay kullanılan bir kahveci deneyimi kurulacak. Müzik modülü yalnızca şarkı adı/sanatçı metni ve öneri seçimi içerir.

Product ve MenuProduct görsel alanları, fotoğraflı kartlar ve Owner yükleme yönetimi tamamlandı. Tasarım geliştirilirken fiyat doğrulama, sepet, ödeme ve işletme izolasyonu korunur.

## 1. Görsel altyapısı ve ilk ürün koleksiyonu

- Product için yönetilen görsel referansı, alternatif metin ve güvenilir içerik bilgisi ekle; public menü DTO'suna uygun thumbnail URL'si ver.
- Owner panelinden görsel yükleme/değiştirme: tenant yetkisi, dosya boyutu/piksel sınırı, gerçek dosya formatı kontrolü, metadata temizliği ve thumbnail üretimi. Local geliştirme dosya depolaması kalıcı volume üzerinde; production object storage ayrı kurulum.
- Görselleri aynı çekim diliyle hazırla: sıcak doğal ışık, krem taş/ahşap fon, tutarlı kadraj ve renk. Demo için temsili üretilmiş fotoğraflar; gerçek işletme menüsünde işletmenin ürün fotoğrafları.
- İlk koleksiyon: 12 ana üründe fotoğraf; ardından 24 ürünün tamamı. Fotoğrafsız veya yüklenemeyen ürün için tutarlı yedek görünüm. WebP thumbnail, sabit görüntü oranı, ilk ekran dışındaki görsellerde lazy loading.
- Kabul: tenant B, A'nın görselini değiştiremez; hatalı görsel ürünü kullanılamaz yapmaz; mobilde görüntü yüklenirken kart yer değiştirmez; demo görselleri temsili olarak belirtilir.

## 2. Premium müşteri tasarımı

- Yön: sıcak krem, espresso kahvesi, koyu zeytin; sakin tipografi, belirgin fiyatlar, ferah boşluklar. Başlıkta karakterli serif, içerikte okunabilir sans-serif.
- Üst bölümde şube/teslim bilgisi ve ölçülü bir fotoğraf. Mobil ilk ekranda ürünlere hızlı erişim; büyük dekoratif alan siparişi geciktirmesin.
- Fotoğraflı ürün kartı: ad, tek cümle açıklama, başlangıç fiyatı, gerçek içerikten gelen özellik etiketi ve ekleme/seçme eylemi.
- Sabit kategori gezinmesi, genişletilmiş arama, işletmenin seçtiği “Öne çıkanlar”. Satış verisi olmadan “en çok satan” etiketi kullanılmaz.
- Ürün detayında büyük fotoğraf, seçenekler, canlı fiyat özeti; altta erişilebilir sepet alanı. Ödeme ve takip ekranları aynı görsel dilde tamamlanır.
- Kabul: 320/390/1440px taşma yok; dokunma hedefleri ve klavye odağı görünür; mevcut seçenek/sepet/ödeme akışı geçer. Barista panelinde hız ve okunabilirlik önceliği sürer.

## 3. Menüyü 24 ürüne genişletme

| Kategori | Örnek ürünler |
| --- | --- |
| Espresso bazlı — 6 | Espresso, Americano, Latte, Cappuccino, Flat White, Mocha |
| Soğuk kahveler — 4 | Iced Latte, Iced Americano, Cold Brew, Espresso Tonic |
| Filtre ve demleme — 3 | Filtre Kahve, V60, Chemex |
| Kahve dışı — 4 | Matcha Latte, Chai Latte, Sıcak Çikolata, Çay |
| Fırından — 4 | Kruvasan, Bademli Kruvasan, Cookie, Tarçınlı Çörek |
| Tatlılar — 3 | Brownie, San Sebastian, Limonlu Cheesecake |

Fiyatlar demo fiyatı olarak tanımlanır. Süt/boy/ekstra grupları yalnızca uygun içeceklere bağlanır; tatlılar zorunlu kahve seçenekleri almaz. Mevcut DB'ye idempotent demo içerik güncellemesi uygulanır; sadece ilk açılış seed'ini değiştirmek yeterli değildir. İşletmenin düzenlediği ürün/fiyatlar üzerine yazılmaz; geçmiş sipariş snapshot'ları korunur.

Yeni/özel/vegan gibi etiketler yönetici tarafından doğrulanmış içerikten gelir. İçerik ve alerjen bilgileri işletme tarafından girilir/doğrulanır; fotoğraf veya ürün adı üzerinden otomatik doğruluk iddiası yapılmaz. Şube bulunurluğu ve sıralama işletmede kalır.

Kabul: altı kategori ve 24 demo ürün; yeni kategorilerde arama/quote/sepet; uygun seçenek grupları; ikinci çalıştırmada kopya ürün yok; mevcut sipariş geçmişi korunur.

## 4. PostgreSQL şarkı kataloğu ve öneri modülü

İlk sürüm tamamlandı (15 Ekim): 49 başlangıç şarkısı; aksan/Türkçe karakter toleranslı ad/sanatçı araması; her sorguda en fazla 40 sonuç; şubeye özel aktiflik ve modülü aç/kapat; kataloğa elle ekleme; son 100 öneriyi görme ve kabul/reddetme. Gerçek veri API/DB üzerinden gelir. Demo marker 4 tekrar çalışınca işletmenin ayarlarını değiştirmez. İlk taslaktaki 100–200 şarkıya genişletme, CSV içe aktarma, sıralama ve tarih aralıklı öneri sayıları henüz uygulanmadı. Harici metadata kaynağı bağlanmadı.

Önerilen ilk sürüm: işletmenin onayladığı 100–200 şarkıdan oluşan metin kataloğu. Müşteri sipariş takip ekranında “Bu molaya bir şarkı öner” alanından ad/sanatçı arayıp seçer. İlk sürümde ödemesi doğrulanmış sipariş başına bir öneri; öneri gönderimi şubeye ve sipariş capability'sine bağlıdır. Seçim müzik çalınacağı sözü vermez.

- Songs: şarkı adı, sanatçı ve varsa kaynak/recording kimliği. Tür/ruh hali işletmenin kendi etiketleridir.
- BranchSongs: tenant/şube havuzu, aktiflik, sıralama. Havuzdaki parça için yönetici onayı şarttır.
- SongRequests: sipariş, şube, şarkı, zaman ve bekliyor/kabul/reddedildi durumu. Tekil sipariş/şube kısıtı DB'de; rate limit ve CSRF. Ödenmemiş/yabancı sipariş öneri gönderemez.
- Yönetici: modülü aç/kapat, kataloğa ekle/CSV içe aktar, şube havuzunu yönet, önerileri kabul/reddet. Şube ve günlük zaman aralığıyla öneri sayılarını göster.
- Müşteri yalnızca ad/sanatçı ve öneri durumunu görür. İlk sürüm player, ses dosyası, söz, albüm görseli veya Spotify hesabı gerektirmez.
- Müzik yüklemesi ayrı istek/hata sınırı kullanır; hata olduğunda sipariş takip ve ödeme çalışmaya devam eder. Müşteri ekranı bizim DB'den cevap alır.
- Kabul: aynı siparişte tekrar gönderim ikinci kayıt yaratmaz; farklı tenant/şubeye gönderim reddedilir; havuz dışı şarkı seçilemez; modül kapalıyken öneri alınmaz; hata sipariş ekranını bozmaz.

MusicBrainz, yönetici tarafında şarkı adı/sanatçı aramak ve onaylanan kayıtları içeri almak için ikinci adım olabilir. Temel metadata ve kaynak kimliği alınır; ek tür/ruh hali etiketleri bize ait olur. API çağrıları sunucudan, tanımlayıcı User-Agent ve hız sınırıyla yapılır; müşteri ekranı doğrudan dış servise bağlanmaz. Çekirdek veri CC0; ek verilerin lisansı farklıdır. Spotify bu metin tabanlı ilk sürüm için gerekli değildir.

## 5. Sonraki ürün iyileştirmeleri

Sıra: favori ürünler ve aynı seçimi tekrar sepete ekleme; ürün yanında uygun tatlı önerisi; açık fiyat kurallı kahve+tatlı kombinleri; şubeye özel logo/kapak/renk; PWA ve ağ kesintisi davranışları; gerçek ölçümlerle performans/yayına hazırlık. Kombin indirimi sunucu quote/snapshot'a bağlanır. Sadakat/puan ve kupon ayrı sonraki kapsamdır.

## Uygulama sırası ve kontrol noktaları

1. Görsel alanları/yükleme + ilk fotoğraf koleksiyonu.
2. Fotoğraflı premium menü + ürün detay/sepet/takip tasarımı.
3. Altı kategori/24 ürün + tüm görseller + admin içerik alanları.
4. Yerel şarkı kataloğu + siparişe bağlı öneri + yönetim.
5. Favori/tekrar sipariş/ürün eşleştirmesi ve PWA/yayın cilası.

Her dilim ayrı çalışır ve gösterilebilir biçimde tamamlanır. Tasarım dilimi mobil/desktop görsel kontrol ve mevcut satın alma regresyonuyla; yeni veri/izin akışları anlamlı backend testleriyle doğrulanır. İlk teslim ölçütü fotoğraflı premium menüde çalışır mevcut ödeme akışıdır. Süre tahmini ilk görsel koleksiyonu ve tasarım dilimi tamamlandıktan sonra güncellenir.

## Kaynaklar

- https://musicbrainz.org/doc/MusicBrainz_API
- https://musicbrainz.org/doc/About/Data_License
- https://musicbrainz.org/doc/MusicBrainz_API/Rate_Limiting
