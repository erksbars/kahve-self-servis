using System.Threading.RateLimiting;
using Kahve.Api.Infrastructure;
using Kahve.Api.Modules.Catalog;
using Kahve.Api.Modules.Staff;
using Kahve.Api.Modules.Orders;
using Kahve.Api.Modules.Payments;
using Kahve.Api.Modules.Realtime;
using Kahve.Api.Modules.Admin;
using Kahve.Api.Modules.Music;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<RequestScope>();
builder.Services.AddSingleton<ProductImageStore>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings__Database is required.")));
builder.Services.AddProblemDetails();
builder.Services.AddSignalR(o => { o.MaximumReceiveMessageSize = 4096; o.EnableDetailedErrors = false; });
builder.Services.AddSingleton<StaffSubscriptions>();
builder.Services.AddScoped<RealtimePump>();
builder.Services.AddHostedService<RealtimeWorker>();
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);
var local = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
if (builder.Configuration.GetValue<bool>("Sandbox:Enabled") && !local) throw new InvalidOperationException("Sandbox payments are restricted to Development/Testing.");
builder.Services.AddSingleton<CapabilityTokens>();
builder.Services.AddSingleton<SandboxPaymentProvider>();
builder.Services.AddSingleton<IPaymentProvider>(sp => sp.GetRequiredService<SandboxPaymentProvider>());
builder.Services.AddSingleton<IPaymentProvider, Kahve.Api.Modules.Payments.Iyzico.IyzicoPaymentProvider>();
builder.Services.AddScoped<Kahve.Api.Modules.Payments.Iyzico.IyzicoFlow>();
builder.Services.AddHostedService<Kahve.Api.Modules.Payments.Iyzico.IyzicoReconciliationWorker>();
builder.Services.AddSingleton<PaymentProviders>();
builder.Services.AddScoped<PaymentSettlement>();
builder.Services.AddSingleton<Kahve.Api.Modules.Payments.Iyzico.IyzicoMerchantAccounts>();
builder.Services.AddHttpClient<Kahve.Api.Modules.Payments.Iyzico.IyzicoCheckoutClient>(client => client.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddTransient<Kahve.Api.Modules.Payments.Iyzico.IIyzicoCheckoutClient>(sp => sp.GetRequiredService<Kahve.Api.Modules.Payments.Iyzico.IyzicoCheckoutClient>());
builder.Services.AddScoped<WebhookProcessor>();
builder.Services.AddScoped<IOrderReceiver, OrderReceiver>();
builder.Services.AddScoped<OutboxPump>();
builder.Services.AddHostedService<PaymentOutboxWorker>();
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "kahve.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = false;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = AuthEndpoints.ValidatePrincipal;
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("music", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("tracking", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("webhook", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("quote", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("imageUpload", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("invitation", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
// Reject an unsupported selection before serving traffic; never silently charge via another adapter.
_ = app.Services.GetRequiredService<PaymentProviders>();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    var imageLimit = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata>();
    var bodyLimit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (imageLimit is not null && bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = imageLimit.MaxRequestBodySize;
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var isHub = context.Request.Path.StartsWithSegments("/hubs/orders");
    if (isHub && context.Request.Headers.Origin.Count > 0 && (!Uri.TryCreate(context.Request.Headers.Origin, UriKind.Absolute, out var origin) || !string.Equals(origin.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase) || origin.Scheme != context.Request.Scheme)) { context.Response.StatusCode = 403; return; }
    if (!isHub && (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method)) && context.GetEndpoint()?.Metadata.GetMetadata<SignedWebhookEndpoint>() is null)
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Güvenlik doğrulaması başarısız. Sayfayı yenileyin." }); return; }
    }
    await next();
});
app.MapGet("/api/health", async (AppDbContext db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapStaffEndpoints();
app.MapCatalogEndpoints();
ProductImageStore.MapPublic(app);
app.MapQuoteEndpoints();
app.MapCheckoutEndpoints();
app.MapSandboxEndpoints();
app.MapStaffOrderEndpoints();
app.MapAdminEndpoints();
app.MapMusicEndpoints();
Kahve.Api.Modules.Payments.Iyzico.IyzicoMerchantAccounts.MapReadiness(app);
Kahve.Api.Modules.Payments.Iyzico.IyzicoFlow.MapEndpoints(app);
app.MapHub<OrderHub>("/hubs/orders", o => o.CloseOnAuthenticationExpiration = true);
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (app.Configuration.GetValue<bool>("Demo:Enabled"))
    {
        if (!local) throw new InvalidOperationException("Demo seed is restricted to Development/Testing.");
        await DemoSeed.Run(db, app.Configuration["Demo:Password"] ?? throw new InvalidOperationException("Demo__Password is required."));
    }
}
app.Run();
public partial class Program;

