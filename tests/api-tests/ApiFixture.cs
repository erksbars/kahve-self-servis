using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Kahve.Api.Tests;

public sealed class ApiFixture : IAsyncLifetime
{
    public const string Password = "Integration-test-only-42!";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        Factory = new ConfiguredFactory(database.GetConnectionString());
    }
    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await database.DisposeAsync();
    }
    private sealed class ConfiguredFactory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Database", connection);
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("Demo:Enabled", "true");
            builder.UseSetting("Demo:Password", Password);
            builder.UseSetting("Sandbox:Enabled", "true");
            builder.UseSetting("Payments:WebhookSecret", "Test-webhook-only-secret-at-least-32-characters");
            builder.UseSetting("Tracking:EncryptionKey", "Test-tracking-only-secret-at-least-32-characters");
            builder.UseSetting("Outbox:RunWorker", "false");
            builder.UseSetting("Realtime:RunWorker", "false");
            builder.UseSetting("Iyzico:RunWorker", "false");
        }
    }
}
