# Siparişe bağlı istek parçalar

19 Ekim 2026 kararıyla müzik özelliği yalnızca metin isteği olarak sadeleştirildi. Katalog seçimi, MusicBrainz aktarım ekranı, Spotify/YouTube bağlantıları ve siparişten bağımsız öneri düğmesi güncel akıştan kaldırıldı. Harici müzik API'si, oynatıcı veya hesap bağlantısı yoktur.

## Müşteri

Menü → ürünleri sepete ekle → **Sepetin** içinde **Bir şarkı isteğin var mı? (İsteğe bağlı)** alanına şarkı/sanatçı yaz → sepeti doğrula → ödemeye geç. En fazla 240 karakter; boş veya yalnızca boşluk girilirse şarkı isteği oluşturulmaz. Metin, ödeme oturumu ve siparişle birlikte tek transaction içinde kaydedilir. Ödeme doğrulanmadan işletme paneline çıkmaz; müşteri metni ödeme sonrasında ayrıca göndermez. Çalınma garantisi verilmez.

Metin şubeye özel sessionStorage taslağında tutulur; aynı sekmede sayfa yenilenince korunur, başka şubeye taşınmaz. Ödeme doğrulandığında ilgili şubenin taslağı ve sepeti temizlenir; başarısız ödemede korunur. Tarayıcı depolamayı engellerse form çalışır, yenilemede taslak korunması garanti edilemez. Checkout anahtarı aynı metin/sepet için tekrar kullanılır. Farklı şarkı aynı anahtarla gönderilirse 409 döner. İstekler HTML olarak yorumlanmaz.

Paid siparişin kendi takip bağlantısında istek metni ve Pending/Accepted/Rejected kararı görünür. Sunucu sorgusu order + tenant + branch ile sınırlandırılır. Kabul edilmesi çalınma garantisi anlamına gelmez.

## İşletme ve personel

- Owner: `/admin` → **İstek parçalar**, şube seçimi.
- Barista: `/staff` → sipariş panosunun altındaki **İstek parçalar** alanı, panoda seçili şube.
- Liste yalnızca o şubenin ödemesi Paid ve iptal edilmemiş siparişlerine bağlı metin isteklerini getirir. Sipariş numarası, metin, tarih ve durum görünür. Yeni/kabul/red/tümü filtresi sunucuda uygulanır; en yeni önce, eşit tarihlerde ID ile kararlı sıralama ve sayfa başına 20 kayıt. Teslim edilmiş siparişlerin istekleri listede kalır. Eski bekleyen istekler son 100 kayıt sınırına takılmaz.
- Panel başlığında şubenin tüm bekleyen istek sayısı gösterilir; seçili filtre/sayfadan bağımsızdır. Personel panosunun üstünde aynı sayacı paylaşan **İstek parçalar · N yeni** kısayolu vardır. Başarılı kabul/red sonrasında sipariş numarasıyla görünür onay mesajı verilir.
- Beş saniyede bir yenilenir; **Yenile** ile elle de güncellenir. Ağ hatasında mevcut veriyle karar gönderimi kapatılır.
- **Kabul et / Reddet** yalnızca Pending durumunda çalışır. Kabul etme müzik çalmaz veya harici oynatıcıya sıra eklemez.

## API ve bütünlük

`POST /api/checkouts`: mevcut sepet alanlarına optional `requestedSong` (string, en fazla 240 karakter) eklendi. Yeni istek, siparişle aynı transaction içinde SongRequests'e yazılır: OrderId dolu, SongId ve GuestKeyHash null, RequestedText trim edilmiş, Status Pending. Mevcut kaynak/seçim CHECK, order unique ve tenant/şube/order composite FK'leri geçerlidir; yeni migration gerekmedi. Şarkı yazılmayan eski checkout'ların fingerprint biçimi korunur.

`GET /api/staff/branches/{branchId}/song-requests?status=Pending&page=1&pageSize=20`: Owner/Barista oturumu; Owner kendi tenant şubelerine, Barista sadece atanmış şubeye erişir. Status Pending (varsayılan), Accepted, Rejected veya All. Page 1–10000; pageSize 1–50 (varsayılan 20). Geçersiz parametre 400, başka tenant/şube 404, anonim erişim 401.

Yanıt artık `{ items, pendingCount, total, page, pageSize, totalPages }` nesnesidir; items satırı id/text/publicNumber/status/version/createdAt içerir. Total seçili filtreye, pendingCount tüm şubeye aittir. Count/sayfa verileri aynı RepeatableRead snapshot'ında okunur. İstenen sayfa son sayfayı aşarsa son geçerli sayfa döner; son sayfadaki istekler işlendiğinde boş bir sayfada kalınmaz. Boş sonuç page=1,totalPages=1,total=0. Güncel istemciye geçiş için açık eski panel sekmeleri yenilenmelidir.

`PUT /api/staff/branches/{branchId}/song-requests/{id}`: `{ status: "Accepted" | "Rejected", expectedVersion }`. CSRF, aynı şube yetkisi, Paid/iptal durumu, Pending ve version denetlenir. Ortak admin transaction/advisory lock ve audit uygulanır. Stale karar 409. Ödenmemiş istek listelenemez veya karara bağlanamaz.

Eski public `GET/POST /api/branches/{slug}/music` ve `GET/POST /api/orders/track/{token}/music` kaldırıldı. Bağımsız anonim öneri ve 10 dakikalık çerez beklemesi artık kullanılmaz. Öneri sayısı sipariş başına unique kayıt ile sınırlıdır.

Eski katalog/veri/migration ve Owner katalog uyumluluk endpoint'leri geçmiş veriyi korumak için yerinde bırakıldı; güncel arayüz bunları çağırmaz. Önceki MusicBrainz rehberi ve hazırlama aracı arşiv kapsamındadır. Kullanıcı verisi veya siparişler silinmedi. Abonelik kapsam dışıdır.
