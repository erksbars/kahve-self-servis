using System.Text.Json;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Orders;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Payments.Iyzico;
public sealed record IyzicoCustomer(string Name, string Surname, string IdentityNumber, string Email, string Phone, string Address, string City, string Country);
public sealed class IyzicoFlow(AppDbContext db, CapabilityTokens tokens, IyzicoMerchantAccounts accounts, IIyzicoCheckoutClient client, PaymentSettlement settlement, IConfiguration config)
{
    private AppDbContext Db => db;
    private CapabilityTokens TokenStore => tokens;
    public static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/api/iyzico/sessions/{token}", (string token, IyzicoFlow flow, CancellationToken ct) => flow.Session(token, ct)).RequireRateLimiting("tracking");
        app.MapPost("/api/iyzico/sessions/{token}/initialize", (string token, IyzicoCustomer input, HttpContext http, IyzicoFlow flow, CancellationToken ct) => flow.Initialize(token, input, http.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", ct)).RequireRateLimiting("quote");
        app.MapPost("/api/iyzico/sessions/{token}/verify", async (string token, IyzicoFlow flow, CancellationToken ct) =>
        {
            var payment = await flow.Find(token, ct);
            if (payment is null) return Results.NotFound();
            if (payment.ProtectedProviderToken != "") await flow.Verify(payment.Id, flow.TokenStore.Unprotect(payment.ProtectedProviderToken), ct);
            return await flow.Session(token, ct);
        }).RequireRateLimiting("quote");
        app.MapPost("/api/payments/iyzico/callback/{reference}", async (string reference, HttpContext http, IyzicoFlow flow, CancellationToken ct) =>
        {
            if (reference.Length != 32 || !reference.All(char.IsAsciiHexDigit) || !http.Request.HasFormContentType) return Results.BadRequest();
            var form = await http.Request.ReadFormAsync(ct); var providerToken = form["token"].ToString();
            if (providerToken.Length is < 1 or > 500) return Results.BadRequest();
            var payment = await flow.Db.Payments.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Provider == "iyzico" && x.ProviderReference == reference, ct);
            if (payment is null) return Results.NotFound();
            if (!await flow.Verify(payment.Id, providerToken, ct)) return Results.Json(new { message = "Ödeme sonucu henüz doğrulanamadı." }, statusCode: 503);
            return Results.Redirect($"http://127.0.0.1:5187/iyzico/pay/{flow.TokenStore.Unprotect(payment.ProtectedSessionToken)}");
        }).WithMetadata(new SignedWebhookEndpoint()).RequireRateLimiting("webhook");
    }
    private Task<Payment?> Find(string token, CancellationToken ct) => !CapabilityTokens.Valid(token) ? Task.FromResult<Payment?>(null) : db.Payments.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Provider == "iyzico" && x.SessionTokenHash == CapabilityTokens.Hash(token), ct);
    private async Task<IResult> Session(string token, CancellationToken ct)
    {
        var payment = await Find(token, ct); if (payment is null) return Results.NotFound();
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == payment.OrderId && x.TenantId == payment.TenantId, ct);
        var branch = await db.Branches.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == payment.BranchId && x.TenantId == payment.TenantId, ct);
        return Results.Ok(new { amount = payment.Amount, payment.Currency, paymentStatus = payment.Status, initializationState = payment.InitializationState, order.ExpiresAt, expired = order.ExpiresAt <= DateTimeOffset.UtcNow, branch = new { branch.Name, branch.Slug }, items = JsonSerializer.Deserialize<List<QuotedItem>>(order.ItemsJson, CheckoutEndpoints.Json), trackingUrl = payment.Status == "Paid" ? $"/orders/track/{tokens.Unprotect(order.ProtectedTrackingToken)}" : null, paymentUrl = payment.Status == "Pending" && payment.InitializationState == "Ready" && payment.ProtectedPaymentUrl != "" && order.ExpiresAt > DateTimeOffset.UtcNow ? tokens.Unprotect(payment.ProtectedPaymentUrl) : null, review = payment.InitializationState == "Review", demo = true });
    }
    private async Task<IResult> Initialize(string token, IyzicoCustomer input, string ip, CancellationToken ct)
    {
        var payment = await Find(token, ct); if (payment is null) return Results.NotFound();
        var credentials = accounts.Find(payment.TenantId); if (credentials is null) return Results.Json(new { message = "Şubenin ödeme hesabı hazır değil." }, statusCode: 503);
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == payment.OrderId && x.TenantId == payment.TenantId, ct);
        if (payment.Status != "Pending" || order.ExpiresAt <= DateTimeOffset.UtcNow) return Results.Conflict(new { message = "Bu ödeme oturumu yeni ödeme başlatamaz. Sonucunu kontrol edin." });
        var fields = new[] { input.Name, input.Surname, input.IdentityNumber, input.Email, input.Phone, input.Address, input.City, input.Country };
        if (fields.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 250) || !System.Net.Mail.MailAddress.TryCreate(input.Email, out _)) return Results.BadRequest(new { message = "Alıcı bilgilerini kontrol edin." });
        var address = new IyzicoAddress(input.Address, $"{input.Name} {input.Surname}", input.City, input.Country);
        var snapshot = JsonSerializer.Deserialize<List<QuotedItem>>(order.ItemsJson, CheckoutEndpoints.Json)!;
        var request = new IyzicoInitializeRequest(payment.ProviderReference, order.Id.ToString("N"), payment.Amount, new Uri(config["Iyzico:CallbackOrigin"]!.TrimEnd('/') + $"/api/payments/iyzico/callback/{payment.ProviderReference}"), new(payment.Id.ToString("N"), input.Name, input.Surname, input.IdentityNumber, input.Email, input.Phone, input.Address, input.City, input.Country, ip), address, address, snapshot.Select((x, index) => new IyzicoBasketItem($"{order.Id:N}-{index}", x.Name, "Kahve", x.LineTotal)).ToList());
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await DatabaseLocks.Acquire(db, $"iyzico-init:{payment.Id}", ct);
            var current = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == payment.Id && x.TenantId == payment.TenantId, ct);
            if (current.Status != "Pending") return Results.Conflict(new { message = "Ödeme sonucu alınmış; bu sayfada sonucunu kontrol edin." });
            if (current.InitializationState == "Ready" && current.ProtectedPaymentUrl != "") return Results.Ok(new { paymentUrl = tokens.Unprotect(current.ProtectedPaymentUrl) });
            if (current.InitializationState is not ("NotStarted" or "Rejected")) return Results.Conflict(new { message = "Ödeme başlatma sonucu kontrol ediliyor. Yeni ödeme başlatmayın; bu sayfayı koruyun." });
            current.InitializationState = "Initializing"; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        // No external HTTP request inside a database transaction. One persisted attempt only.
        IyzicoSession session;
        try { session = await client.Initialize(credentials, request, ct); }
        catch (Exception e) when (e is IyzicoProtocolException or ArgumentException)
        {
            await SaveInitialization(payment.Id, null, e is IyzicoProtocolException p && p.MayHaveCreatedSession ? "Unknown" : "Rejected", e is IyzicoProtocolException p2 ? p2.Code : "invalid_customer", CancellationToken.None);
            if (e is ArgumentException or IyzicoProtocolException { MayHaveCreatedSession: false }) return Results.Json(new { message = "iyzico ödeme isteğini reddetti. Bilgileri kontrol edin; hata sürerse işletmeye başvurun." }, statusCode: 422);
            return Results.Json(new { message = "Ödeme oturumu açılamadı veya sonucu belirsiz. Tekrar ödeme başlatmadan sonucu kontrol edin." }, statusCode: 502);
        }
        await SaveInitialization(payment.Id, session, "Ready", null, CancellationToken.None);
        return Results.Ok(new { paymentUrl = session.PaymentPageUrl.AbsoluteUri });
    }
    private async Task SaveInitialization(Guid id, IyzicoSession? session, string state, string? error, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(ct); await DatabaseLocks.Acquire(db, $"iyzico-init:{id}", ct);
        var current = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == id, ct);
        // A callback may have verified the payment while initialize was returning.
        if (current.Status != "Pending" || current.InitializationState == "Review") return;
        current.InitializationState = state; current.LastProviderError = error;
        if (session is not null) { current.ProtectedProviderToken = tokens.Protect(session.Token); current.ProtectedPaymentUrl = tokens.Protect(session.PaymentPageUrl.AbsoluteUri); }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<bool> Verify(Guid id, string providerToken, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var payment = await db.Payments.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Provider == "iyzico", ct); if (payment is null) return false;
        if (payment.ProtectedProviderToken != "" && tokens.Unprotect(payment.ProtectedProviderToken) != providerToken) return false;
        var credentials = accounts.Find(payment.TenantId); if (credentials is null) return false;
        IyzicoResult result;
        try { result = await client.Retrieve(credentials, new(payment.ProviderReference, payment.OrderId.ToString("N"), providerToken, payment.Amount), ct); }
        catch (IyzicoProtocolException e)
        {
            await using var errorTx = await db.Database.BeginTransactionAsync(CancellationToken.None);
            await DatabaseLocks.Acquire(db, $"iyzico-init:{id}", CancellationToken.None);
            var failed = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == id, CancellationToken.None);
            if (failed.Status == "Pending") { failed.LastProviderError = e.Code; failed.LastVerifiedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(CancellationToken.None); }
            await errorTx.CommitAsync(CancellationToken.None); return false;
        }
        var canonical = JsonSerializer.Serialize(result, CheckoutEndpoints.Json);
        if (result.Currency != payment.Currency || (result.Outcome == IyzicoOutcome.Confirmed && result.Amount != payment.Amount) || (result.Outcome == IyzicoOutcome.Failed && result.Amount != 0)) return false;
        if (result.Outcome == IyzicoOutcome.Confirmed)
        {
            var orderSnapshot = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == payment.OrderId && x.TenantId == payment.TenantId, ct);
            var items = JsonSerializer.Deserialize<List<QuotedItem>>(orderSnapshot.ItemsJson, CheckoutEndpoints.Json)!;
            var expectedIds = items.Select((_, i) => $"{payment.OrderId:N}-{i}").ToHashSet();
            if (result.Lines.Count != expectedIds.Count || !expectedIds.SetEquals(result.Lines.Select(x => x.ItemId))) return false;
        }
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await DatabaseLocks.Acquire(db, $"iyzico-init:{id}", ct); var current = await db.Payments.IgnoreQueryFilters().SingleAsync(x => x.Id == id && x.TenantId == payment.TenantId, ct);
            current.ProtectedProviderToken = tokens.Protect(providerToken); current.ProviderResultJson = canonical; current.LastVerifiedAt = DateTimeOffset.UtcNow; current.LastProviderError = null;
            if (current.Status == "Pending") current.InitializationState = result.Outcome == IyzicoOutcome.Review ? "Review" : "Ready";
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        if (result.Outcome == IyzicoOutcome.Review) return true;
        db.ChangeTracker.Clear();
        var status = result.Outcome == IyzicoOutcome.Confirmed ? "paid" : "failed";
        var processed = await settlement.Apply(new("iyzico", $"{result.PaymentId}:{status}", payment.ProviderReference, status, payment.Amount, result.Currency, CapabilityTokens.Hash(canonical), RejectAfterExpiry: false), ct);
        return processed is not IStatusCodeHttpResult { StatusCode: >= 400 };
    }
}
