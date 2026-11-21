using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Identity.Infrastructure;
using Nirengi.Modules.Identity.Application.Abstractions;
using Testcontainers.PostgreSql;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Runs the real host against a throwaway PostgreSQL container with all module migrations applied.</summary>
public sealed class NirengiApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// One HashKey for every host built from this factory. The e-mail payload is encrypted under it, and hosts made with
    /// <c>WithWebHostBuilder</c> share the database (and so its outbox) with the main one, like instances of one deployment.
    /// </summary>
    public static readonly string TestHashKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public const string AdminEmail = "admin@nirengi.test";

    public FakeEmailSender Mail { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    /// <summary>A stand-in admin build: the host serves whatever is at AdminPanel:Path.</summary>
    public string AdminPanelPath { get; } = CreateAdminBuild();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        Directory.Delete(AdminPanelPath, recursive: true);
    }

    private static string CreateAdminBuild()
    {
        var root = Directory.CreateTempSubdirectory("nirengi-admin-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "index.html"), """<meta name="csp-nonce" content="__CSP_NONCE__"><script type="module" src="/admin/assets/app.js"></script>""");
        File.WriteAllText(Path.Combine(root, "assets", "app.js"), "export {}");
        return root;
    }

    /// <summary>Waits until the Identity outbox has no unprocessed e-mail message left.</summary>
    public async Task WaitForIdentityMailDelivered(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            await using (var scope = Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                var pending = await db.OutboxMessages.CountAsync(m => m.ProcessedAt == null && m.Type == "identity.email-requested", TestContext.Current.CancellationToken);
                if (pending == 0)
                {
                    return;
                }
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("Identity e-mail messages are still waiting in the outbox.");
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Outbox:PollInterval", "00:00:00.200");
        builder.UseSetting("Outbox:BaseDelay", "00:00:00.300");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("AdminPanel:Path", AdminPanelPath);
        builder.UseSetting("Curriculum:AutoPublishSeed", "true");
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "10000");
        builder.UseSetting("RateLimiting:Auth:PerIpPermitLimit", "10000");
        builder.UseSetting("RateLimiting:Api:PermitLimit", "10000");
        builder.UseSetting("RateLimiting:Session:PermitLimit", "10000");
        builder.UseSetting("Identity:Security:HashKey", TestHashKey);
        builder.UseSetting("Identity:AdminEmails:0", AdminEmail);
        builder.UseSetting("Smtp:Host", "localhost");
        builder.UseSetting("Smtp:FromAddress", "test@nirengi.local");
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Mail));
    }
}

/// <summary>All integration tests share one container and one host.</summary>
[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<NirengiApiFactory>
{
    public const string Name = "api";
}
