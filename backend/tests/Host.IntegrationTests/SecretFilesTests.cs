using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Needs no container: only the configuration source is exercised.</summary>
public sealed class SecretFilesTests
{
    [Fact]
    public void A_secret_file_becomes_a_setting_named_by_its_file_name_and_wins_over_earlier_sources()
    {
        var directory = Directory.CreateTempSubdirectory("nirengi-secrets-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "ConnectionStrings__Database"), "Host=secret");

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = "Host=appsettings" })
                .AddSecretFiles(directory)
                .Build();

            configuration.GetConnectionString("Database").ShouldBe("Host=secret");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_missing_secrets_directory_is_not_an_error() =>
        Should.NotThrow(() => new ConfigurationBuilder().AddSecretFiles(Path.Combine(Path.GetTempPath(), "nirengi-no-such-dir")).Build());
}
