# iyzico sandbox kurulumu

Checkout Form, her kahvecinin kendi hesabıyla bağlandı. TenantId bazında anahtar seçilir; global/başka işletme fallback'i yoktur. Yalnızca sandbox anahtarları, API ve hosted ödeme host'u kabul edilir.

## Kurulum

Proje kökünde Demo Kahve A için:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\set-iyzico-sandbox.ps1 -TenantId 03237922-080c-43ac-82a4-5227867ee823
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-payment-tunnel.ps1
```

İlk script API Key ve Secret Key'i gizli girişle alır ve ignored `.env` dosyasına işletmeye özel yazar. Anahtarları sohbete veya kaynak koda eklemeyin. Demo B için TenantId `ece6811d-5fc5-41d2-b15a-d18dc6f1fbfc` kullanılır.

Tünel yalnızca callback geçidini açar; origin'i `.env` içine `IYZICO__CALLBACKORIGIN` olarak kaydeder. `.env` içinde `PAYMENTS_PROVIDER=iyzico` seçin:

```powershell
docker compose up -d --build --wait
```

Owner-only GET `/api/admin/payments/iyzico` kendi tenant'ının credentialsConfigured/configurationValid/checkoutConnected durumunu verir; secret döndürmez. Bu alanlar dış tünelin sağlığı veya ödeme başarısının garantisi değildir. Demo A yapılandırıldı; anahtarı olmayan Demo B'nin checkout'u kapalıdır.

Geçici URL bilgisayar ve tünel açıkken çalışır, yeniden başlatmada değişebilir. Devam eden ödemeler sırasında kapatmayın. Yeni origin sonrası API recreate gerekir. [Callback geçidi](payment-tunnel.md).

## Akış ve güvenlik

Checkout fiyat/ürün snapshot'ını sunucuda sabitler. `/iyzico/pay/{capability}` alıcı bilgilerini ister; kart girişi yalnızca HTTPS sandbox-cpp.iyzipay.com üzerinde yapılır. Alıcı/adres verisi iyzico'ya iletilir, sipariş kaydında ve browser storage'ında saklanmaz.

Initialize tam UTF-8 body üzerinden IYZWSv2 ile doğrulanır; yanıt conversation/token imzası ve hosted URL host'u denetlenir. Token/URL DB'de şifrelidir. Transaction kilidi eşzamanlı ikinci initialize'ı engeller. Açık sağlayıcı reddi Rejected olur; bilgiler düzeltilip aynı checkout tekrar denenebilir. Timeout veya belirsiz sonuç Unknown/Initializing olur; yeni tahsilat ve otomatik initialize retry engellenir.

Callback token'ı tek başına kanıt değildir. Server-side retrieve kendi işletme anahtarlarıyla yapılır; response imzası, token/conversation/basket, TRY/tutar, fraudStatus ve snapshot satırları doğrulanır. Yalnızca fraudStatus=1 ve tutarlı sonuç Paid yapar. Review kuyruğa alınmaz. Paid ve outbox atomik kaydedilir; worker Received yapar. Geç callback yerel 15 dakikalık süre dolsa da doğrulanmış tahsilatı işler.

Bilinen token'ı bulunan Pending işlemler 30 saniyede, son 7 günlük kayıtlar üzerinde sınırlı ve sıralı biçimde uzlaştırılır. Müşteri de sonucu sorgulayabilir. Token alınamayan Unknown ancak doğrulanan callback ile kurtarılabilir; callback gelmezse manuel sağlayıcı kontrolü gerekir. 7 günden eski bekleyenler için otomatik uzlaştırma yoktur. Canlı tahsilat, iade/iptal ve işletme manuel uzlaştırma ekranı henüz yoktur.

## Doğrulama — 13 Ekim 2026

Dış iyzico sandbox üzerinde 140 TL test ödemesi, resmî test kartı ve mock 3D Secure ile tamamlandı. Geçici HTTPS callback uygulamaya döndü; DB Payment=Paid, Order=Received ve müşteri ekranında “Sipariş alındı” doğrulandı. Gerçek para çekilmedi. `example.test` e-postası hata 5 aldı; resmî örnek `email@email.com` ile test başarılı oldu.

Otomatik testler dış servise bağlı değildir: imza/tutar/correlation/tenant, tekrar callback, geç ödeme, review, belirsiz initialization ve açık reddin güvenli tekrarını kapsar. Harici sandbox kontrolü bunlardan ayrı yapıldı; canlı kabul testi değildir.

Resmî kaynaklar:

- [Kimlik doğrulama](https://docs.iyzico.com/en/getting-started/preliminaries/authentication/hmacsha256-auth)
- [Initialize](https://docs.iyzico.com/en/payment-methods/checkoutform/cf-implementation/cf-initialize)
- [Retrieve](https://docs.iyzico.com/en/payment-methods/checkoutform/cf-implementation/cf-retrieve)
- [Yanıt imzası](https://docs.iyzico.com/en/advanced/response-signature-validation)
- [Test kartları](https://docs.iyzico.com/en/add-ons/test-cards)
- [Hata kodları](https://docs.iyzico.com/en/add-ons/error-codes)
