using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Admin;
public static partial class AdminEndpoints
{
    private static void MapProductImages(RouteGroupBuilder group)
    {
        group.MapPut("/products/{id:guid}/image", async (Guid id, int expectedVersion, string? alt, HttpContext http, AppDbContext db, RequestScope scope, ProductImageStore store, CancellationToken ct) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct); if (product is null) return Results.NotFound();
            if (product.Version != expectedVersion) return Conflict();
            if (alt?.Length > 200) return Invalid("Görsel açıklaması en fazla 200 karakter olabilir.");
            if (http.Request.ContentLength > ProductImageStore.MaxBytes) return Results.StatusCode(413);
            using var buffer = new MemoryStream(); var chunk = new byte[8192]; int read;
            while ((read = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > ProductImageStore.MaxBytes) return Results.StatusCode(413);
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            byte[] normalized;
            try { normalized = ProductImageStore.Normalize(buffer.ToArray()); } catch (ArgumentException e) { return Invalid(e.Message); }
            product.ImagePath = await store.Save(product.TenantId, normalized, ct);
            product.ImageAlt = string.IsNullOrWhiteSpace(alt) ? product.Name : alt.Trim(); product.Version++;
            Audit(db, scope, id, "product.image.update"); await NotifyBranches(db, ct); await db.SaveChangesAsync(ct);
            return Results.Ok(new { imageUrl = product.ImagePath, product.ImageAlt, product.Version });
        }).WithMetadata(new RequestSizeLimitAttribute(ProductImageStore.MaxBytes)).RequireRateLimiting("imageUpload");
        group.MapDelete("/products/{id:guid}/image", async (Guid id, int expectedVersion, AppDbContext db, RequestScope scope, CancellationToken ct) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct); if (product is null) return Results.NotFound();
            if (product.Version != expectedVersion) return Conflict();
            product.ImagePath = null; product.ImageAlt = null; product.Version++;
            Audit(db, scope, id, "product.image.detach"); await NotifyBranches(db, ct); await db.SaveChangesAsync(ct);
            return Results.Ok(new { imageUrl = (string?)null, product.ImageAlt, product.Version });
        });
    }
}
