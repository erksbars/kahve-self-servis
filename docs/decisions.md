# Mimari kararlar — 12 Ekim 2026

Kullanıcının talebi MVP hazırlığına başlamak. `kahve_self_servis_mvp_codex.md` ürün gereksinimi referansı olarak okundu; içindeki örnek Codex promptu ayrı bir kullanıcı talimatı olarak çalıştırılmadı. Boş repo için ilk doğrulanabilir kapsam Aşama 0–1 ve API'ye bağlı menü ön izlemesi seçildi.

1. Tek React frontend, müşteri `/b/:slug` ve personel `/staff` route'ları. Ayrı build gereksinimi yok.
2. ASP.NET Core / EF Core 10 ve PostgreSQL 17; modüler monolit. .NET 10 LTS tercihi [Microsoft destek tablosuna](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support) dayanır. PostgreSQL sağlayıcısı [Npgsql 10.0.3](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL); EF runtime/design aynı 10.0.12 sürümüne sabitlendi.
3. Personel oturumu sunucuda doğrulanan cookie'dir. Tenant istemci parametresinden alınmaz; doğrulanan kullanıcının claim'inden gelir. Aktiflik ve rol her istekte veritabanından yeniden doğrulanır.
4. Tenant filtreleri anonim istek için sonuç döndürmez. Public menü, credential doğrulama ve demo seed kontrollü global sorgu istisnalarıdır. Public menü yalnızca resolved branch + tenant kapsamındaki katalog DTO'sunu döndürür.
5. Ürün ve kategoriler işletme bazındadır; BranchProduct şube uygunluğu ve fiyat override'ını tutar. Composite foreign key yanlış tenant ilişkisinin DB'ye yazılmasını engeller. Aşama 2 migration'ı eski kategori metinlerini tenant bazında kategori satırlarına aktarır; mevcut ürün kimlikleri ve fiyatları korunur.
6. Şube aç/kapat için expectedVersion zorunludur. Güncelleme ve audit aynı SaveChanges transaction'ında saklanır. Yetkisiz şube 404, eski sürüm 409 döndürür.
7. Demo verisi yalnızca açık Demo:Enabled ve Development/Testing ortamında oluşturulur. İlk seed atomik, tekrar çalıştırma idempotent. Rastgele yerel parolalar Git dışında `.env` dosyasında saklanır.
8. Yerel Docker portları 5187/5087/55432; mevcut uygulamanın 5173/5080 portlarıyla çakışmayı önler. Başka servise müdahale edilmedi.
9. Aşama 2'de sepet/quote, Aşama 3'te sandbox checkout ve kalıcı sipariş tamamlandı. Gerçek sağlayıcıyla tahsilat yok. Müzik, oyun, sohbet, sadakat kapsam dışı.
10. Seçenek grupları min/max seçim kurallarına sahiptir. OptionValue global uygunluk ve fiyat farkını; BranchOption şube uygunluğu/fiyat override'ını tutar. Tüm ilişkiler tenant composite FK'leri ile sınırlandırılır. Seçeneğin ilgili ürüne ait olması ayrıca quote'ta doğrulanır.
11. Quote ve checkout PostgreSQL RepeatableRead ile katalog okur; tutarı decimal hesaplar. Quote rezervasyon değildir. Checkout expectedTotal ile yeniden doğrular ve snapshot'ı kalıcı saklar; fiyat değişmişse kullanıcıdan güncel toplamı yeniden onaylamasını ister.
12. Misafir sepeti şube bazında sessionStorage'dadır. API yalnızca product ID, option IDs, adet ve branch slug kabul eder; fiyat alanlarını kaynak olarak kullanmaz. Sepet sınırları 20 satır, satırda 10 adet, toplam 50 adet, satırda 10 seçenektir.
13. QR [node-qrcode](https://github.com/soldair/node-qrcode) ile PNG üretilir; harici QR servisine veri gönderilmez. Site adresi girilir; dal bağlantısı seçilen şubeden oluşturulur. Yerel adresin telefonla paylaşım sınırı ekranda açıklanır.
14. Aşama 3 PaymentProvider yalnızca server-side sandbox adaptörüdür. Demo sonuç seçimi imzalı olay üretir; browser redirect'i asıl ödeme kaynağı değildir. Secret'lar yalnızca API ortamındadır. Sandbox Development/Testing ile sınırlıdır ve feature flag kapalıysa endpoint'ler 404 döner.
15. Checkout key şube bazında unique; request hash farklı gövdeyi reddeder. Frontend aynı tam request için key'i sessionStorage ve bellek içinde korur; cevap kaybında aynı oturumu geri alır. Başarısız/Expired işlem yeni key ile yeniden denenir.
16. Ödeme onayı ve outbox atomiktir. Worker PostgreSQL row lock + order advisory lock kullanır; işlem ve processed işareti birlikte commit olur. Hata halinde ücretli sipariş korunur, kalıcı backoff ile tekrar denenir. Redis/event bus eklenmedi.
17. Order tracking/session tokenları 256-bit rastgele; DB hash üzerinden lookup yapılır. İdempotent yanıtlar için raw token ayrıca AES-GCM ile şifrelenir. Tracking encryption key `.env` içinde kalıcı tutulur; demo container restart'ları bu anahtarı değiştirmez.
18. Pending oturum 15 dakikada sona erer. Sandbox süresi dolduktan sonra yeni Paid olayını kabul etmez. Gerçek sağlayıcıda geç ödeme, iptal/iade ve uzlaştırma ayrı kapsamdır. Aşama 4'te Received sonrası staff durum güncellemeleri UI/API'ye açıldı.
19. Staff PATCH order lock altında current version kontrol eder. Aynı sürümü kullanan iki baristadan biri 409 alır; frontend güncel durumu yükler, offline'da sonucu olmuş gibi göstermez. History/audit/RealtimeEvent commit birlikte yapılır.
20. SignalR grupları server-side yetkilidir. Tracking token JoinTracking mesajında kalır; URL/query/log'a eklenmez. Staff üyeliği katılımda ve event öncesinde aktiflik/rol/tenant/branch üzerinden doğrulanır. Yetki iptalinde grup üyeliği ve gösterilen pano kaldırılır.
21. RealtimeEvent durable outbox seçildi; HTTP commit sonrası doğrudan socket yayınına bağımlılık yok. Olaylar tekrar gelebilir; client yalnızca snapshot invalidation yapar. Tek API instance için in-memory SignalR membership yeterlidir; yatay ölçekleme bu dilimde yoktur.
22. [SignalR client dokümanındaki](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0) davranışa göre ilk bağlantı hataları ayrıca retry edilir; otomatik reconnect sonrasında grup join ve GET tekrar uygulanır. Fallback 2 saniye, canlı bağlantı uzlaştırması 30 saniyedir.
23. Personel yeni ücretli siparişleri ekranda görür; ses yalnızca kullanıcı düğmeyle açınca etkinleşir. Browser notification/OS bildirim izni istenmez. 10 dakikadan fazla bekleyen kartlar görsel işaretlidir; sipariş sırası paidAt'a göre korunur, süre tahmini üretilmez.

## Açık kararlar

- Ödeme sağlayıcısı, alt üye/işletme modeli, KDV ve belge akışı henüz seçilmedi.
- Çalışma saatleri, kapasite bilgi ayarı ve personel daveti Aşama 5'te uygulandı. Platform yöneticisi ve kapasiteyle rezervasyon limiti henüz yok.
- Canlı ortam için domain, hosting, TLS, secret yönetimi, kalıcı cookie keyring, backup ve migration deployment süreci belirlenmedi.
- Seed saat kısıtlaması koymaz; owner sonradan haftalık saatleri tanımlar. UI saatler ile manuel kabulü birlikte değerlendirir.

25. İşletme mutation'ları tenant advisory lock altında serialize edilir. Ürün/seçenek aggregate'i ve şube override'ı aynı tenant kilidini kullanır; Version ile eski formun sessiz üzerine yazması engellenir. Public checkout RepeatableRead snapshot ile fiyat okur.
26. Ürün silme yerine satıştan kapatma vardır. Seçenekler kaldırılabilir; önceki order/payment JSON snapshot'ları korunur. Seed marker, sunucu yeniden başladığında kaldırılmış seçeneklerin geri eklenmesini önler.
27. Haftalık saatler şube IANA saat diliminde değerlendirilir. Boş liste saat kısıtı koymaz; manuel kabul ayrı tutulur. Gece yarısını aşan aralıklar desteklenmez. Hazırlık kapasitesi bu dilimde bilgi ayarıdır.
28. Davet e-posta göndermez; owner'ın paylaşacağı 24 saatlik, tek kullanımlık capability link oluşturur. Raw token saklanmaz. Parola oluşturma tenant kilidiyle rol/aktiflik değişimine karşı serialize edilir. Login ve davet kabulü ayrı rate limit havuzlarındadır.
29. Rapor satış cohort'unu ödeme tarihine bağlar; Preparing → Ready sürelerini history'den hesaplar. Gelir vergi dahil sipariş toplamıdır; kullanıcı vergi oranını belirtir, yasal oran varsayılmaz. İade/iptal operasyonu henüz yoktur.

30. Kullanıcı henüz gerçek ödeme sağlayıcısı seçmedi (13 Ekim 2026). Aşama 6'da yalnızca sağlayıcıdan bağımsız hazırlık yapıldı. iyzico/PayTR ve işletme hesabı/pazaryeri modeli `payment-provider-selection.md` içinde resmî dokümanlarla karşılaştırıldı; gerçek adaptör etkinleştirilmedi.
31. Checkout continuation ve config doğrulaması genel IPaymentProvider sözleşmesine; sandbox HMAC imzası SandboxPaymentProvider'a aittir. PaymentProviders startup'ta seçimi doğrular; desteklenmeyen değer sandbox'a düşmez. İdempotent checkout, Payment.Provider alanını kullanır; adaptör yoksa 503 döner ve yeni işlem oluşturmaz.
32. Adapter yalnızca doğrulanmış internal VerifiedPaymentEvent üretir; ortak PaymentSettlement transaction'ı amount/currency, provider-qualified event/ref lock, terminal durum, audit/history/outbox kurallarını uygular. Sandbox süre aşımı politikasının gerçek tahsilat için otomatik varsayılmaması açık bırakıldı. Gerçek geç ödeme uzlaştırması henüz yok.
33. Checkout UTC zamanı PostgreSQL mikro saniye hassasiyetine yuvarlanır; ilk yanıt ile DB-backed tekrarın expiresAt değeri birebir aynıdır.

## iyzico sandbox bağlantısı

13 Ekim 2026: her işletmenin kendi hesabı seçildi; platform alt üye tahsilatı uygulanmadı. Hosted Checkout Form sandbox'a bağlandı; kart bilgisi uygulamaya alınmaz. Callback token'ına güvenilmez, kendi işletme anahtarıyla retrieve response imzası ve tam sipariş/tutar/satır eşlemesi gerekir. Review kuyruğa çıkmaz. Ödeme ve outbox mevcut atomik settlement üzerinden işlenir.

Açık initialization failure güvenli Rejected retry sağlar; belirsiz initialization ikinci tahsilatı engeller. Known-token Pending son 7 gün boyunca uzlaştırılır, unknown/no-token doğrulanmış callback veya manuel inceleme gerektirir. Geç onay yerel süre aşımı nedeniyle kaybedilmez. Dış sandbox ödeme/callback/Received testi başarılıdır; canlı para, refund ve operatör uzlaştırma ekranı ayrı kapsamdır. Lokal callback browser return sabit localhost5187 adresine bağlıdır; production dağıtımında canonical HTTPS origin tasarımı gerekir.

## Ürün görseli ve premium menü — 13 Ekim 2026

Public katalog görsel URL ve alternatif metin içerir. Yükleme sadece Owner'ın kendi ürününe, CSRF/version ve bounded raster decode ile yapılır; key/tenant dosya yolu uygulama tarafından üretilir. Görseller WebP'ye normalize edilir, metadata atılır; API non-root kalır ve kalıcı volume kullanır. DB ilişkisiz eski dosyalar sonraki bakım işine bırakılır, detach kalıcı dosya silmez. Demo fotoğrafları temsilidir ve arayüzde belirtilir. Yeni demo içerik bir marker ile eklenir; owner değişiklikleri veya eski ödeme/sipariş snapshot'ları resetlenmez.
