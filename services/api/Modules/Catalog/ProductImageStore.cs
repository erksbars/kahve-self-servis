using SkiaSharp;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Catalog;
public sealed class ProductImageStore(IConfiguration config, IWebHostEnvironment env)
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public string Root => Path.GetFullPath(config["CatalogImages:Root"] ?? Path.Combine(env.ContentRootPath, "catalog-images"));
    public static byte[] Normalize(byte[] bytes)
    {
        if (bytes.Length is < 1 or > MaxBytes) throw new ArgumentException("Görsel en fazla 5 MB olabilir.");
        using var data = SKData.CreateCopy(bytes); using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp)) throw new ArgumentException("Geçerli bir JPG, PNG veya WebP görseli seçin.");
        var info = codec.Info;
        if (info.Width is < 32 or > 6000 || info.Height is < 32 or > 6000 || (long)info.Width * info.Height > 16000000 || codec.FrameCount > 1) throw new ArgumentException("Görsel 32–6000 piksel, en fazla 16 megapiksel ve hareketsiz olmalı.");
        using var decoded = new SKBitmap(info.Width, info.Height);
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success) throw new ArgumentException("Görsel okunamadı; başka bir dosya seçin.");
        // Apply EXIF orientation before re-encoding; no original metadata is copied.
        var swapped = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swapped ? info.Height : info.Width; var height = swapped ? info.Width : info.Height;
        var scale = Math.Min(1d, 960d / Math.Max(width, height));
        using var output = new SKBitmap(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)));
        using (var canvas = new SKCanvas(output))
        {
            canvas.Clear(SKColors.White); canvas.Scale((float)scale);
            switch (codec.EncodedOrigin)
            {
                case SKEncodedOrigin.TopRight: canvas.Translate(width, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: canvas.Translate(width, height); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Translate(0, height); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(width, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(width, height); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, height); canvas.RotateDegrees(-90); break;
            }
            canvas.DrawBitmap(decoded, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        }
        using var image = SKImage.FromBitmap(output); using var encoded = image.Encode(SKEncodedImageFormat.Webp, 84); return encoded.ToArray();
    }
    public string FilePath(Guid tenant, string key) => Path.Combine(Root, tenant.ToString("N"), key + ".webp");
    public async Task<string> Save(Guid tenant, byte[] bytes, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N"); var path = FilePath(tenant, key); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct); return $"/api/catalog-images/{tenant:D}/{key}";
    }
    public static void MapPublic(WebApplication app)
    {
        app.MapGet("/api/catalog-images/{tenant:guid}/{key}", async (Guid tenant, string key, Infrastructure.AppDbContext db, ProductImageStore store, HttpContext http, CancellationToken ct) =>
        {
            if (key.Length != 32 || !key.All(char.IsAsciiHexDigit)) return Results.NotFound();
            var url = $"/api/catalog-images/{tenant:D}/{key}";
            if (!await db.Products.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.TenantId == tenant && x.ImagePath == url, ct)) return Results.NotFound();
            var path = store.FilePath(tenant, key); if (!File.Exists(path)) return Results.NotFound();
            http.Response.Headers.CacheControl = "public, max-age=86400, immutable"; return Results.File(path, "image/webp");
        });
    }
}
