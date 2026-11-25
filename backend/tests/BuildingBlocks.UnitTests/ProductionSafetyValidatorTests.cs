using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Nirengi.BuildingBlocks.Api.Security;
using Shouldly;

namespace Nirengi.BuildingBlocks.UnitTests;

public sealed class ProductionSafetyValidatorTests
{
    private const string Proxy = "10.0.0.7";

    [Theory]
    [InlineData("Host=db;Include Error Detail=true")]
    [InlineData("Host=db;include error detail = True")]
    [InlineData("Host=db;IncludeErrorDetail=yes")]
    [InlineData("Host=db;Include_Error_Detail=1")]
    public void Production_rejects_error_detail_in_the_connection_string(string connectionString)
    {
        var result = Validate("Production", connectionString);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Include Error Detail");
        result.FailureMessage.ShouldNotContain("Host=db");
    }

    [Theory]
    [InlineData("Host=db;Username=app")]
    [InlineData("Host=db;Include Error Detail=false")]
    public void Production_accepts_a_connection_string_without_error_detail(string connectionString) =>
        Validate("Production", connectionString).Succeeded.ShouldBeTrue();

    [Fact]
    public void Development_may_use_error_detail() =>
        Validate("Development", "Host=localhost;Include Error Detail=true").Succeeded.ShouldBeTrue();

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(string environment, string connectionString)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = connectionString,
            ["ForwardedHeaders:KnownProxies:0"] = Proxy,
        }).Build();
        return new ProductionSafetyValidator(new FakeEnvironment(environment), configuration).Validate(null, new ProductionSafetyOptions());
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
