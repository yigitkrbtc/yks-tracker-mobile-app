using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Identity.Application.Abstractions;
using Testcontainers.PostgreSql;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Runs the real host against a throwaway PostgreSQL container with all module migrations applied.</summary>
public sealed class NirengiApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public static readonly string TestSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public const string AdminEmail = "admin@nirengi.test";

    public FakeEmailSender Mail { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Outbox:PollInterval", "00:00:00.200");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "10000");
        builder.UseSetting("Identity:Security:HashKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
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
