using System.Data;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Orders;
using Kahve.Api.Modules.Music;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kahve.Api.Modules.Payments;

public sealed record CheckoutRequest(string? BranchSlug, List<QuoteItemRequest?>? Items, decimal ExpectedTotal, string? RequestedSong = null);
public sealed record CheckoutReply(string PaymentUrl, decimal Amount, string Currency, DateTimeOffset ExpiresAt);

public static class CheckoutEndpoints
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static bool Enabled(IConfiguration config) => PaymentProviders.Selection(config) != "none";
    public static void MapCheckoutEndpoints(this WebApplication app)
    {
        if (Enabled(app.Configuration)) app.MapPost("/api/checkouts", Create).RequireRateLimiting("quote");
        app.MapGet("/api/orders/track/{token}", Track).RequireRateLimiting("tracking");
        app.MapGet("/api/staff/branches/{branchId:guid}/orders", async (Guid branchId, AppDbContext db, RequestScope scope) =>
        {
            if (!await db.Branches.AnyAsync(x => x.Id == branchId && db.StaffBranchAccess.Any(a => a.BranchId == x.Id && a.StaffId == scope.StaffId))) return Results.NotFound();
            var orders = await db.Orders.AsNoTracking().Where(x => x.BranchId == branchId && x.PaymentStatus == "Paid" && new[] { "Received", "Preparing", "Ready" }.Contains(x.Status)).OrderBy(x => x.PaidAt)
                .Select(x => new { x.Id, x.PublicNumber, x.Status, x.Version, x.PaidAt, x.ItemsJson }).ToListAsync();
            return Results.Ok(orders.Select(x => new { x.Id, x.PublicNumber, x.Status, x.Version, x.PaidAt, items = JsonSerializer.Deserialize<List<QuotedItem>>(x.ItemsJson, Json), paymentVerified = true }));
        }).RequireAuthorization();
    }

    private static async Task<IResult> Create(CheckoutRequest request, HttpContext http, AppDbContext db, CapabilityTokens tokens, PaymentProviders providers, IConfiguration config, CancellationToken ct)
    {
        if (!Enabled(config)) return Results.NotFound();
        if (!Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var key) || key == Guid.Empty)
            return Results.BadRequest(new { message = "Checkout için geçerli bir işlem anahtarı gerekli." });
        if (string.IsNullOrWhiteSpace(request.BranchSlug) || request.BranchSlug.Length > 100) return Results.BadRequest(new { message = "Geçerli bir şube seçin." });
        var requestedSong = string.IsNullOrWhiteSpace(request.RequestedSong) ? null : request.RequestedSong.Trim();
        if (request.RequestedSong?.Length > 240) return Results.BadRequest(new { message = "Şarkı isteği en fazla 240 karakter olabilir." });
        var items = request.Items?.Select(x => x is null ? null : new { x.ProductId, x.Quantity, options = x.OptionValueIds?.Order().ToArray() });
        // Keep the old fingerprint for checkouts without a song so existing retries remain valid.
        object hashInput = requestedSong is null ? new { request.BranchSlug, request.ExpectedTotal, items } : new { request.BranchSlug, request.ExpectedTotal, items, requestedSong };
        var requestHash = CapabilityTokens.Hash(JsonSerializer.Serialize(hashInput, Json));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            try
            {
                await DatabaseLocks.Acquire(db, $"checkout:{request.BranchSlug}:{key}", ct);
                var branch = await db.Branches.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Slug == request.BranchSlug, ct);
                if (branch is null) return Results.NotFound(new { message = "Şube bulunamadı." });
                var existing = await db.Payments.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branch.Id && x.IdempotencyKey == key, ct);
                if (existing is not null)
                {
                    if (existing.RequestHash != requestHash) return Results.Conflict(new { code = "idempotency_conflict", message = "Aynı işlem anahtarı farklı bir sepet için kullanılamaz." });
                    var oldOrder = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == existing.OrderId && x.TenantId == existing.TenantId, ct);
                    var originalProvider = providers.Resolve(existing.Provider);
                    if (originalProvider is null) return Results.Json(new { code = "provider_unavailable", message = "Bu ödeme oturumu şu anda kullanılamıyor. Daha sonra aynı işlemle yeniden deneyin." }, statusCode: 503);
                    return Results.Ok(new CheckoutReply(originalProvider.CheckoutPath(tokens.Unprotect(existing.ProtectedSessionToken)), existing.Amount, existing.Currency, oldOrder.ExpiresAt));
                }
                var result = await QuoteEndpoints.Calculate(new(request.BranchSlug, request.Items), db, ct);
                if (result.Error is not null) return result.Error;
                var quote = result.Quote!;
                if (quote.Total != request.ExpectedTotal) return Results.Conflict(new { code = "price_changed", message = "Sepetin fiyatı değişti. Güncel tutarı yeniden doğrulayıp onaylayın." });
                if (quote.Total > 9999999999.99m) return Results.BadRequest(new { message = "Sepet tutarı işlem sınırını aşıyor." });
                var tracking = CapabilityTokens.New();
                var provider = providers.Selected;
                if (provider.Name == "iyzico")
                {
                    var accounts = http.RequestServices.GetRequiredService<Iyzico.IyzicoMerchantAccounts>();
                    try { if (accounts.Find(branch.TenantId) is null) return Results.Json(new { message = "Bu şubenin online ödeme bağlantısı henüz hazır değil." }, statusCode: 503); }
                    catch (Exception e) when (e is InvalidOperationException or ArgumentException) { return Results.Json(new { message = "Bu şubenin online ödeme ayarı kontrol edilmeli." }, statusCode: 503); }
                    if (quote.Total <= 0) return Results.BadRequest(new { message = "iyzico ödeme toplamı sıfırdan büyük olmalı." });
                }
                var session = CapabilityTokens.New();
                // PostgreSQL stores microseconds; keep the first reply identical to DB-backed retries.
                var utcNow = DateTimeOffset.UtcNow;
                var now = new DateTimeOffset(utcNow.Ticks - utcNow.Ticks % 10, TimeSpan.Zero);
                var order = new Order { Id = Guid.NewGuid(), TenantId = branch.TenantId, BranchId = branch.Id, Total = quote.Total, ItemsJson = JsonSerializer.Serialize(quote.Items, Json), CreatedAt = now, ExpiresAt = now.AddMinutes(15), TrackingTokenHash = CapabilityTokens.Hash(tracking), ProtectedTrackingToken = tokens.Protect(tracking) };
                db.Orders.Add(order);
                if (requestedSong is not null)
                    db.SongRequests.Add(new SongRequest { Id = Guid.NewGuid(), TenantId = branch.TenantId, BranchId = branch.Id, OrderId = order.Id, RequestedText = requestedSong, CreatedAt = now });
                db.Payments.Add(new Payment { Id = Guid.NewGuid(), TenantId = branch.TenantId, BranchId = branch.Id, OrderId = order.Id, IdempotencyKey = key, RequestHash = requestHash, Provider = provider.Name, ProviderReference = provider.CreateReference(), Amount = order.Total, SessionTokenHash = CapabilityTokens.Hash(session), ProtectedSessionToken = tokens.Protect(session) });
                db.OrderStatusHistory.Add(new() { TenantId = branch.TenantId, OrderId = order.Id, ToStatus = "PaymentPending", Actor = "customer", Reason = "checkout.created", OccurredAt = now });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Results.Ok(new CheckoutReply(provider.CheckoutPath(session), order.Total, order.Currency, order.ExpiresAt));
            }
            catch (Exception e) when (e is PostgresException { SqlState: "40001" } || e is DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "40001" } })
            {
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
        }
        return Results.Conflict(new { message = "Checkout başka bir istekte işleniyor. Aynı işlemle yeniden deneyin." });
    }

    private static async Task<IResult> Track(string token, AppDbContext db, CancellationToken ct)
    {
        if (!CapabilityTokens.Valid(token)) return Results.NotFound();
        var hash = CapabilityTokens.Hash(token);
        // Public capability grants access to one order only, never to a branch/tenant list.
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.TrackingTokenHash == hash, ct);
        if (order is null) return Results.NotFound();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(x => x.Id == order.BranchId && x.TenantId == order.TenantId, ct);
        var songRequest = order.PaymentStatus == "Paid" ? await db.SongRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OrderId == order.Id && r.TenantId == order.TenantId && r.BranchId == order.BranchId && r.RequestedText != null)
            .Select(r => new { text = r.RequestedText, r.Status }).SingleOrDefaultAsync(ct) : null;
        return Results.Ok(new { order.PublicNumber, order.Status, order.PaymentStatus, order.Total, order.Currency, order.CreatedAt, order.PaidAt, branch = new { branch.Name, branch.Slug }, items = JsonSerializer.Deserialize<List<QuotedItem>>(order.ItemsJson, Json), songRequest, demo = true });
    }
}
