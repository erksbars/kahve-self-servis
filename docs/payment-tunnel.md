# Geçici iyzico callback HTTPS bağlantısı

Kullanıcı 13 Ekim 2026'da geçici test bağlantısını seçti. Cloudflare Quick Tunnel kullanılır; Cloudflare hesabı veya alan adı gerekmez. Bu test servisi canlı ortam değildir.

## Kapsam

cloudflared doğrudan uygulama portuna bağlanmaz. Loopback `127.0.0.1:5190` üzerinde ayrı Node callback gateway'i vardır:

- GET `/` ve `/healthz` sabit sağlık metni döndürür.
- Yalnızca POST `/api/payments/iyzico/callback/{32 karakterlik lowercase hex referans}` ve `application/x-www-form-urlencoded` yerel API'ye iletilir.
- Menü/personel/admin/auth/dosya yolları, query string ve başka metotlar 404; yanlış içerik 415; 64 KiB üstü 413 döner.
- Callback için global 120/dakika sınırı vardır. Cookie, Authorization ve forwarded header'lar API'ye taşınmaz. API response body, cookie ve redirect'leri dışarı yansıtılmaz.
- Request body/access log yoktur. Callback imzası/sonuç retrieve doğrulaması uygulama katmanının sorumluluğudur; gateway bir ödeme doğrulayıcısı değildir.

Bu bağlantı kurulduğunda iyzico checkout/callback uygulama bağlantısı henüz tamamlanmamış olabilir. Gateway sağlık kontrolünün geçmesi, gerçek bir ödeme callback'inin kabul edildiği anlamına gelmez. Callback normalleştirme, browser dönüşü ve settlement entegrasyonunda gateway response sözleşmesi de tamamlanacak.

## Başlatma / kapatma

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-payment-tunnel.ps1
```

Script resmî cloudflared 2026.10.0 Windows amd64 binary'sini proje `.local` klasörüne indirir ve GitHub release asset SHA256 digest'i ile karşılaştırır. Global install veya servis kurulmaz. Gateway ve tünel gizli background process olarak çalışır. URL, PID ve başlangıç zamanı ignored `.local/payment-tunnel/state.json` içinde tutulur. Anahtarlar bu dosyada yer almaz.

Üretilen adres ignored `.env` dosyasına `IYZICO__CALLBACKORIGIN` olarak kaydedilir; mevcut merchant anahtarları ve ödeme modu korunur. API bu env değerini yeniden oluşturulduğunda yükler. iyzico initialize bu origin'i kullanır. Gateway yalnızca `http://127.0.0.1:5187/iyzico/pay/{43-karakter-capability}` dönüşüne 303 izin verir; başka redirect veya upstream body aktarılmaz. Bu masaüstü test bağlantısı farklı cihaz/production adresi için tasarlanmadı.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\stop-payment-tunnel.ps1
```

Kapatma scripti PID'lerin bu proje binary/script komutuna ait olduğunu kontrol eder. Diğer uygulamaları durdurmaz. Bilgisayar/tünel kapatılınca URL çalışmaz; yeni tünelde adres değişir. Devam eden sandbox ödeme denemeleri varken tüneli kapatmayın.

## Kontroller

`node --test scripts/payment-callback-gateway.test.cjs`: public yol kısıtı, sabit upstream'e forwarding, header/cookie/response redaction, content/body sınırı, rate limit ve allowlist dönüşü için 5 test.

[Cloudflare resmî Quick Tunnel dokümanı](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/)
