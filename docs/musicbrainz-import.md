# MusicBrainz dosyasından katalog aktarımı

> Arşiv: 19 Ekim 2026 kullanıcı kararıyla dış katalog kapsamdan çıkarıldı. Aktarım ekranı güncel arayüzden kaldırıldı. Bu rehber önceki uygulamanın kaydıdır; güncel akış için [siparişe bağlı istek parçalar](music.md).

`/admin` → **Müzik** → şube seçimi → **MusicBrainz’den şarkı ekle**.

1. MusicBrainz kayıtlarını içeren JSON dosyasını seçin (en fazla 500 kayıt / 512 KB).
2. **Kayıtları önizle**: ad, sanatçı, yeni/mevcut kayıt ve şube durumu görüntülenir. Veritabanına yazılmaz.
3. **Kataloğa aktar**: kayıtlar işletmenin kataloğuna alınır. Yeni şube bağlantıları kapalıdır.
4. **İşletme kataloğu** bölümünde istediğiniz şarkıları **Şubede aç** ile müşterilere sunun.

Aktarım modülü otomatik açmaz; mevcut başlık/sanatçı ve şube açık/kapalı kararlarını değiştirmez. Katalog şarkısı seçme → öneri → işletme kabul/reddetme akışı sürer. Serbest metin ve Spotify/YouTube arama bağlantıları korunur. Müşteri sadece o şubede açılan yerel kayıtları arar.

## Veri kaynağı ve kapsam

Şarkı adı, sanatçı kredisi ve recording MBID, MusicBrainz'in CC0 temel veri alanlarıdır. Etiket/tür, puan, annotation, albüm resmi, ses ve diğer alanlar aktarılmaz. Verinin lisansı ile barındırılan servise erişim ayrı konulardır: [temel veri lisansı](https://musicbrainz.org/doc/About/Data_License), [veritabanı indirme bilgileri](https://musicbrainz.org/doc/MusicBrainz_Database/Download), [resmî veri dağıtımları](https://metabrainz.org/datasets/postgres-dumps).

Bu sürüm canlı API çağırmaz; ticari API/startup hesabı gerekmeden yerel dosya işleme altyapısını sağlar. MusicBrainz dosyası projeye dahil edilmedi, mevcut 49 demo şarkı değişmedi. Dosya sahibi verinin MusicBrainz'den geldiğini doğrulamalıdır; girilen MBID çevrimiçi sorgulanmaz. Büyük arşivler otomatik indirilmez. Kaynak dağıtımın kendi erişim koşulları ve checksum bilgileri izlenmelidir. [MusicBrainz API erişim koşulları](https://musicbrainz.org/doc/MusicBrainz_API).

## Kabul edilen dosya biçimi

MusicBrainz JSON recording arama çıktısındaki `recordings` dizisi veya aynı kayıtların doğrudan JSON dizisi kabul edilir. Aşağıdaki yalnızca şema örneğidir; ID gerçek bir recording UUID olmalıdır:

```json
{
  "recordings": [
    {
      "id": "<recording UUID>",
      "title": "Şarkı adı",
      "artist-credit": [{ "name": "Sanatçı adı" }]
    }
  ]
}
```

`artist-credit` içinde `name` veya `artist.name`, `joinphrase` ve metin parçaları desteklenir. Şarkı ve birleştirilmiş sanatçı adı 1–160 karakter olmalı; kontrol karakterleri kabul edilmez. Aynı MBID'nin farklı şarkılarla tekrarlandığı dosya reddedilir. Aynı başlık/sanatçının farklı kayıt/master sürümleri menüde tek seçenek olur; tek temsilci MBID tutulur. Mevcut MBID veya normalize ad/sanatçıyla eşleşen kayıt yeniden yaratılmaz. İçerik HTML olarak işlenmez.

## Büyük dökümden küçük dosya hazırlama

Python 3 standart kütüphanesi yeterlidir; ek paket/hesap/anahtar yoktur. `scripts/prepare-musicbrainz-catalog.py` yerel recording JSON, JSONL, sıkıştırılmış JSONL (`.xz`, `.gz`, `.bz2`) veya recording JSONL içeren `.tar.xz/.tar.bz2/.tar.gz` arşivini okur. PostgreSQL `mbdump.tar.bz2` bir JSON recording arşivi değildir ve bu araçla doğrudan işlenmez. JSONL kaynağı [MusicBrainz DumpJSON biçimine](https://github.com/metabrainz/musicbrainz-server/blob/master/admin/DumpJSON) uygun olmalıdır.

Proje klasöründe örnek komut:

```powershell
python ./scripts/prepare-musicbrainz-catalog.py "C:/MusicBrainz/recording.tar.xz" "C:/MusicBrainz/kafe-katalog.json" --artist "Scorpions" --artist "Sezen Aksu" --artist "Norah Jones" --limit 200
```

Birden çok `--artist` filtresinden herhangi birine uyan ilk benzersiz şarkılar seçilir. Filtre verilmezse ilk geçerli kayıtlar alınır; popülerlik veya kafe uygunluğu sıralaması değildir. Geçersiz/uzun kayıtlar elenir; çıktıyı önizlemede inceleyin. Filtreler büyük dökümün uzun süre taranmasını gerektirebilir. Tüm döküm belleğe alınmaz; arşiv üyeleri diske çıkarılmaz. Çıktı yeni dosyaya yazılır; mevcut dosyanın üzerine yazılmaz. `--limit` 1–500, varsayılan 200. Sonuç yoksa dosya oluşturulmaz. Çıktı boyutu 512 KB'ı aşarsa daha küçük limit kullanın.

Hazırlanan `kafe-katalog.json` dosyasını yukarıdaki yönetim ekranında seçin.

## API ve doğrulama

`POST /api/admin/music/{branchId}/musicbrainz/import`

```json
{ "data": { "recordings": [] }, "preview": true }
```

Boş örnek gerçek istekte reddedilir; 1–500 kayıt gerekir. `preview` varsayılan true; yazmak için false gönderilir. Endpoint gövde sınırı 1 MB (JSON zarfı dahil). Owner rolü, CSRF, tenant/şube denetimi ve ortak admin lock/transaction kullanılır. Tüm kayıtlar yazmadan önce doğrulanır; hatalı satır bütün aktarımı durdurur. Sonuç `total`, `added`, `linked`, `unchanged`, `rows` döndürür; sayılar tekrarlar ayıklandıktan sonraki benzersiz şarkılara aittir. Eşzamanlı tekrarlar aynı kayda gider; commit yanıtı kaybolursa güvenle yeniden gönderilebilir. Aktarım audit kaydı üretir.

`MusicBrainzSources` migration Songs tablosuna nullable MusicBrainzRecordingId ve tenant başına unique index ekler. Mevcut şarkı/öneri silinmez. Veri lisansı MusicBrainz içeriğine aittir; bu entegrasyon müzik çalma hakkı vermez.
