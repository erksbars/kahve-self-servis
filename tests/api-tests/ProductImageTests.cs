using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Kahve.Api.Tests;
public sealed class ProductImageTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    private static byte[] Photo(int width = 1200, int height = 900)
    {
        using var bitmap = new SKBitmap(width, height); bitmap.Erase(SKColors.SaddleBrown); using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100); return encoded.ToArray();
    }
    private static async Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string path, byte[]? bytes = null, bool csrf = true)
    {
        var req = new HttpRequestMessage(method, path) { Content = bytes is null ? null : new ByteArrayContent(bytes) };
        if (bytes is not null) req.Content!.Headers.ContentType = new("image/jpeg");
        if (csrf) req.Headers.Add("X-CSRF-TOKEN", (await c.GetFromJsonAsync<JsonElement>("/api/session/csrf")).GetProperty("token").GetString());
        return await c.SendAsync(req);
    }
    private static async Task Login(HttpClient c, string email)
    {
        var csrf = await c.GetFromJsonAsync<JsonElement>("/api/session/csrf"); var req = new HttpRequestMessage(HttpMethod.Post, "/api/session/login") { Content = JsonContent.Create(new { email, password = ApiFixture.Password }) }; req.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString()); (await c.SendAsync(req)).EnsureSuccessStatusCode();
    }
    private async Task<Guid> Product()
    {
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Slug == "demo-moda"); var category = await db.Categories.IgnoreQueryFilters().FirstAsync(x => x.TenantId == branch.TenantId);
        var p = new Product { Id = Guid.NewGuid(), TenantId = branch.TenantId, CategoryId = category.Id, Name = "Image test", BasePrice = 100 }; db.Products.Add(p); db.BranchProducts.Add(new() { TenantId = branch.TenantId, BranchId = branch.Id, ProductId = p.Id }); await db.SaveChangesAsync(); return p.Id;
    }
    [Fact]
    public void Normalization_reencodes_and_resizes_to_webp()
    {
        var result = ProductImageStore.Normalize(Photo()); using var data = SKData.CreateCopy(result); using var codec = SKCodec.Create(data);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat); Assert.Equal(960, codec.Info.Width); Assert.Equal(720, codec.Info.Height);
    }
    [Fact]
    public void Invalid_and_excessive_images_are_rejected_before_storage()
    {
        Assert.Throws<ArgumentException>(() => ProductImageStore.Normalize(Encoding.UTF8.GetBytes("<svg><script>alert(1)</script></svg>")));
        Assert.Throws<ArgumentException>(() => ProductImageStore.Normalize(new byte[ProductImageStore.MaxBytes + 1]));
        Assert.Throws<ArgumentException>(() => ProductImageStore.Normalize(Photo(20, 20)));
        Assert.Throws<ArgumentException>(() => ProductImageStore.Normalize(Photo(6001, 32)));
    }
    [Fact]
    public async Task Upload_requires_owner_csrf_and_owned_product()
    {
        var id = await Product(); var path = $"/api/admin/products/{id}/image?expectedVersion=1"; var bytes = Photo(100, 100);
        using var anonymous = Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await Send(anonymous, HttpMethod.Put, path, bytes)).StatusCode);
        using var barista = Client(); await Login(barista, "barista-a@demo.local"); Assert.Equal(HttpStatusCode.Forbidden, (await Send(barista, HttpMethod.Put, path, bytes)).StatusCode);
        using var foreign = Client(); await Login(foreign, "owner-b@demo.local"); Assert.Equal(HttpStatusCode.NotFound, (await Send(foreign, HttpMethod.Put, path, bytes)).StatusCode);
        using var owner = Client(); await Login(owner, "owner-a@demo.local"); Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, HttpMethod.Put, path, bytes, false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, HttpMethod.Put, path, Encoding.UTF8.GetBytes("not an image"))).StatusCode);
    }
    [Fact]
    public async Task Upload_is_public_only_under_correct_tenant_and_detach_respects_version()
    {
        var id = await Product(); using var c = Client(); await Login(c, "owner-a@demo.local"); var path = $"/api/admin/products/{id}/image?expectedVersion=1&alt=Test%20coffee";
        var response = await Send(c, HttpMethod.Put, path, Photo()); response.EnsureSuccessStatusCode(); var saved = await response.Content.ReadFromJsonAsync<JsonElement>(); var url = saved.GetProperty("imageUrl").GetString()!; Assert.Equal(2, saved.GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, HttpMethod.Put, path, Photo())).StatusCode);
        using var publicClient = Client(); var photo = await publicClient.GetAsync(url); photo.EnsureSuccessStatusCode(); Assert.Equal("image/webp", photo.Content.Headers.ContentType!.MediaType); Assert.Contains("immutable", photo.Headers.CacheControl!.ToString());
        var segments = url.Split('/'); segments[3] = Guid.NewGuid().ToString(); Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync(string.Join('/', segments))).StatusCode);
        var menu = (await c.GetFromJsonAsync<MenuDto>("/api/branches/demo-moda/menu"))!; Assert.Equal(url, menu.Products.Single(x => x.Id == id).ImageUrl);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(c, HttpMethod.Delete, $"/api/admin/products/{id}/image?expectedVersion=1")).StatusCode);
        (await Send(c, HttpMethod.Delete, $"/api/admin/products/{id}/image?expectedVersion=2")).EnsureSuccessStatusCode(); Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync(url)).StatusCode);
    }
    [Fact]
    public async Task Demo_upgrade_is_idempotent_preserves_edits_and_desserts_have_no_coffee_options()
    {
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Slug == "demo-besiktas");
        var products = await db.Products.IgnoreQueryFilters().Where(x => x.TenantId == branch.TenantId).ToListAsync(); Assert.Equal(24, products.Count); Assert.All(products, p => Assert.StartsWith("/images/menu/", p.ImagePath));
        var dessert = products.Single(x => x.Name == "Brownie"); Assert.False(await db.OptionGroups.IgnoreQueryFilters().AnyAsync(x => x.ProductId == dessert.Id));
        var latte = products.Single(x => x.Name == "Latte"); latte.BasePrice = 333; latte.Version++; await db.SaveChangesAsync();
        var marker = await db.DemoSeedMarkers.SingleAsync(x => x.Id == 3); db.DemoSeedMarkers.Remove(marker); await db.SaveChangesAsync();
        await PremiumDemoCatalog.Run(db, 3); await PremiumDemoCatalog.Run(db, 3); Assert.Equal(333, latte.BasePrice); Assert.Equal(24, await db.Products.IgnoreQueryFilters().CountAsync(x => x.TenantId == branch.TenantId));
        var tea = products.Single(x => x.Name == "Çay"); Assert.False(await db.OptionGroups.IgnoreQueryFilters().AnyAsync(x => x.ProductId == tea.Id));
        var mocha = products.Single(x => x.Name == "Mocha"); Assert.Equal(2, await db.OptionGroups.IgnoreQueryFilters().CountAsync(x => x.ProductId == mocha.Id));
    }
}
