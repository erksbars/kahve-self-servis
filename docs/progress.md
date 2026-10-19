# Aşama 0–1 teslim kaydı

Bu ilk teslimin tarihsel kaydıdır. Güncel kapsam Aşama 4'e kadar aşağıda genişletildi.

Tarih: 12 Ekim 2026. Çalışma branch'i: `codex/mvp-foundation`.

## Çıktı

Boş repo incelendi, mimari kararlar yazıldı. .NET/React proje yapısı, PostgreSQL migration, demo seed, personel oturumu, tenant/branch izolasyonu, audit, Docker Compose ve CI tanımı oluşturuldu. Müşteri menüsü API'ye bağlandı; personelin kabul ayarı gerçek DB üzerinde çalışıyor.

## Yerel test sonucu

- Backend: 9/9 başarılı; Testcontainers ile gerçek, geçici PostgreSQL.
- Frontend: 3/3 başarılı; CSRF ve API hata davranışı.
- Tarayıcı: 5/5 başarılı; gerçek API ile menü, oturum, aç/kapat ve 320/390/1440px taşma kontrolü.
- TypeScript + Vite production build başarılı.
- Docker image build ve `docker compose up -d --build --wait` başarılı. Menü ekran görüntüleri mobil/masaüstünde görsel olarak incelendi.
- Uzak GitHub CI çalıştırılmadı; yalnızca workflow tanımı eklendi.

## Dosya grupları

- `services/api`: modüller, altyapı, migration ve Dockerfile.
- `apps/web`: müşteri/personel arayüzü, API client, testler, Vite ve Nginx.
- `tests/api-tests`: PostgreSQL fixture ve izolasyon/güvenlik testleri.
- `docker-compose.yml`, `.env.example`, `scripts`: yerel başlatma.
- `docs`, `README.md`, `.github/workflows/ci.yml`: kararlar, sözleşmeler, kurulum ve CI.

## Bilinen sınırlar ve sonraki kapsam

Bu sürüm satın alma veya ödeme yapmaz. Varyant/ekstra/sepet, quote, ödeme/sipariş modülleri, SignalR, tam yönetim paneli, QR görseli ve PWA cache sonraki dilimlerdir. Ürün kategorisi başlangıçta metin alanıdır. Demo parolaları ilk seed sırasında oluşur; yeniden başlatma parola reset'i yapmaz. Docker API restart'ı cookie anahtarlarını kalıcı tutmaz; canlıya geçmeden keyring yapılandırılmalıdır.

Bir sonraki doğrulanabilir dilim Aşama 2: seçenek grupları, sepet, branch/ürün uygunluğunu doğrulayan server quote, QR bağlantısı/görseli; kapsamı fiyat ve bulunurluk testleriyle kapatılacak. Ödeme testleri Aşama 3, canlı operasyon/takip testleri Aşama 4'te eklenecek.

## Aşama 2 teslim kaydı — 12 Ekim 2026

İlişkisel kategoriler, seçenek grupları/değerleri, şube bazında seçenek override'ı ve mevcut kategorileri/verileri koruyan migration eklendi. Demo seçenekleri mevcut veri silinmeden seed edilir. Müşteri boy/süt/ekstra ve adet seçebilir; aynı seçimler birleşir, şubeye özel sepet yenilemede korunur. Public quote API güncel fiyatları ve satış durumunu doğrular. Ürün/seçenek kapandıysa reddeder; istemci fiyatı kullanılmaz. Personel şubeye özel QR üretebilir, paylaşım adresini değiştirebilir, PNG indirebilir ve yazdırabilir.

Doğrulama: **20 backend + 6 frontend + 12 tarayıcı = 38 başarılı test**. Gerçek PostgreSQL, Docker image build ve mevcut demo DB üzerine migration başarılı. TypeScript/Vite build başarılı. 320/390/1440px ürün seçim ve sepet ekranları test edildi; mobil ekran görüntüleri görsel olarak incelendi. Tarayıcı testlerinde seçime göre 430 TL quote, adet değişiminde doğrulamanın kaldırılması, bağlantı hatasında sepetin korunması ve güvenli retry, şube ayrımı ve QR PNG indirme kontrol edildi. Yazdırma düğmesi print akışını açar; fiziksel çıktı/telefon taraması yapılmadı. Uzak CI henüz çalıştırılmadı.

Yeni/değişen gruplar: `Modules/Catalog`, `Modules/Orders/QuoteEndpoints.cs`, `Infrastructure`, `Migrations/*CatalogOptions*`, frontend CustomerMenu/ProductPicker/Cart/Dialog/BranchQr, CSS, backend QuoteTests, frontend cart testleri ve e2e/cart.spec.ts. API sözleşmesi ve README güncellendi.

Sonraki dilim **Aşama 3**: gerçek checkout oluşturma, sabit fiyat snapshot'ı, güvenli tracking token, sahte sandbox sağlayıcısı, imzalı/idempotent webhook ve outbox. Bu teslimde ödeme alınmaz, sipariş oluşturulmaz. Quote geçmiş sipariş snapshot'ı yerine geçmez. Yönetim CRUD'u, çalışma saatleri, barista sipariş kuyruğu, SignalR ve PWA cache sonraki dilimlerdir.

## Aşama 3 teslim kaydı — 12 Ekim 2026

Checkout, Payment/Order/snapshot, OrderStatusHistory, PaymentWebhookEvent ve PaymentOutbox tabloları/migration eklendi. Checkout onaylanan tutarı tekrar doğrular; şube bazında idempotency key + request hash aynı işlemden ikinci siparişi engeller. Eşzamanlı tekrarlar DB lock/constraint ve sınırlı transaction retry ile toparlanır. Hosted yerel sandbox kart bilgisi istemez ve gerçek para çekmez; demo sağlayıcı sonucu imzalı webhook işlemcisinden geçer. HMAC, timestamp, amount/currency ve tekrar event kontrolleri uygulanır.

Paid + webhook/history/outbox atomiktir. Worker Paid → Received aktarır; hata/retry veritabanında kalıcıdır. Failed/Expired kuyrukta görünmez. Anonim takip uzun, siparişe özel token ile çalışır; DB'de hash ve şifreli kopya bulunur. Staff queue API'si tenant/branch scope'lu ve salt okunurdur; token veya ödeme session alanı döndürmez. Frontend cevap kaybında aynı checkout key'i ve sunucu ödeme sonucuyla toparlanır. Başarılı ödemede sepet temizlenir; başarısızda korunur.

Doğrulama: **37 backend + 10 frontend + 21 tarayıcı = 68 başarılı senaryo**. Backend gerçek geçici PostgreSQL kullanır. E2E'de 20 senaryo ilk çalışmada geçti; URL manipülasyonu testi ödeme navigasyonunu beklemediği için test düzeltildi ve hedefli tekrarında geçti. Mobil ödeme/takip senaryoları görsel QA için tekrar çalıştırıldı. Uzak CI çalıştırılmadı.

Kapsam: aynı anda 5 checkout isteğinde tek kayıt/oturum; yinelenen webhook'ta tek Paid transition/outbox; geçersiz imza/eski/gelecek timestamp/tutar/currency reddi; başarısız/Expired terminal durum; snapshot korunması; kısa/yanlış tracking token reddi; outbox hatasında Paid kaydının korunması ve retry ile tek Received geçişi; tenant scope; sandbox kapalıyken secrets olmadan 404. Tarayıcı ödeme başarı/başarısız/retry, response loss, redirect query manipülasyonu, sepet temizleme ve 320/390/1440px taşma kontrollerini kapsar.

Dosya grupları: `Modules/Payments`, `Modules/Orders`, `Infrastructure/CapabilityTokens.cs` ve locks/DbContext, `Migrations/*CheckoutPayments*`, frontend PaymentPages/checkout/API/CustomerMenu/routes/styles, PaymentTests/checkout.test/e2e/payments.spec, secrets init/run scripts, Compose/CI/Nginx ve dokümantasyon.

Sınırlar: gerçek tahsilat yok; barista işlem arayüzü ve SignalR yok; müşteri takibi 2 saniye polling. İade/iptal/gerçek sağlayıcı gecikmiş tahsilat akışı uygulanmadı. Sandbox çalışma ortamı Development/Testing ile sınırlı. Tracking encryption key ve webhook secret aynı DB ile korunmalı. E2E demo DB'de örnek siparişler oluşturur.

Sonraki doğrulanabilir dilim **Aşama 4**: barista Yeni/Hazırlanıyor/Hazır panosu, tenant/branch yetkisi ve expectedVersion ile durum güncelleme, SignalR + reconnect/polling, müşteri sayfasında canlı ilerleme ve teslim.

## Aşama 4 teslim kaydı — 12 Ekim 2026

`/staff` içinde şube seçimi ve Yeni/Hazırlanıyor/Hazır panosu eklendi. Kartlar ödeme onayı, ürün/adet/seçenek snapshot'ı, paidAt üzerinden geçen süre ve 10 dakikada uzun bekleme işareti içerir. Personel Preparing/Ready/Completed geçişini büyük butonlarla yapar; expectedVersion çakışması 409 olarak görünür ve doğru son durum yüklenir. Şube yeni siparişe kapalıyken önceki ücretli sipariş tamamlanabilir. Teslim aktif panodan çıkar.

SignalR hub'da server-side yetkili branch/order grupları, aynı-origin kontrolü, reconnect'te yeniden join ve GET snapshot uygulanır. Tracking token query yerine hub mesajıyla gider. Staff üyeliği katılımda ve branch olayı yayınlanmadan önce aktiflik/rol/tenant/şube üzerinden doğrulanır; iptal edilmiş üyelik gruptan çıkarılır. HTTP mutation CSRF ve tenant/branch scope ile korunur. Her status/şube değişimi transaction içinde RealtimeEvent kaydeder; bildirim hatasında event pending kalır ve tekrar denenir.

Yeni sipariş ekran bildirimi ve kullanıcı tıklamasıyla açılan Web Audio uyarısı eklendi. Offline butonlar devre dışıdır; sonuçlar server ACK olmadan optimistic yazılmaz. SignalR yoksa 2 saniyelik polling, canlıyken 30 saniyelik uzlaştırma vardır. Masaüstünde üç sütun birlikte; küçük ekranlarda sütunlar arası sekmeler bulunur. Başarılı işlemden sonra telefonda ilgili aşamaya geçilir; teslimden sonra Yeni kuyruğuna dönülür.

Doğrulama: **45 backend + 10 frontend + 28 tarayıcı = 83 başarılı senaryo**. Tüm 28 E2E aynı çalıştırmada geçti. Son mobil sütun geçişi düzenlemesinden sonra 320/390/1440px teslim senaryoları hedefli tekrarlandı. TypeScript/Vite ve Docker build başarılı; PostgreSQL migration mevcut veriye uygulandı. Masaüstü ve mobil pano ekran görüntüleri görsel QA'dan geçti. Fiziksel cihazda ses duyumu/OS bildirim testi yapılmadı; tarayıcıda ses açma kontrolü doğrulandı. Uzak CI henüz çalıştırılmadı.

Yeni testler: tam durum zinciri/history/audit, aynı sürümle eşzamanlı barista çakışması, geçersiz/ücretsiz/terminal geçiş, CSRF, diğer tenant/atanmamış şube, hub branch/token yetkisi, cross-origin reddi, aktiflik iptaliyle grup üyeliği kaldırma, realtime yayın hatası ve kalıcı retry. E2E gerçek WebSocket OrderChanged frame'ini, yeni siparişin sayfa yenilemeden gelmesini, müşteride canlı hazırlık/hazır/teslim, kapalı şubede önceki siparişi tamamlama, stale barista 409, offline kontrol, yeniden bağlanma, SignalR blokluyken polling ve mobil sütun seçimini doğrular.

Değişen gruplar: `Modules/Realtime`, `Modules/Orders/StaffOrderEndpoints.cs` ve transitions, DbContext/RealtimeOrders migration, branch kabul mutation'ı, Program/hub config, frontend StaffBoard/useLive/PaymentPages/App/API/CSS, Vite/Nginx WebSocket proxy, StaffOperationTests/e2e/barista.spec ve docs/README.

Bilinen sınırlar: gerçek para tahsilatı yok; SignalR tek API instance için yapılandırıldı; işletme katalog/personel CRUD ve raporlar henüz yok; PWA cache, iptal/iade ve gerçek sağlayıcı entegrasyonu sonraki kapsam. Temel müşteri menüsü → sandbox ödeme → personel operasyonu → teslim akışı artık çalışır. Bir sonraki dilim **Aşama 5**: işletme menü/ürün/seçenek/şube/personel yönetimi ve temel raporlar.

## Aşama 5 teslim kaydı — 12 Ekim 2026

`/admin` işletme sahibi yönetim paneli eklendi. Kategori/ürün düzenleme ve ekleme, seçenek grup/değerleri, vergi dahil fiyat ayarı, satıştan kapatma, şube fiyat/bulunurluk ve seçenek override'ları çalışır. Ürün ve seçenek değişimi geçmiş ödeme/sipariş snapshot'larını değiştirmez. Seed marker restart sırasında kaldırılmış seçenekleri geri üretmez.

Şube adı/adresi/IANA saat dilimi, haftalık saatler, manuel kabul ve hazırlık kapasitesi düzenlenebilir. Yeni şube mevcut ürünlere bağlanır; aktif owner'lar erişim alır. Mevcut slug QR korunması için sabittir. Saatler public menü + quote/checkout doğrulamasına bağlıdır. QR PNG indirme/yazdırma mevcut bileşenle yönetim ekranına bağlandı.

Personel daveti 24 saatlik, tek kullanımlık capability link oluşturur; DB raw token saklamaz. `/invite/:token` parola oluşturma ekranı eklendi. Yenileme eski token'ı geçersiz kılar. Rol/şube/aktiflik değiştirilir; pasifleştirme bir sonraki HTTP isteğinde mevcut cookie erişimini iptal eder. Kendi yönetici erişimini kapatma engellenir. Davet kabulü ve login ayrı 10/dakika rate limit havuzlarına sahiptir. Otomatik e-posta yok.

Tarih ve şube filtreli rapor onaylı sipariş, satış toplamı, teslim edilen, iptal, başarısız ödeme ve gerçek Preparing → Ready ortalamasını gösterir. Ödeme tarihi kohortu, tarih sınırları ve saat dilimi ekranda açıklanır; yeterli history yoksa ölçüm yoktur.

Doğrulama: **52 backend + 10 frontend + 34 tarayıcı = 96 başarılı senaryo**. Tarayıcı paketinin 29'u gerçek API ile, 5'i açıkça fixture kullanılan yönetim arayüzü kontrolüdür. Tam paket son çalıştırmada geçti. Yeni backend kapsamı Owner/Barista ve tenant sınırları, PUT CSRF/version, yabancı seçenek/kategori/şube reddi, override quote, snapshot korunması, çalışma saati, slug koruması, davet yenileme/tek kullanım/parola/erişim iptali, paid-only rapor ve saat değişimi tarih sınırıdır.

320/390/1440px yönetim/ürün/saat formu ve QR kontrolleri geçti; mobil saat formunun yatay taşması düzeltildi ve dialog iç genişliği test edildi. Gerçek API ile UI'dan ürün ekleme, şube fiyatını değiştirip quote alma, QR ve satıştan kapatma doğrulandı. Yönetim, mobil formlar ve gerçek rapor ekranları görsel QA'dan geçti. .NET/TypeScript/Vite ve Docker derlemeleri başarılı; mevcut DB'ye migration'lar uygulandı. Testlerde tekrar tekrar girişin rate limit tüketmesi oturum paylaşımıyla düzeltildi; üretim giriş limiti gevşetilmedi. Lint hata vermedi; önceki SignalR/QR/ödeme bileşenlerinde 6 uyarı sürüyor. Uzak CI henüz çalıştırılmadı.

Dosya grupları: Modules/Admin, BranchHours, catalog/tax/version/StaffInvitation entities, DbContext/seed guard/migration'lar, Program/auth acceptance, frontend MerchantAdmin/AdminForms/InvitePage/adminTypes/CSS/routes/API, AdminTests ve yönetim E2E/oturum helper'ı, README/API/kararlar.

Sınırlar: kapasite bilgi amaçlıdır, rezervasyon limiti değildir; overnight saatler yok; ürün hard delete yerine satıştan kapatılır; otomatik davet e-postası yok; iade/iptal operasyonu uygulanmadı. Gerçek tahsilat, PWA ve demo deploy tamamlanmadı. Sıradaki dilim **Aşama 6**: kullanıcının sağlayıcı seçimi ve sandbox hesabı/anahtarlarıyla gerçek ödeme adaptörü. Secret'lar sohbet yerine yerel environment/config üzerinden sağlanmalı.

## Aşama 6 hazırlık kaydı — 13 Ekim 2026

Kullanıcı gerçek ödeme sağlayıcısını henüz seçmedi. Bu dilim sağlayıcıdan bağımsız hazırlığı tamamlar; Aşama 6'nın harici entegrasyonu tamamlanmış sayılmaz. iyzico Checkout Form/Marketplace ve PayTR iFrame/Platform Transfer resmî dokümanları kontrol edilerek `payment-provider-selection.md` karşılaştırması ve hesap modeli seçenekleri yazıldı. Sağlayıcı adına kayıt/başvuru veya harici ödeme API isteği yapılmadı.

Genel IPaymentProvider continuation/config sözleşmesi sandbox HMAC imzasından ayrıldı. PaymentProviders kurulu adaptörü startup'ta doğrular; sandbox/none dışında ayar varsa otomatik demo fallback yerine hata verir. Kayıtlı checkout tekrarı Payment.Provider üzerinden aynı session için devam yolu üretir; adaptör yoksa 503 ve sıfır yeni işlem. Sandbox session ve decision yalnızca sandbox kayıtlarına erişir.

WebhookProcessor sandbox imza/zaman/JSON doğrular; internal VerifiedPaymentEvent ortak PaymentSettlement'a geçer. Provider+event/ref ve order kilitleri, aynı transaction'da payment/order/history/outbox, tutar/currency ve terminal durum kontrolleri korunur. Sandbox geç ödeme reddi açık bir politika olarak ayrıldı; gerçek geç tahsilat/uzlaştırma henüz uygulanmadı. Yeni birebir tekrar testi ilk checkout expiresAt'ın PostgreSQL mikro saniye hassasiyetine eşitlenmesini sağladı.

Payments:Provider / PAYMENTS_PROVIDER ayarı README, örnek env, Compose ve local API scriptine eklendi. none modu public menüde checkoutEnabled=false döndürür; müşteri online ödeme kapalı uyarısı görür, ödeme butonu devre dışıdır, menü/sepet/quote korunur.

Doğrulama: **61 backend + 10 frontend + 17 hedefli ödeme/operasyon tarayıcı senaryosu = 88 başarılı kontrol**. Backend tam paket geçti. 17 tarayıcı aynı son çalıştırmada geçti; 16 gerçek API akışıdır, 1 kapalı ödeme menü bayrağını fixture ile değiştirir ve gerçek quote kullanır. Aşama 5'in 34 tarayıcı testi bu dilimde bütünüyle yeniden çalıştırılmadı; ödeme/operasyonla ilgili kapsam seçildi. Yeni 9 backend kontrolü config fallback reddi, secret'sız none, sandbox flag/secret, güvenli continuation token, none endpoint kapanması, farklı sağlayıcının simulator erişim reddi, eksik adaptörde aynı key ile yeni işlem üretilmemesi ve kayıtlı sağlayıcıya yönlendirmeyi kapsar. Önceki imza/idempotency/outbox ve tenant testleri tam pakette korunur.

.NET, TypeScript/Vite ve Docker build başarılı. Model değişimi ve yeni migration yok. Güncel demo stack çalışır; `/api/health` ready. Kapalı ödeme 390px sepet ekranı görsel QA'dan geçti. Harici sağlayıcı E2E veya uzak CI çalıştırılmadı.

Değişen gruplar: Modules/Payments IPaymentProvider/PaymentProviders/PaymentSettlement/WebhookProcessor/Checkout/Sandbox, Program registration/startup, public catalog checkoutEnabled, müşteri ödeme butonu/notice/API tipi, PaymentTests/PaymentProviderTests/e2e/payments, Compose/env/local script ve dokümantasyon.

Kalan girdi: sağlayıcı ve tahsilat modeli (kahvecinin kendi hesabı veya platform alt üye hesabı), sandbox hesabı/yerel secret config ve callback erişimi. Bunlar netleşince initialize/retrieve veya iFrame/callback adaptörü, timeout/idempotency/uzlaştırma ve harici sandbox testleri tamamlanacak. PWA, isteğe bağlı müzik ve deploy/performance/security cilası hâlâ sonraki kapsamdır. Gerçek tahsilat yok.

## iyzico seçim ve protokol istemcisi kaydı — 13 Ekim 2026

Kullanıcı iyzico ve her kahvecinin kendi hesabına tahsilat modelini seçti; sandbox hesabının hazır olduğunu belirtti. Checkout Form HTTP istemcisi resmî IYZWSv2/body HMAC ve response-signature alan sırasıyla yazıldı. Initialize/retrieve, tutar/currency/correlation/token kontrolleri, sandbox URL sınırı, fraud review ayrımı ve transaction-ID sonuç DTO'su eklendi. Initialize otomatik retry etmez; belirsiz sonuç işareti döndürür. Bu istemci henüz ödeme registry/checkout/callback/settlement akışına bağlanmadı.

TenantId bazlı IyzicoMerchantAccounts config lookup global/diğer işletme fallback'i yapmaz. Owner-only readiness endpoint key/secret döndürmez. Yerel secure-input script anahtarları ignored .env'ye işletme bazında yazar; demo modu korunur. Docker env_file ve hot reload API scripti bu anahtarları yükler. Sandbox anahtarları henüz kullanıcı tarafından eklenmedi.

Doğrulama: **80 backend testi tam pakette geçti** (19 yeni client/account kontrolü, önceki 61). 17 protokol testi taklit HTTP yanıtlarıyla exact-body auth, response imzası/tamper, decimal normalization, HTTPS/host, correlation, fraud review, eksik satır ve timeout kontrol eder; 2 account testi tenant izolasyonu ve live-key reddini doğrular. Mevcut AdminTests readiness rol/tenant/secret-redaction kontrolüyle genişletildi. Build ve Docker build geçti, setup script parser ve Compose config doğrulandı. Frontend değişmedi; bu dilimde E2E yeniden çalıştırılmadı. Harici iyzico isteği veya ödeme denemesi yapılmadı.

Kalanlar: yerel sandbox anahtarlarını ekleme, erişilebilir HTTPS callback, buyer alanları, kalıcı initialization/token/ref ve timeout/late-charge/fraud reconciliation state, müşteri ödeme sayfası + callback/verify/settlement bağlantısı, harici sandbox testi. Aşama 6 tamamlanmadı. Kullanıcı kurulumu `docs/iyzico-setup.md` içinde.

## Sandbox anahtar doğrulaması — 13 Ekim 2026

Kullanıcı kurulum scriptiyle Demo Kahve A anahtarlarını yerel .env'ye ekledi. Değerler çıktıya/log'a yazılmadan key/secret varlığı ve sandbox prefix kontrol edildi. API recreate sonrası owner readiness credentialsConfigured=true, configurationValid=true döndürdü. İşletme anahtarlarıyla iyzico sandbox `/payment/bin/check` kimlik doğrulama probu başarılı oldu; bu çağrı ödeme/tahsilat oluşturmadı. Client initialize/retrieve harici ödeme E2E hâlâ yapılmadı; checkoutConnected=false.

Kullanıcı kullanılabilir HTTPS adresi olmadığını belirtti. Kalan checkout/callback/initialization-reconciliation kodu ve erişilebilir HTTPS test adresi tamamlanacak. Anahtarlar yalnızca ignored .env ve API environment içinde; gerçek key/secret çıktıya veya dokümana eklenmedi.

## Geçici HTTPS callback geçidi — 13 Ekim 2026

Kullanıcı geçici bağlantıyı yetkilendirdi. Resmî Cloudflare cloudflared 2026.10.0 Windows amd64 binary'si proje .local dizinine indirildi; SHA256 GitHub release digest'iyle doğrulandı. Node loopback5190 callback gateway + Cloudflare Quick Tunnel gizli background süreçleri çalışır. Genel uygulama portu paylaşılmadı.

Gateway 4 testi geçti: kapalı admin/auth/menu/file yolları; yalnızca sabit yerel API'ye form forwarding; header/cookie/response-body sınırı; content type/body size/rate limit. Dış HTTPS sağlık kontrolü 200; /api/session/me, /admin, /b/demo-moda ve /.env 404 olarak doğrulandı. Tünel URL'si/PID'ler ignored runtime state'te; callback origin mevcut anahtarlar/ödeme modu korunarak ignored .env'ye kaydedildi. Key veya token çıktıya yazılmadı.

Başlatma ve yalnızca proje süreçlerini kapatma scriptleri docs/payment-tunnel.md içinde. Geçici adres süreç kapanınca geçersizleşir ve yeniden başlatmada değişir. Public gateway callback ACK içindir; tarayıcı dönüş/redirect sözleşmesi callback uygulamasına bağlanırken tamamlanacak.

HTTPS bağlantısı kurulması uçtan uca iyzico ödemenin tamamlandığı anlamına gelmez. Checkout/callback/kalıcı initialization ve uzlaştırma kodu hâlâ bağlanacak; mevcut yerel sandbox demo korunur. Bu dilimde gerçek/sandbox ödeme oluşturulmadı.

## iyzico sandbox uçtan uca bağlantı — 13 Ekim 2026

Önceki “henüz bağlı değil” kayıtlarını bu güncelleme geçersiz kılar. İşletmeye özel iyzico provider registry/checkout bağlantısı, alıcı formu/hosted yönlendirme, kalıcı initialization state, şifreli provider token/URL, callback → imzalı retrieve → settlement ve background uzlaştırma tamamlandı. Migration çalışan DB'ye uygulandı. Yeni checkout modu yerel `.env` içinde iyzico; Demo A kendi sandbox hesabını kullanır, anahtarı olmayan Demo B checkout'u kapalıdır.

Başlatma kilidi aynı checkout için eşzamanlı ikinci harici oturumu engeller. Açık sağlayıcı reddi Rejected ile düzeltilebilir; timeout/belirsizlik yeni ödeme başlatmayı engeller. Fraud review Paid değildir. Bilinen token'lı Pending kayıtlar son 7 gün içinde 30 saniyelik sınırlı worker ile kontrol edilir. Token alınamayan Unknown yalnızca doğrulanan callback ile kurtarılabilir; gelmezse manuel sağlayıcı kontrolü gerekir. Müşteri ekranı PII'yi storage'a yazmaz; DB'de alıcı/adres veya kart alanı yoktur. Geç ödeme callback'i yerel süre aşımından bağımsız doğrulanır. Gateway yalnızca allowlist local receipt dönüşüne 303 verir.

Dış iyzico sandbox üzerinde gerçek hesap anahtarlarıyla test kartı + mock 3D Secure + public HTTPS callback + yerel receipt → canlı takip zinciri tamamlandı. 140 TL Flat White sipariş #0118: DB Payment=Paid, Order=Received; tek outbox kaydı ve tek processed kayıt. Müşteri ekranı “Sipariş alındı” gösterdi. Gerçek para çekilmedi. İlk denemeler sağlayıcının e-posta formatı hata 5 ile reddettiği test adresi yüzünden başarısızdı; resmî örnek adresle düzeldi. Belirsiz eski oturum yeniden tahsilat için kullanılmadı.

Doğrulama: 89 backend + 10 frontend + 5 gateway testi başarılı. Tam 40 tarayıcı senaryosu bu dilimde iyzico global moduna geçmeden önce geçti; son reddetme UX değişimi sonrası iyzico fixture paketinin 5 senaryosu yeniden geçti (320/390/1440px, review, unknown). Harici sandbox testi bunlardan ayrı manuel tarayıcı kontrolüdür. Docker/.NET/TypeScript/Vite build başarılı. Uzak CI ve canlı ödeme kabul testi yapılmadı.

Kod grupları: IyzicoPaymentProvider/Flow/ReconciliationWorker/CheckoutClient, Payment state/migration, Program registry, checkout/catalog/sandbox sorguları, IyzicoPayment route/form/receipt, callback gateway redirect, client/flow/UI/gateway testleri. Kurulum/API/state/provider/README belgeleri güncellendi. Görsel kanıt ignored `.local/iyzico-paid-proof.png` içinde.

Kalan genel MVP kapsamı: PWA/offline cilası, isteğe bağlı müzik, deploy/performance/security kabulü. Ödeme için canlı hesap/kalıcı HTTPS, iade/iptal ve işletme manuel uzlaştırma ekranı sonraki kapsamdır. Geçici URL ve servisler açık tutulmalıdır; yeniden açılışta origin güncellenip API recreate edilir. Tam üretim yayını hazır olduğu iddia edilmez.

## Premium menü ve ilk fotoğraf koleksiyonu — 13 Ekim 2026

Kullanıcı görselleri doldurma, premium tasarım, menü çeşitliliği ve DB'den metin tabanlı şarkı önerileri için yol haritasını istedi; ardından başlamayı yetkilendirdi. İlk dilim görsel altyapısı + müşteri menüsü + 12 ürün koleksiyonu olarak uygulandı. Şarkı modülü bu dilimde uygulanmadı.

Built-in image_gen ile 12 ayrı ürün fotoğrafı üretildi; orijinaller ignored .local/menu-source içinde, optimize 960px WebP dosyaları web/public/images/menu içinde. Tutarlı sıcak krem taş fon/doğal ışık/seramik; contact sheet görsel kontrolü yapıldı. Dosyalar 88–140 KB. Tam istemler docs/menu-image-prompts.md içinde. Demo arayüzü görsellerin temsili olduğunu açıkça belirtir.

Product ImagePath/ImageAlt migration, public/admin DTO, Owner-only version/CSRF/tenant kontrollü upload ve detach, gerçek raster decoder/format/5 MiB/16 MP sınırları, yön düzeltme/metadata temizliği/WebP normalize ve public tenant-image eşlemesi eklendi. API non-root çalışır; kalıcı Docker volume uygulama kullanıcısına aittir. Kestrel/nginx upload sınırı yalnızca gereken boyuta ayarlandı; diğer API request sınırı 64 KB sürer. Yanlış tenant/rol/CSRF/version reddi ve detach test edildi. Eski görseller fiziksel silinmez; depolama kotası/garbage cleanup production kapsamında kalır.

İdempotent marker ile demo katalog 5'ten 12 ürüne çıktı: espresso, cappuccino, iced latte, matcha, kruvasan, brownie ve San Sebastian eklendi. Altı kategori. Mevcut owner ürün/fiyat/uygunluk değişiklikleri üzerine yazılmaz; eski sipariş snapshot'ı korunur. Tatlı/fırın ürünleri kahve seçenekleri almaz. Yeni ürün sırası mevcut ürünlerin maksimum sırasından sonra atanır.

Fotoğraflı responsive menü, krem/espresso/olive tonları, serif başlıklar, yatay sticky kategoriler, ürün detay fotoğrafı ve görsel hatasında kullanılabilir fallback eklendi. Admin görsel yüklemesi hemen kaydedilir; formun diğer taslak alanları ve version korunarak son Kaydet çalışır. Desktop hero yüksekliği ve mobil metin yoğunluğu görsel QA ile azaltıldı.

Son doğrulama: 94 backend + 10 frontend testi geçti. 29 tarayıcı senaryosundan 28'i ilk pakette geçti; volume yazma yetkisi düzeltildikten sonra kalan upload/version senaryosu geçti. Son hero/görsel kadraj/form reset cilası sonrası premium paketin 6 senaryosu yeniden geçti. .NET/TypeScript/Vite/Docker build başarılı; mevcut DB migration ve API/web güncellendi. Lint hatası yok; önceden var olan 6 uyarı sürüyor. Yeni testler paid-flow protokolünü değiştirmedi; iyzico modu korunuyor. Bu dilimde dış iyzico kart ödemesi tekrar yapılmadı.

Manuel Cua tarayıcı QA: 390px mobil ve 1440px masaüstü, gerçek DB'den 12 fotoğraf/6 kategori; Brownie görseli dahil son satır kontrol edildi. Kanıt .local/premium-menu-mobile.png ve .local/premium-menu-desktop.png. Tarayıcı yeniden normal boyutuna alındı ve menü açık bırakıldı. Kalan hedefler 24 ürün koleksiyonu, takip/ödeme tasarımını genişletme, DB şarkı modülü ve PWA/yayın cilası; bu özellikler yapılmış sayılmaz.

## Canlı responsive menü ve 24 ürün — 13 Ekim 2026

Kullanıcı daha canlı, responsive, vurucu ama sade görünüm ve geniş ürün yelpazesini istedi. Hero sıcak kayısı tonlarına, güçlü sans başlık + serif vurguya ve doğrudan menüye götüren CTA'ya dönüştü. Mobil üst alan daha kısa; renkli ikonlu kategori düğmeleri ürün sayısını gösterir, seçili durum koyu kontrastla ayrılır. Ürün seçim butonları terracotta oldu. Katalog görüntüsü kategorilere göre kararlı gruplanır; mevcut DB category/product sıraları veya fiyatlar değiştirilmez. Tablet/desktop kartları 2/3 sütun; dar mobil yatay ürün kartı.

Yeni demo marker 3 ile 12 ürün daha eklendi: Mocha, Iced Americano, Espresso Tonic, V60, Chemex, Chai Latte, Sıcak Çikolata, Çay, Bademli Kruvasan, Cookie, Tarçınlı Çörek, Limonlu Cheesecake. Toplam 6 kategoride 24 ürün ve 24 fotoğraf. Mevcut owner fiyat/görsel/satış ayarları ve sipariş snapshot'ları korunur; tekrar seed kopya üretmez. Uygun içecekler boy/süt seçeneği alır, çay/fırın/tatlılar zorunlu kahve seçeneği almaz. Ek 12 fotoğraf built-in image_gen ile ayrı ayrı üretildi; orijinaller .local/menu-source, 960px WebP'ler apps/web/public/images/menu altında. Yeni dosyalar 77–146 KB; contact sheet kontrolü yapıldı. İstemler docs/menu-expansion-prompts.md içinde.

Doğrulama: 94 backend, 10 frontend testi geçti. 34 tarayıcı senaryosu tam seçili regresyon paketinde geçti; son kategori gruplama değişiminden sonra premium paketin 11 senaryosu yeniden geçti. 320/390/600/768/1024/1440px menü/picker/sepet taşma kontrolü, 24 fotoğraf, dört fırın ürünü filtreleme, hero CTA, yeni sıcak çikolata boy+yulaf sütü quote=205 TL, görsel hata fallback'i ve admin upload/version save test edildi. Lint hatası yok, önceden var olan 6 uyarı devam ediyor. .NET/TypeScript/Vite/Docker build başarılı. API/web güncellendi; mevcut iyzico modu korundu. Bu dilimde yeni dış ödeme yapılmadı.

Manuel Cua QA: 390px telefon ve 1440px masaüstü görsel kontrolü, soğuk kahve kategorisi 4 ürün ve Espresso Tonic doğal görsel genişliği 960px doğrulandı. Kanıt .local/lively-menu-mobile.png ve .local/lively-menu-desktop.png; geçici viewport kaldırıldı, menü açık bırakıldı. Şarkı modülü/ödeme-takip tasarımını genişletme/PWA ve canlı yayın bu tarihte sonraki kapsamdaydı.

## Metin tabanlı şarkı önerileri — 15 Ekim 2026

Kullanıcının devam isteğiyle yol haritasındaki şarkı modülünün ilk çalışan sürümü tamamlandı. Songs/BranchSongs/BranchMusic/SongRequests migration, tenant filtreleri ve composite foreign key'ler, sipariş başına unique öneri, CSRF/rate limit ve yönetimle ortak transaction/advisory lock eklendi. Ödenmiş sipariş capability'si kendi şubesinin açık havuzunu görebilir. Aynı şarkı retry'ı aynı kaydı döndürür; ikinci farklı seçim reddedilir. Owner dışı yönetim ve farklı tenant/şubeye erişim reddedilir.

48 yerel demo metadata kaydı her demo işletmesi için ayrı seed edildi. Marker 4 tekrar başlangıçta işletmenin yaptığı değişiklikleri korur. Ad/sanatçı aksan ve Türkçe karakter toleranslı aranır (en fazla 40 sonuç). Müşteri takip ekranında arama/seçim/gönderim ve 15 saniyelik durum yenileme; Owner Müzik sekmesinde şube aç/kapat, havuz, elle şarkı ekleme ve son 100 öneriyi kabul/reddetme. Müzik query hatası sipariş fişi/takibini engellemez. Ses/player/söz/kapak veya harici hesap/metadata servisi yok.

Doğrulama: 99 backend testi (5 yeni müzik entegrasyonu dahil), 10 frontend testi ve 10 tarayıcı testi (5 müzik + 5 mevcut iyzico UI) geçti. 320/390/1440px taşma, arama, tek gönderim UI, hizmet hatası izolasyonu ve yönetici moderasyonu kontrol edildi. Lint hatasız; önceki 6 uyarı sürüyor. .NET/TypeScript/Vite ve Docker build başarılı; mevcut DB migration/seed ve API/web deployment tamamlandı. DB'de işletme başına 48 şarkı ve health=ready doğrulandı. Dış kart ödemesi tekrarlanmadı, iyzico ayarları korundu.

Gerçek mevcut #0118 siparişinde Cua ile PostgreSQL kataloğunun gelmesi, Norah Jones araması ve Sunrise seçimi görüldü; mevcut sipariş adına öneri gönderilmedi. Kanıt .local/music-catalog-proof.png. Takip sekmesi kullanıcının incelemesi için açık. 100–200 şarkıya genişletme, CSV, sıralama/tarih aralıklı müzik raporu, takip/ödeme tasarımını genişletme ve PWA/yayın kalan kapsam. Kullanım/API: docs/music.md.

## Ödeme/takip tasarımı ve kapsam kararı — 15 Ekim 2026

Kullanıcı abonelik ve işletmenin kendi tasarımını kapsamlı özelleştirmesi fikrini sonraya bıraktı; öncelik mevcut MVP'yi tamamlamak. Karar docs/future-subscription.md içinde; önceki konuşmanın paket/fiyat örnekleri kesin ürün kararı sayılmaz.

Müşteri takip ekranı sıcak kayısı zemin, belirgin durum açıklaması/teslim numarası, canlı bağlantı, aşama çizgileri ve ayrı sipariş özeti ile yenilendi. Şarkı alanı kendi kartında devam eder. Ödenmemiş siparişte ödeme doğrulaması/hazırlık aşamaları/şarkı seçimi gösterilmez. iyzico ekranı aynı premium kart ve ortak ReceiptItems ile ürün/seçenek/tutar özeti gösterir; alıcı formu genişte iki sütun, mobilde tek sütun ve autocomplete/inputMode ile düzenlendi. Sandbox uyarıları ve review/unknown/expired/failed korumaları sürer; ödeme başlatma, callback veya tutar protokolü değişmedi.

Doğrulama: TypeScript/Vite/Docker web build ve lint başarılı (önceden var olan 6 uyarı). 25 tarayıcı testi geçti: 4 yeni takip senaryosu, 5 müzik, 5 iyzico ve 11 premium menü/sepet/görsel testi. Takip/ödeme 320/390/1440px, menü 320–1440px doğrulandı. Gerçek #0118 takip ekranı Cua'da ve iyzico 390px form test görüntüsü görsel incelendi. Kanıt .local/tracking-redesign.png ve apps/web/test-results/iyzico-form-390.png. Web container güncellendi, API/DB/iyzico yapılandırması korunuyor. Yeni dış kart ödemesi yapılmadı. Backend kodu bu dilimde değişmedi; önceki 99 test sonucu geçerli.

Mevcut ürünün kalan ana işleri: PWA/ağ kesintisi deneyimi, yayına hazırlık ve kalıcı HTTPS/canlı ödeme kabulü. Favori/tekrar sipariş ve şarkı CSV/rapor genişletmeleri ek ürün işleri olarak ayrı değerlendirilebilir. Üretim yayını tamamlandı iddiası yok.

## Menüden “Sıraya bir şarkı bırak” — 15 Ekim 2026

Kullanıcı isteğiyle şarkı önerisi ödeme sonrasından çıkarıldı. Menünün hero altına “Sıraya bir şarkı bırak” düğmesi eklendi; katalog Dialog içinde açılır, ad/sanatçı aranır ve “Sıraya bırak” ile gönderilir. Sipariş, hesap veya ödeme gerektirmez. Takip ekranındaki müzik bileşeni kaldırıldı. İşletme onayı devam eder; çalınma garantisi verilmez.

GET/POST /api/branches/{slug}/music, anonim HttpOnly/SameSite Strict tarayıcı çerezi, DB hash ve tarayıcı/şube başına 10 dakikalık bekleme; IP başına 30 POST/dakika, CSRF ve aktif havuz kontrolü eklendi. Aynı parça retry'ı mevcut kaydı döndürür; farklı ikinci parça bekleme bitene kadar reddedilir. Çerez tabanlı sınır bir kişi kimliği değildir, çerez silinerek aşılabilir; ek IP limiti sürer. SongRequests.OrderId nullable + GuestKeyHash + kaynak CHECK/index migration eski önerileri korur. Owner listesi left join ile siparişsiz önerileri de getirir ve “Menüden öneri” gösterir. Eski sipariş endpoint'leri uyumluluk için durur; güncel UI çağırmaz.

Doğrulama: 102 backend ve 25 tarayıcı testi geçti. 3 yeni backend testi ödeme olmadan gönderim, eşzamanlı tekrar, bekleme süresinin bitmesi, başka tarayıcı gizliliği, farklı şube/tenant/havuz, CSRF, çerez ve kapalı modülü kapsar. Menüden 320/390/1440px arama/seçim/gönderim sırasında checkout çağrılmadığı test edildi; mevcut menü/sepet/iyzico/takip regresyonları geçti. .NET/TypeScript/Vite/Docker build ve lint başarılı (önceki 6 uyarı). Migration uygulandı, API/web güncellendi. Cua'da gerçek katalogla menü düğmesi ve Norah Jones araması görsel doğrulandı; canlı öneri gönderilmedi. Kanıt .local/menu-music-dialog.png. Abonelik askıda kalır.

## Şarkı aramasında odak kaybı düzeltmesi — 15 Ekim 2026

Kullanıcı her harften sonra takılma ve Still Loving You sonucunun bulunmamasını bildirdi. Query key değişince data geçici undefined oluyor ve koşullu canRequest bloğu arama input'unu unmount ediyordu. keepPreviousData ile sorgu geçişinde mevcut ekran/input korunur; eski istekler AbortSignal alır, geçici eski sonuçta gönderim kapatılır. Gönderim sonrası tüm şube/arama cache'leri invalid edilir. UI aramanın şubenin yerel kataloğunda olduğunu açıklar; bulunamayan kayıt mesajı kapsamı belirtir.

Demo seed marker 5, Still Loving You — Scorpions kaydını mevcut 48'e ekler. Marker 4'ü yeniden çalıştırmaz, mevcut aktiflik ve owner ayarlarını değiştirmez. Her demo işletmesinde 49 kayıt DB'den doğrulandı. Harici müzik araması/API entegrasyonu yok.

Doğrulama: müzik modülünün 9 backend entegrasyonu ve 6 tarayıcı testi geçti. Yeni test her harfi normal klavyeyle yazar, debounce ve gecikmeli sunucu yanıtı sonrası input'un değerini/odağını kontrol eder; boş sonuçtan ve arama temizlemeden sonra da odağı doğrular. Gerçek DB'de başlık ve sanatçı araması test edildi. Cua'da Still Loving You/Scorpions tek sonucu ve focused=true görüldü. Kanıt .local/music-search-fixed.png. .NET/TypeScript/Vite/Docker build başarılı, lint önceki 6 uyarıyla hatasız. API/web güncellendi. Tam backend paketi bu küçük düzeltmede tekrarlanmadı; önceki 102 test kaydı tarihsel olarak geçerli.
## Katalog dışı metin önerisi ve arama bağlantıları — 15 Ekim 2026

Menü müzik penceresine Katalogdan seç/Kendin yaz yöntemleri eklendi. Serbest şarkı/sanatçı metni 1–240 karakter olarak gönderilir, Pending kaydı oluşturur ve kataloğa otomatik eklenmez. Müşteri seçimi/önerisi ve Owner gelen listesinde Spotify’da ara/YouTube’da ara sabit HTTPS arama bağlantıları bulunur; URL encoding, yeni sekme ve noopener noreferrer uygulanır. API anahtarı, hesap bağlama veya otomatik çalma yoktur. MusicBrainz toplu katalog içe aktarma henüz uygulanmadı.

FreeTextMusicRequests migration eski kayıtları korur; nullable SongId, RequestedText, seçim CHECK ve tenant/şube FK ekler. Left join sayesinde metin önerileri müşteri ve işletmede görünür. Moderasyon, CSRF, açık modül, tenant/şube sınırı, anonim çerez ve 10 dakika bekleme korunur. Aynı normalize metnin eşzamanlı tekrarları tek kayda gider, farklı metin/katalog seçimi aynı beklemeye tabidir. Migration geri alımı metin kayıtları çözülmeden engellenir.

Doğrulama: 11 müzik backend entegrasyonu ve 9 tarayıcı testi başarılı. 320/390/1440px metin girişi, boş gönderim, URL encoding, metin submit ve onay, Owner moderasyonu ve harf harf arama odağı kontrol edildi. .NET/TypeScript/Vite/Docker build başarılı; lint hatasız (önceki 6 uyarı). API/web güncellendi, migration başlangıçta uygulandı. Görsel kanıt apps/web/test-results/music-free-text-390.png. Gerçek müşteri adına öneri gönderilmedi; dış sitelerde arama açılmadı. Abonelik askıda.

## MusicBrainz dosyasından katalog aktarımı — 15 Ekim 2026

Kullanıcı canlı ticari/startup erişimi olmadığını belirterek dosyadan aktarım hazırlığını seçti. Owner Müzik ekranına 500 kayıt / 512 KB JSON dosyası seçimi, sunucuda önizleme, yeni/mevcut/şube sayıları ve açık aktarım düğmesi eklendi. JSON recording dizisi veya recordings zarfı kabul edilir; yalnızca CC0 temel alanlar (recording MBID, başlık, artist-credit) alınır. Kaynak kimliği çevrimiçi doğrulanmaz; dosyanın kaynağı işletme tarafından doğrulanmalıdır. Canlı MusicBrainz API çağrısı ve otomatik veri indirme yoktur.

Songs.MusicBrainzRecordingId ve tenant unique index migration eklendi. İçe aktarma Owner/CSRF/tenant/şube, admin transaction/lock ve audit ile korunur; bütün dosya yazmadan önce doğrulanır. Tekrar ID/ad/sanatçı ayıklanır; mevcut başlıklar ve açık/kapalı şube seçimleri korunur. Yeni şube kayıtları kapalı başlar; işletme katalogdan açabilir. Müşteri katalogdan öneri, metin önerisi ve Spotify/YouTube bağlantıları sürer.

scripts/prepare-musicbrainz-catalog.py yerel JSON/JSONL/sıkıştırılmış JSONL/recording JSONL tar arşivlerinden sanatçı filtresiyle küçük dosya üretir. Tam dökümü belleğe almaz, arşiv yollarını diske çıkarmaz, yalnızca temel alanları yazar; mevcut çıktıyı ezmez. docs/musicbrainz-import.md biçim, kaynak, hazırlama ve kullanım rehberidir. Gerçek kaynak dosyası henüz yüklenmedi; mevcut 49 demo kayıt korundu. Abonelik askıda.

Doğrulama: 14 backend testi (3 yeni aktarım testi dahil), 13 tarayıcı testi (4 yeni dosya/önizleme/aktarım/aktivasyon testi dahil) ve 4 Python hazırlama testi geçti. Eşzamanlı tekrar, salt önizleme, hatalı dosyada bütün aktarımın reddi, mevcut seçimin korunması, rol/tenant/CSRF, 320/390/1440px ve aramada odak kaybı regresyonu kapsandı. .NET/TypeScript/Vite/Docker build başarılı; lint önceki 6 uyarıyla hatasız. API/web güncellendi, health=ready. Mobil aktarım önizlemesi görsel incelendi: apps/web/test-results/musicbrainz-import-390.png. Canlı müşteri önerisi veya gerçek kaynak aktarımı yapılmadı.

## Siparişe bağlı metin şarkı isteği — 19 Ekim 2026

Kullanıcı dış katalog, API ve oynatıcı işini kaldırıp sipariş sırasında metin isteği ve işletme için İstek parçalar paneli istedi. Müşteri sepetine isteğe bağlı 240 karakter şarkı/sanatçı alanı eklendi. Checkout requestedSong'u trim eder; sipariş/ödeme/istek aynı transaction içinde oluşturulur. OrderId unique kayıt aynı checkout retry'ında ikinci istek yaratmaz. Metin checkout fingerprint'ine dahildir; farklı metin aynı key ile reddedilir. Şarkı olmayan eski checkout fingerprint'i korundu. Mevcut tablo CHECK/FK yapısı uygun olduğu için yeni migration gerekmedi.

İstek ödemesi Paid olduğunda görünür; yarım kalan/başarısız/iptal siparişler panele çıkmaz. Owner /admin → İstek parçalar, Barista /staff sipariş panosu altında aynı şubenin listesini görür. Son 100 kayıt, sipariş no/metin/tarih/durum, varsayılan yalnızca yeni filtre, 5 saniye yenileme ve kabul/reddetme eklendi. GET/PUT /api/staff/branches/{branchId}/song-requests Owner/Barista, tenant/şube yetkisi, CSRF, Pending/version, ortak admin lock/transaction ve audit ile korunur. Kabul müzik çalmaz veya başka servise sıra eklemez.

Menü müzik düğmesi, katalog seçimi/aktarımı, Spotify/YouTube bağlantıları ve eski public branch/track music endpoint'leri kaldırıldı. Eski katalog/migration/veri ve Owner uyumluluk endpoint'leri korunur, güncel UI kullanmaz; veri silinmedi. MusicBrainz hazırlığı ve rehberi arşiv kapsamındadır. Abonelik kullanıcı kararıyla kapsam dışı olarak kaydedildi.

Doğrulama: tam backend paketi 101 test başarılı; 28 tarayıcı testi (8 yeni sipariş isteği, 5 iyzico, 4 takip, 11 premium menü) başarılı. Eşzamanlı checkout, isteğe bağlı boş metin, maksimum uzunluk, yalnızca Paid görünürlüğü, sipariş numarası, tenant/personel erişimi, CSRF, stale karar, eski public uçların kapanması ve 320/390/1440px doğrulandı. Tarayıcıda metnin HTML olarak çalışmadığı ve baristanın reddedebildiği test edildi. .NET/TypeScript/Vite/Docker build ve lint başarılı; önceki 6 lint uyarısı sürer. Docker başlatıldı, API/web güncellendi, health=ready. Mobil sepet ve panel görsel incelendi: apps/web/test-results/order-song-cart-390.png, order-song-panel-390.png. Gerçek dış kart ödemesi ve canlı müşteri adına şarkı isteği gönderilmedi.

## Tasarım ve ürün deneyimi raporu — 19 Ekim 2026

Kullanıcı genel rapor ve önceliklendirilmiş yol haritası istedi. docs/product-experience-roadmap-2026-10-19.md oluşturuldu; README'den bağlandı, eski premium plan tarihsel olarak işaretlendi. Müşteri menüsü Cua'da görsel incelendi; menü/ürün/sepet/ödeme/takip/personel/istek akışlarının kodu ve son test kayıtları okundu. Rapor kesin bug, koddan çıkan risk ve tasarım önerisini ayırır; normal/hata akışları, olmaması gerekenler, P0/P1/P2 uygulama sırası, bağımlılıklar, çıkış ölçütleri, ek özellikler ve pilot ölçümleri içerir.

Öne çıkanlar: Header'ın sabit Moda yönlendirmesi, demo/canlı metin ayrımı, şarkı taslağının yenilemede kaybı, müşterinin takipte istek onayı görmemesi, son 100 kayıt filtresinde eski Pending görünürlüğü riski ve takipte eski veri/güncellik uyarısı. Mobil hero/sepet/tek ana ödeme eylemi ve personel geri bildirimi önerileri eklendi. Abonelik iptal, kapsamlı editör ve dış müzik entegrasyonları kapsam dışı tutuldu. Uygulama kodu değiştirilmedi; bu rapor sırasında testler tekrar çalıştırılmadı, önceki 101 backend/28 tarayıcı sonucu tarihli kanıt olarak belirtildi. Yol haritasının hazırlanması uygulama dilimlerini otomatik başlatmadı.

## Deneyim yol haritası — ilk düzeltme paketi — 19 Ekim 2026

Kullanıcı rapor doğrultusunda düzeltmeleri başlattı. Header menü rotasındaki slug'ı veya ödeme/takip sayfasının mevcut query cache'indeki doğrulanmış şubeyi kullanır; sabit Moda bağlantısı kaldırıldı. Şubesi henüz bilinmeyen ödeme/takipte marka tıklanabilir değildir, başka şubeye tahminle yönlenmez. Şarkı taslağı şubeye özel sessionStorage'da yenilemede korunur; doğrulanmış ödemede ilgili taslak/sepet temizlenir, başarısız girişimde korunur. Depolama engelliyken form kullanılabilir.

Capability takip yanıtına yalnızca Paid ve aynı order/tenant/branch'e ait metin/karar eklendi; müşteride alındı/kabul/red ve çalınma garantisi olmadığı görünür. Takipte son doğrulama saati, hata/offline durumunda eski veri açıklaması ve yenileme eylemi eklendi. Rapordaki B06 netleştirildi: genel FetchError zaten vardı; eksik açık güncellik bağlamıydı.

Sepette tek birincil Ödemeye devam eylemi quote'u sunucuda kontrol eder. Tutar değişmemişse checkout açılır; değişmişse güncel tutar gösterilir ve müşteri yeniden onaylamadan checkout çağrılmaz. Server checkout kontrolü/idempotency korunur. Sepeti doğrula isteğe bağlı ikincil eylem olarak kaldı. Başlatılan isteğin payload/key/song snapshot'ı kullanılır, işlem sırasında sepet düzenleme kapalıdır. Mobilde alt eylem alanı sticky; textarea/select odak stili genişletildi. Müşteri sepet/takip demo metinleri demo bilgisiyle koşullandı; API/iyzico hâlâ sandbox, canlı ortam/personel metni ayrımı tamamlandı iddiası yok.

Doğrulama: ilgili 24 backend entegrasyonu, 12 frontend birim testi ve 41 tarayıcı testi geçti. 6 yeni tarayıcı senaryosu branch logo/taslak izolasyonu, 320/390/1440px tek ana ödeme, fiyat değişiminde açık onay, takipte eski veri/istek kararı ve toparlanmayı kapsar. Mevcut menü/sepet/iyzico/şarkı/takip regresyonları geçti. TypeScript/Vite/.NET/Docker build başarılı; lint önceden var olan 6 uyarıyla hatasız. API/web güncellendi, health kontrol edildi; mobil yeni sepet görsel incelendi. Önceki tam 101 backend sonucu tarihsel kayıt; bu pakette kapsamlı ilgili alt paket çalıştırıldı. Gerçek dış kart ödemesi yapılmadı.

Kalan: B02'nin sunucu/personel ortam modelini ayırma, B05 istek sunucu filtre/sayfalama, B08 sayaç, sepette seçenek düzenleme, hero/ürün keşfi ve pilot/yayın. Abonelik ve dış müzik entegrasyonları kapsam dışı. Aktif yol haritası docs/product-experience-roadmap-2026-10-19.md içinde güncellendi.

## Deneyim yol haritası — istek paneli filtre/sayfalama ve sayaç — 19 Ekim 2026

Kullanıcı devam istedi. B05/B08 uygulandı: sunucuda Pending/Accepted/Rejected/All filtresi ve sayfalama, varsayılan 20 kayıt; filtre öncesi şubedeki tüm Paid/iptal edilmemiş metin isteklerinden bekleyen sayısı. RepeatableRead ile count/sayfa aynı snapshot'ta okunur, eşit zamanlarda ID sıralaması kararlıdır. Son 100 sınırı kaldırıldı. Son sayfa boşalırsa son geçerli sayfaya clamp edilir. Yanıt `{items,pendingCount,total,page,pageSize,totalPages}`; açık eski istemci sekmeleri yenilenmelidir.

Panelde durum seçimi, Önceki/Sonraki, sayfa/toplam, bekleyen rozeti ve karar sonrası sipariş numarasıyla onay mesajı eklendi. Filtre değişince sayfa 1'e döner. Personel panosunun üstünde sayaçlı İstek parçalar kısayolu var; panel ve kısayol aynı Pending ilk sayfa query cache'ini paylaşır. Şube seçimi eski şubenin sayısını göstermez; sayaç hatasında güncellenemedi mesajı verir. Owner/Barista/şube/CSRF/version ve ödeme korumaları sürer.

Doğrulama: 5 backend müzik entegrasyonu ve 15 tarayıcı testi başarılı. Yeni 145 kayıt testi 120 yeni işlenmiş kaydın gerisindeki 25 eski Pending isteğin iki sayfada erişimini, tekrar ID olmamasını, filtre/sayıları, geçersiz parametreleri ve son sayfadaki 5 karar sonrası geri düşmeyi doğrular. Tarayıcıda 320/390/1440px görünüm, karar bildirimi, durum filtresi, sayfa gezinmesi, sayaç güncellenmesi ve önceki deneyim regresyonları geçti. .NET/TypeScript/Vite/Docker build başarılı; lint önceki 6 uyarıyla hatasız. API/web güncellendi, health=ready. Mobil panel görsel incelendi. Gerçek müşteri adına karar/şarkı isteği gönderilmedi.

Sonraki ana iş: sepette ürün seçeneklerini düzenleme, ardından hero/keşif ve kalan demo/canlı ayrımı/pilot hazırlığı. Abonelik ve dış müzik entegrasyonları kapsam dışı. Güncel kullanım docs/music.md, yol haritası docs/product-experience-roadmap-2026-10-19.md.

