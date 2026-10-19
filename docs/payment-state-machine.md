# Sandbox ödeme ve operasyon durumları — Aşama 4

Sandbox ödeme ve kalıcı sipariş uygulanmıştır. Gerçek sağlayıcıyla tahsilat, iptal ve iade henüz uygulanmadı.

Kalıcı ödeme kaydı: Pending → Paid / Failed / Expired. Order başlangıcı PaymentPending'dir; doğrulanmış ödeme sonucu Paid / PaymentFailed / Expired olur. Paid → Received aktarımı outbox worker üzerinden gerçekleşir.

Received → Preparing → Ready → Completed geçişleri domain, staff PATCH ve barista panosunda uygulanır. expectedVersion eskiyse 409; siparişin branch/tenant yetkisi veya ücretli ödeme koşulu sağlanmazsa işlem yapılmaz. Tamamlanan sipariş aktif panodan çıkar; takip sayfasında terminal durum korunur. Draft/CheckoutPending ayrı kalıcı durumlar değildir; doğrulanmış checkout transaction'ı PaymentPending order + Pending payment oluşturur.

- Quote ve checkout fiyatları sunucuda katalog + varyant + extras üzerinden yeniden hesaplanır. TRY decimal tutarlar checkout snapshot'ında sabit kalır.
- Kapalı şube, kapanan ürün ve geçersiz seçenek ödeme başlatılmadan reddedilir.
- Checkout idempotency key tenant/branch kapsamında unique saklanır; farklı request body ile aynı key 409 üretir.
- Hosted ödeme redirect'i tek başına doğrulama değildir. İmzalı webhook veya sağlayıcıdan server-side doğrulama asıl kaynaktır.
- Provider event ID unique, tutar/para birimi/reference eşleştirmesi zorunlu. Replay ve tekrar webhook yalnızca bir Paid transition üretir. Aynı event ID farklı gövde ile reddedilir; farklı event ID ile aynı Paid sonuç yeni outbox/durum oluşturmaz.
- Paid + operasyon kuyruğu event'i aynı transaction'da outbox'a yazılır. Worker tekrar deneyebilir; client event'leri idempotent olmalıdır.
- Tracking token kriptografik rastgele ve siparişe özel; veritabanında hash saklanır. Ardışık public number erişim yetkisi değildir.
- Her geçiş OrderStatusHistory içinde kaynak/zaman/neden kaydeder; Order.Version artırılır. Staff kimliği history Actor ve AuditLog.ActorId'de saklanır. History/audit/canlı bildirim kaydı status ile aynı transaction'dadır.
- İptal ve iade kuralları gerçek sağlayıcı davranışı seçilince kesinleştirilecek; terminal state'ten keyfi geri dönüş olmayacak.

Yerel sahte sandbox IPaymentProvider entegrasyonu tamamlandı. Demo ekranındaki sonuç seçimi sunucuda HMAC-SHA256 imzalı olay üretir ve webhook ile aynı işlemciden geçer. ±300 saniye toleransı, fixed-time imza karşılaştırması ve 15 dakikalık oturum süresi uygulanır. Failed/Expired terminaldir; yeniden deneme yeni checkout key'i gerektirir.

Outbox işçisi sipariş geçişi ve processedAt işaretini tek transaction'da saklar. İşleme hatası rollback edilir, attempts/nextAttemptAt DB'de güncellenir ve yeniden deneme yapılır. Başarılı ödeme aktarım hatasında kaybolmaz. Worker uygulama yeniden başladığında DB'deki bekleyen kayıtları bulur.

Takip ve ödeme tokenları 32-byte kriptografik rastgele değerlerdir. Veritabanında hash ve idempotent yanıt için ayrı AES-GCM şifreli kopya saklanır. Raw token DB/log alanına yazılmaz. Şifreleme anahtarı ve webhook secret frontend'e gönderilmez.

iyzico sandbox entegrasyonu tamamlandı: NotStarted → Initializing → Ready; açık başlatma reddi Rejected ile aynı checkout düzeltilebilir, belirsiz sonuç Unknown ile ikinci initialize engellenir. İmzalı retrieve fraud review için Review, onaylı sonuç için Paid → Received, doğrulanmış başarısız sonuç için Failed üretir. Callback yerel süre dolduktan sonra da doğrulanmış tahsilatı kabul eder. Bilinen token'lı Pending kayıtlar son 7 gün için uzlaştırılır. Token alınamayan Unknown için doğrulanmış callback veya manuel sağlayıcı kontrolü gerekir. İptal/iade, pazaryeri alt üye modeli ve canlı kabul testleri ayrı kapsamdır.
