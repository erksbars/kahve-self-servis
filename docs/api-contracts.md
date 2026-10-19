# Uygulanan API — Aşama 0–4

| Method | Yol | Yetki / davranış |
| --- | --- | --- |
| GET | /api/health | Veritabanı bağlantısı; 200/503 |
| GET | /api/branches/{slug}/menu | Public, doğru şube fiyatları ve demo işareti |
| POST | /api/checkouts/quote | Public + CSRF; güncel katalogdan fiyat/uygunluk doğrulama |
| POST | /api/checkouts | Public + CSRF + Idempotency-Key; sandbox checkout |
| GET | /api/sandbox/sessions/{token} | Sadece uzun session capability ile snapshot/sonuç |
| POST | /api/sandbox/sessions/{token}/decision | CSRF + session capability; demo sağlayıcı sonucu |
| POST | /api/payments/sandbox/webhook | CSRF yerine HMAC imza + zaman damgası |
| GET | /api/orders/track/{token} | Sadece siparişe özel uzun tracking capability |
| GET | /api/staff/branches/{id}/orders | Yetkili tenant/şube; yalnızca ücretli aktif siparişler |
| PATCH | /api/staff/orders/{id}/status | Yetkili tenant/şube + CSRF + expectedVersion |
| GET | /api/session/csrf | CSRF cookie + request token |
| POST | /api/session/login | email/password; CSRF; IP başına 10 deneme/dakika |
| GET | /api/session/me | Oturum; email/role |
| POST | /api/session/logout | Oturum + CSRF |
| GET | /api/staff/branches | Oturumun tenant ve staff branch scope'u |
| PATCH | /api/staff/branches/{id}/accepting-orders | Yetkili şube + CSRF + expectedVersion |
| GET | /api/admin/products | Owner, tenant scope; salt okunur |

PATCH body: `{ "isAcceptingOrders": false, "expectedVersion": 1 }`. Başarılı cevap ID, yeni kabul durumu ve version içerir. Eski sürüm 409; yetkisiz veya olmayan şube 404. Webhook dışında her mutation `X-CSRF-TOKEN` header'ı gerektirir. Login/logout sonrası kimlik değiştiğinden mutation öncesi yeni CSRF alınır.

Menu DTO: branch `{id,name,slug,address,isAcceptingOrders}`, merchant, currency, demo, products `[{id,name,description,category,price,available,optionGroups}]`. Her grup `{id,name,minSelect,maxSelect,values:[{id,name,priceDelta,available}]}` döndürür. Şube seçenek override'ı fiyat farkına/uygunluğa uygulanır. Personel veya parola alanları public cevaba eklenmez.

## Quote

Body: `{ "branchSlug": "demo-moda", "items": [{ "productId": "uuid", "quantity": 2, "optionValueIds": ["uuid"] }] }`.

Cevap: `{branchSlug,currency,total,calculatedAt,items:[{productId,name,quantity,basePrice,unitPrice,lineTotal,options:[{id,group,name,priceDelta}]}]}`. Fiyat, delta ve toplam sadece sunucuda hesaplanır. Quote kalıcı checkout veya ödeme rezervasyonu değildir.

Hatalar: 400 invalid_cart (min/max, tekrar seçenek, başka ürünün seçeneği, sınırlar); 404 branch_not_found; 409 branch_closed / product_unavailable / option_unavailable. Response `{code,message}`. Sepet 1–20 satır, satırda 1–10 adet, toplam en fazla 50 adet, en fazla 10 seçenek/satır. API'nin gördüğü IP başına 60 quote/dakika. Canlı reverse proxy için güvenilir forwarded headers ve rate limit politikası ayrıca yapılandırılmalıdır.

## Henüz uygulanmayan sözleşmeler

Müzik endpoint'leri sonraki dilim içindir. Gerçek ödeme sağlayıcısı, iade ve iptal akışı henüz uygulanmadı.

## Checkout / sandbox / takip

Checkout body: quote body'si + `expectedTotal` (onaylanan toplam). Header `Idempotency-Key`: sıfır olmayan UUID. Cevap `{paymentUrl,amount,currency,expiresAt}`; paymentUrl `/sandbox/pay/{sessionToken}`. Şube + key unique'dir; aynı gövde aynı oturumu döndürür. Farklı gövde/key tekrarı 409 idempotency_conflict; yeniden fiyat doğrulamada fark varsa 409 price_changed. Kapalı şube/geçersiz seçenekler quote ile aynı kurallara tabidir. Katalog daha sonra değişse bile oluşturulmuş ödeme snapshot'ı korunur.

Session GET, snapshot items ve yalnızca Paid ise trackingUrl döndürür. Decision body `{ "outcome": "paid" }` veya `failed`. Bu demo endpoint'i sağlayıcının server-signed bildirimini aynı doğrulanan işlemciden geçirir. Kart alanı, müşteri hesabı veya redirect success parametresi yoktur. Sonuç terminaldir; Failed/Expired yeni key ile yeni checkout ister.

Webhook body `{eventId,providerReference,status,amount,currency}`; status paid/failed. Header `X-Sandbox-Timestamp`: Unix seconds. Header `X-Sandbox-Signature`: `HMAC-SHA256(secret, timestamp + "." + rawBody)` hex. ±300 saniye toleransı, fixed-time imza kontrolü. Geçersiz imza/zaman 401; yanlış tutar/para birimi 400; aynı event ID farklı gövde 409; tekrar aynı olay 200 duplicate. Event kaydı, ödeme/durum geçmişi ve outbox tek transaction'dadır. Ödeme süresi dolmuşsa 410 ve Expired; bu sandbox davranışıdır, gerçek sağlayıcının gecikmiş tahsilat/iade kuralları ayrı tasarlanacaktır.

Tracking cevabı `{publicNumber,status,paymentStatus,total,currency,createdAt,paidAt,branch,items,demo}`. Token 32 kriptografik rastgele byte'ın base64url temsilidir (43 karakter); DB'de SHA256 hash ve tekrar yanıt verebilmek için ayrı AES-GCM korumalı kopya bulunur. Sipariş numarası veya GUID takip yetkisi vermez. Yanlış token 404. Staff liste token/ödeme session bilgisi döndürmez; Paid + Received/Preparing/Ready kayıtları paymentVerified ile görünür. Yetkisiz şube 404.

## Staff status ve SignalR

PATCH body `{ "status": "Preparing", "expectedVersion": 3 }`; hedef yalnızca Preparing / Ready / Completed. Received → Preparing → Ready → Completed sırası zorunlu; terminal durum geri dönmez. Tüm değişimler history + audit + RealtimeEvent ile tek transaction'dadır. Başarı `{id,status,version}`; eski sürüm 409 version_conflict + `{current:{status,version}}`; geçersiz durum/ücretsiz sipariş 409 invalid_transition. Yetkisiz veya olmayan sipariş 404. Şube kabulü kapalıyken ücretli sipariş tamamlanabilir.

Hub yolu `/hubs/orders`. Browser aynı-origin bağlantısı, cookie auth ve server-side grup yetkisi kullanır. `JoinStaff(branchId)` aktif staff/rol/tenant/şube yetkisini DB'den doğrular. `JoinTracking(secureToken)` yalnızca tek siparişi hash ile bulur; token URL/query yerine hub mesajında gönderilir. Client keyfi group adı gönderemez. Grup adları server tarafından `branch:{id}:staff` / `order:{id}:tracking` üretilir.

Event'ler `OrderChanged` (tracking'e version; personele orderId/version), `BranchChanged` ve `AccessRevoked`. Event snapshot'ın yerini almaz; istemci yetkili GET ile güncel durumu yükler. Worker bir şube olayını yayınlamadan önce bağlı staff yetkilerini tekrar kontrol eder, yetkisi kaldırılan üyeyi gruptan çıkarır. Cookie süresi dolduğunda bağlantı kapanır. Reconnect'te join yetkisi yeniden doğrulanır ve kaçırılan olaylar GET ile toparlanır. Origin uyuşmazlığı 403; hub mesaj sınırı 4096 byte.

## İşletme yönetimi (Aşama 5)

`/api/admin/*` yalnızca aktif Owner hesabı için, kendi tenant query filter'ı altında çalışır. POST/PUT CSRF ister. Mutation'lar tenant advisory lock + transaction, sürüm kontrolü ve audit kaydı kullanır. PUT eski `expectedVersion` ile 409; başka tenant nesnesi 404 (yabancı kategori/seçenek/şube referansı 400).

| İşlem | Yol | Gövde / yanıt |
| --- | --- | --- |
| Katalog oku | GET `/api/admin/catalog` | categories, products + optionGroups/values, branchProducts, branchOptions |
| Kategori ekle/düzenle | POST `/categories`, PUT `/categories/{id}` | name, sortOrder, expectedVersion |
| Ürün ekle/düzenle | POST `/products`, PUT `/products/{id}` | categoryId, name, description, basePrice, taxRate, active, sortOrder, optionGroups, expectedVersion |
| Şube fiyat/bulunurluk | PUT `/branches/{branchId}/products/{productId}` | available, nullable priceOverride, expectedVersion, options[{optionValueId,available,priceOverride}] |
| Şube oku/ekle/düzenle | GET/POST `/branches`, PUT `/branches/{id}` | name, slug, address, timeZone, isAcceptingOrders, preparationCapacity, hours[{day,opens,closes}], expectedVersion |
| Personel liste/davet | GET/POST `/staff` | POST email, role (Owner/Barista), branchIds; yanıt id, invitationUrl |
| Davet yenile | POST `/staff/{id}/invitation` | yalnızca kabul edilmemiş hesap; eski token'lar expire edilir |
| Yetki/aktiflik | PUT `/staff/{id}` | role, active, branchIds, expectedVersion |
| Rapor | GET `/reports/summary?from=YYYY-MM-DD&to=YYYY-MM-DD&branchId=...` | en fazla 366 gün, kapsayıcı yerel tarih aralığı |

Tablodaki kısa yollar `/api/admin` önekiyle kullanılır. Yeni group/value kimliği boş UUID'dir; mevcut kimlikler ürüne ve gruba ait olmalı. Gruplar aggregate olarak uzlaştırılır; kaldırma eski sipariş JSON snapshot'ını etkilemez. Vergi fiyata dahildir. Boş override temel fiyatı kullanır. Kapalı ürün menüden çıkar; bulunmayan ürün görünür ama checkout reddedilir.

Saatler Pazar=0…Cumartesi=6, gün başına tek HH:mm aralığıdır. Açılış < kapanış; gece aşımı yok. Boş liste sınırsız saat demektir; manuel kabul ayrıca kontrol edilir. `isOpenNow` ikisini birlikte yansıtır. Mevcut slug değiştirilemez. Kapasite yalnızca operasyon ayarıdır.

Rapor: `paidOrders`, `completedOrders`, `revenue`, `cancelledOrders`, `failedPayments`, nullable `averagePreparationMinutes`, `preparationSamples`, `timeZone`, `currency`, `demo`. Başarılı sipariş kohortu PaidAt ile seçilir; hazırlık süresi Preparing/Ready history'den ölçülür. Failed/Cancelled CreatedAt ile seçilir. Tüm şubeler Europe/Istanbul, tek şube kendi saat dilimiyle hesaplanır. Geçersiz yaz saati tarih sınırı 400 verir.

Public GET `/api/invitations/{token}` email/role/expiry döndürür; POST `/{token}/redeem` gövdesi `{password}` (12–128 karakter), CSRF ve ayrı IP başına 10/dakika davet limiti ister. Token 256-bit rastgele, DB yalnızca SHA256 hash tutar; 24 saat ve tek kullanım. Kullanılmış/expire token 410. Aktivasyon oturum açmaz. Personel GET parola/hash/token döndürmez; pasifleştirme/rol değişimi mevcut cookie'yi bir sonraki istekte geçersiz kılar. Otomatik e-posta yok.

## Sağlayıcı seçimi hazırlığı (13 Ekim 2026)

`Payments:Provider` / Compose `.env` `PAYMENTS_PROVIDER`: `sandbox`, `iyzico` veya `none`. Ayar verilmezse Sandbox:Enabled=true için sandbox, aksi halde none seçilir. Kurulu olmayan sağlayıcı API startup'ında hata verir. Seçim başlangıçta yapılır; değiştirmek restart/recreate ister. iyzico işletmeye özel sandbox anahtarlarını ve public HTTPS callback origin'i gerektirir.

Checkout tekrar yanıtının yolu mevcut payment.Provider üzerinden kurulu adaptörden alınır. Adaptör bulunamıyorsa 503 `{code:"provider_unavailable",message}`; yeni order/payment oluşturulmaz. İlk ve tekrar checkout expiresAt değerleri PostgreSQL mikro saniye hassasiyetinde aynıdır.

Public menü `checkoutProvider` ve `checkoutEnabled` döndürür. iyzico seçiliyken işletmenin hesabı yoksa false olur. UI ödemeye geçişi kapatır; menü, quote ve sepet korunur. Anahtar içermez.

Sandbox GET/decision yalnızca Provider=sandbox kayıtlarına erişir; başka sağlayıcının aynı session token'ı 404 döner. Sandbox imza/JSON doğrulaması WebhookProcessor'da, provider-normalized verified sonucun atomik işlenmesi PaymentSettlement'tadır. VerifiedPaymentEvent internal'dır, herhangi bir HTTP route body sözleşmesi değildir. Mevcut signature, CSRF, idempotency, tutar/currency, history/outbox ve terminal durum davranışları korunur.

Sandbox expiry reddi demo sağlayıcısının politikasıdır. iyzico doğrulanmış geç ödeme sonucunu kabul eder; geçerli initialize süresi yine 15 dakikadır.

## iyzico Checkout Form sandbox

- GET `/api/iyzico/sessions/{token}`: 43 karakter capability ile yalnızca iyzico oturumu. paymentStatus, initializationState, expiresAt, branch, amount/currency ve snapshot items. Hazır/henüz geçerli Pending oturumda doğrulanmış hosted paymentUrl; Paid olduğunda trackingUrl. Anahtar/provider token/PII döndürmez.
- POST `/api/iyzico/sessions/{token}/initialize`: CSRF zorunlu. Body `{name,surname,identityNumber,email,phone,address,city,country}`. Bilgiler iyzico'ya gönderilir, DB'ye yazılmaz. Yeni/Rejected oturumda initialize; Ready ise aynı paymentUrl. Pending olmayan, süresi dolan veya sonucu belirsiz oturum 409. Açık sağlayıcı reddi 422, belirsiz protokol/bağlantı hatası 503; yeni checkout oluşturmaz.
- POST `/api/iyzico/sessions/{token}/verify`: CSRF zorunlu; kayıtlı şifreli token ile server-side retrieve, ardından güncel session DTO. Doğrulanmamış sonuç Paid yapmaz.
- POST `/api/payments/iyzico/callback/{reference}`: 32 hex reference + form-urlencoded token. Personel cookie/CSRF gerektirmez; ödeme yalnızca imzalı server-side retrieve ile doğrulanır. Bilinmeyen reference 404, doğrulanamayan sonuç 503, doğrulanmış sonuç sabit yerel receipt adresine 302. Public gateway yalnızca bu dönüşü 303'e çevirir.

Initialize=NotStarted/Initializing/Ready/Rejected/Unknown/Review. Review, Unknown ve Initializing ikinci initialize'ı engeller. Pending bilinen token'lar son 7 gün için background uzlaştırılır. İade/canlı tahsilat API'si yoktur. Kurulum ve sınırlar: `iyzico-setup.md`.

## Ürün görselleri

Public menu ürünleri `imageUrl` ve `imageAlt` ek alanlarını döndürür; görseli olmayan üründe null. Admin catalog aynı alanları içerir. Genel ürün kaydetme body’si görsel URL'si belirleyemez; yalnızca aşağıdaki owned-product işlemleri görseli değiştirir.

- PUT `/api/admin/products/{id}/image?expectedVersion=N&alt=...`: Owner, tenant ve CSRF gerekir. Body ham JPG/PNG/WebP dosyasıdır; multipart değildir. En fazla 5 MiB, 32–6000 px ve 16 megapiksel, hareketsiz raster. Decoder gerçek formatı doğrular; EXIF yönü uygulanır, metadata atılır ve en uzun kenarı 960 px WebP üretilir. Rastgele dosya kimliği tenant dizininde saklanır. Yanıt `{imageUrl,imageAlt,version}`; 400 yanlış dosya/açıklama, 404 yabancı/olmayan ürün, 409 sürüm çakışması, 413 boyut sınırı. Kullanıcı başına 12/dakika yükleme sınırı.
- DELETE `/api/admin/products/{id}/image?expectedVersion=N`: aynı yetki/CSRF/version kontrolüyle ürün görsel ilişkisini kaldırır; dosya fiziksel olarak silinmez. Yanıt null görsel alanları ve yeni version. Yönetim formu bu version ile diğer alanları kaydeder.
- GET `/api/catalog-images/{tenantId}/{32hex-key}`: yalnızca DB'de o tenant'ın ürünüyle eşleşen görseli verir; doğru public URL anonim erişilebilir. Yanlış tenant/kimlik veya ilişkisiz görsel 404. `image/webp`, nosniff ve public immutable cache (1 gün). Kaldırılan eski URL sunucuda 404 olsa da önceden cache edilmiş kopya cache süresince kalabilir; ürün fotoğrafları public içeriktir.

Demo fotoğrafları web paketindeki `/images/menu/*.webp` dosyalarıdır. Kullanıcı yüklemeleri source repository dışında kalıcı `catalog-images` volume'unda tutulur; bu volume deploy/backup planına eklenmelidir. İlişkisiz eski dosyaları temizleme ve depolama kotası sonraki production kapsamında; mevcut detach dosyayı kalıcı silmez.
