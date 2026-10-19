# İlk premium menü görselleri

13 Ekim 2026. Üretim: built-in image_gen, her ürün için ayrı çağrı. Demo için temsili görsellerdir; gerçek işletmenin ürün çekimi değildir.

Kullanılan optimize edilmiş dosyalar: `apps/web/public/images/menu/*.webp`. Her biri en uzun kenarda 960 piksel, kalite 84; yaklaşık 88–140 KB. Orijinal PNG'lerin proje içi kopyaları ignored `.local/menu-source` altında tutulur. Sıkıştırma aynı ProductImageStore normalizasyonuyla yapıldı. Koleksiyon contact sheet üzerinden görsel olarak kontrol edildi.

## İlk görselin tam istemi

```text
Use case: product-mockup. Asset type: premium coffee menu product photograph, square image. Subject: a single flat white coffee in a small matte ivory ceramic cup and saucer, microfoam with delicate fern latte art. Scene: warm cream limestone tabletop, soft warm natural side light, understated independent coffee shop editorial photography, realistic fine ceramic and foam texture. Camera: three-quarter overhead angle, entire cup centered with generous margin, shallow but subject clearly sharp. Palette: cream, espresso brown, muted olive. No writing, logos, people, borders or collage. Create one photorealistic product image, not a website mockup.
```

## Diğer 11 görselin ortak tam istem şablonu

```text
Use case: product-mockup. Asset type: premium coffee menu photograph, square image. Subject: {subject}. Scene: warm cream limestone tabletop, soft warm natural side light. Understated independent coffee shop editorial food photography, realistic fine texture. Camera: three-quarter overhead angle, subject centered with generous margin, sharp subject and subtly soft background. Palette: cream, espresso brown, muted olive. No text, logos, people, borders, utensils or collage. Create exactly one photorealistic product photograph, not a website mockup.
```

`subject` yerine aşağıdaki metinler aynen kullanıldı:

| Dosya | Subject |
| --- | --- |
| latte.webp | A tall matte ivory ceramic cup of cafe latte on a saucer, smooth milk and espresso, elegant heart latte art |
| americano.webp | A single matte ivory cup and saucer filled with dark black americano coffee, subtly reflective surface, no milk |
| filter.webp | A small amber glass carafe of freshly brewed black filter coffee beside an ivory cup |
| cold-brew.webp | A single clear straight glass of dark cold brew coffee with large translucent ice cubes, rich dark coffee, no milk |
| espresso.webp | A small ivory demitasse of espresso on a tiny saucer, thick golden crema |
| cappuccino.webp | A round ivory cappuccino cup with thick airy milk foam and a small circular latte art swirl on a saucer |
| iced-latte.webp | A single clear tall glass of iced latte with milk and espresso softly marbled, large ice cubes, no whipped cream |
| matcha.webp | A single matte ivory cup of matcha latte, soft green matcha foam with white leaf latte art, on a saucer |
| croissant.webp | One golden flaky butter croissant on a warm ivory ceramic plate, realistic delicate crisp layers |
| brownie.webp | One square dark chocolate brownie on an ivory ceramic plate, rich fudgy crumb, minimal crumbs |
| cheesecake.webp | One triangular slice of burnt Basque San Sebastian cheesecake on an ivory ceramic plate, deeply browned top and creamy pale inside |

Her çağrıda transparent_background=false; referans görsel kullanılmadı. CLI veya API key gerektiren alternatif üretim kullanılmadı.
