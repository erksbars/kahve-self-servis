# Ödeme sağlayıcısı seçimi ve entegrasyon hazırlığı

Güncel durum: 13 Ekim 2026. Kullanıcı **iyzico** ve **kahveci başına ayrı sağlayıcı hesabı** modelini seçti. Checkout Form, kalıcı initialize/token state, callback → imzalı retrieve → settlement/outbox ve sınırlı background uzlaştırma bağlandı. Dış sandbox test kartı/mock 3D Secure ile 140 TL sipariş Paid → Received doğrulandı. Kurulum/sınırlar: [iyzico](iyzico-setup.md). Aşağıdaki teknik karşılaştırma ve hazırlık adımları önceki karar kaydıdır; canlı tahsilat/iade kapsamı tamamlanmadı.

## Önce tahsilat modelini seçmek gerekir

Çok işletmeli yazılım olması, tek platform hesabından tahsilat yapılacağını kendiliğinden belirlemez. İki ürün modeli vardır:

- Her kahveci kendi sağlayıcı hesabıyla tahsilat yapar: işletmeye ait sağlayıcı/hesap yapılandırması, güvenli secret yönetimi ve işletme bazında callback doğrulaması gerekir.
- Platform alt üye işyerleri adına tahsilat yapar: sağlayıcının pazaryeri ürünü, alt üye eşlemesi ve mutabakat/transfer akışı gerekir. Bu model için sağlayıcıyla hesap ve ürün kapsamı netleştirilmeli.

Mevcut demo tek sandbox hesabını kullanır. Gerçek hesap modeli adına varsayım veya sahte alt üye bilgisi eklenmedi.

## Teknik karşılaştırma

| Konu | iyzico | PayTR |
| --- | --- | --- |
| Barındırılan ödeme | Checkout Form initialize yanıtındaki paymentPageUrl ile yönlendirme; sonuç retrieve adımıyla alınır | iFrame API: sunucudan iframe_token alınır, form iframe'de açılır; sonuç callback ile gelir |
| Çok işletmeli tahsilat | Marketplace akışında basket item başına subMerchantKey ve subMerchantPrice | Pazaryeri ürünü ve platform transfer API'si |
| Kurulum girdileri | Sağlayıcı hesabı, sandbox API kimlik bilgileri, callback adresi ve seçilen hesap modeli | Merchant panelindeki merchant_id, merchant_key, merchant_salt; callback adresi ve seçilen hesap modeli |
| Müşteri verisi etkisi | CF initialize şeması buyer/billingAddress ve fiziksel üründe shippingAddress alanları tanımlar | iFrame token isteği email ve diğer kullanıcı alanlarını tanımlar |

Bu karşılaştırma sağlayıcıların entegrasyon dokümanlarına dayanır; komisyon, ticari uygunluk veya hesap onayı değerlendirmesi değildir. Misafir akışında hangi müşteri bilgilerinin gerçekten zorunlu olduğu seçilen sağlayıcı/ürünle doğrulanmalı. Gerçek kişisel bilgiler yerine sabit örnek bilgiler gönderilmemeli.

Mühendislik değerlendirmem: mevcut yönlendirme akışına yakınlığı ve belgelenmiş alt üye akışı nedeniyle iyzico Checkout Form'u ilk değerlendirme adayı olarak görüyorum. Bu bir teknik tercihtir; sağlayıcı seçimi yapılmış sayılmaz. PayTR iFrame de değerlendirilebilir. Hesap modeli ve gerekli müşteri verisi netleşmeden iki sağlayıcıdan biri etkinleştirilmez.

Resmî kaynaklar (13 Ekim 2026'da kontrol edildi):

- [iyzico Checkout Form akışı](https://docs.iyzico.com/en/payment-methods/checkoutform/cf-implementation)
- [iyzico Checkout Form initialize şeması](https://docs.iyzico.com/en/payment-methods/checkoutform/cf-implementation/cf-initialize)
- [iyzico Marketplace implementasyonu](https://docs.iyzico.com/en/products/marketplace/marketplace-implementation)
- [iyzico Marketplace online payment alanları](https://docs.iyzico.com/en/products/marketplace/marketplace-implementation/online-payment)
- [PayTR iFrame genel akışı ve kimlik bilgileri](https://dev.paytr.com/en/iframe-api)
- [PayTR iFrame token isteği](https://dev.paytr.com/en/iframe-api/iframe-api-1-adim)
- [PayTR platform transfer/pazaryeri](https://dev.paytr.com/platform-transfer-talebi)

## Bu dilimde tamamlanan kod hazırlığı

- `IPaymentProvider` local checkout devam yolu, referans ve config doğrulama sözleşmesidir. Sandbox imza protokolü bu genel sözleşmeden ayrıldı.
- `PaymentProviders` kurulu adaptörleri seçer: `sandbox`, `iyzico` ve `none`. Kurulmamış sağlayıcı başlangıçta hata verir; sandbox'a düşmez.
- İdempotent checkout yanıtı kayıtlı `Payment.Provider` üzerinden yol üretir. Kayıtlı adaptör bulunamazsa 503 verir ve yeni ödeme/sipariş üretmez.
- `WebhookProcessor` sandbox imzası/zaman/JSON doğrulamasından sorumludur. İç `VerifiedPaymentEvent` yalnızca doğrulanmış adapter sonucu olarak `PaymentSettlement`'a iletilir; HTTP body'ye bağlanmaz.
- `PaymentSettlement` provider+event/ref kilitlerini, tutar/para birimini, terminal durumu, payment/order/history/outbox transaction'ını yönetir. Sandbox geç ödeme reddi açık bir politikadır; gerçek adaptörün geç tahsilat/uzlaştırma tasarımı henüz uygulanmadı.
- Sandbox session/decision sorguları yalnızca sandbox sağlayıcısının kayıtlarını bulur.
- Menü `checkoutEnabled` bilgisi döndürür. Ödeme kapalıysa müşteri uyarı görür, ödemeye geçiş devre dışıdır ve sepeti korunur.
- Checkout zamanları PostgreSQL'in mikro saniye hassasiyetine eşitlendi; ilk yanıt ile tekrar yanıtının expiresAt değeri aynıdır.

`.env` için `PAYMENTS_PROVIDER=sandbox` demo; `PAYMENTS_PROVIDER=none` checkout ve demo ödeme endpoint'lerini kapatır. Bu ayar gerçek sağlayıcıyı kurmaz. Mevcut sipariş verisi silinmez. Anahtarlar sohbete veya kaynak koda eklenmez.

## Gerçek adaptör için kalan uygulama

1. Sağlayıcı, tahsilat modeli ve sandbox hesabı seçilir; secret'lar yerel ortam/config üzerinden sağlanır.
2. Snapshot'a bağlı ödeme başlatma, sağlayıcıya özel imza/kimlik doğrulama ve sonuç retrieve/callback implementasyonu eklenir. Harici HTTP isteği DB transaction'ı içinde yapılmaz.
3. Başlatma timeout'unda yeni tahsilat üretmeden aynı işlemin durumu sorgulanır; correlation/payment reference kalıcı saklanır. Provider protokolüne uygun idempotency/uzlaştırma uygulanır.
4. Provider callback'i sandbox HMAC protokolüne zorlanmaz; kendi doğrulama/ACK/retry kuralları uygulanır. Redirect tek başına ödeme onayı değildir.
5. Geç callback/expire sonrası başarılı tahsilat, iptal/iade ve uzlaştırma akışı tasarlanır; gerçek tahsilat sessizce kaybedilmez.
6. Sağlayıcıya erişen sandbox E2E; yinelenen callback, tutar/para birimi, tenant/alt üye, timeout ve gerçek callback erişimi test edilir.

Bu hazırlık testleri harici entegrasyon kanıtı değildi. Sonraki iyzico bağlantısı ve ayrı dış sandbox testi tamamlandı; güncel doğrulama/sınırlar `iyzico-setup.md` içinde. Canlı tahsilat kabulü ve iade hâlâ ayrı kapsamdır.
