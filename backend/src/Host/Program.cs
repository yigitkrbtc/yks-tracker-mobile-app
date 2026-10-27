using System.Reflection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Nirengi.BuildingBlocks.Api.Security;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Host;

if (args.Contains("--health-probe"))
{
    // Container HEALTHCHECK: the runtime image ships without curl.
    return await HealthProbe.Run(Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080");
}

var builder = WebApplication.CreateBuilder(args);

if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    // Build-time OpenAPI generation starts the host without secrets; give startup validation throwaway values.
    builder.Configuration.AddInMemoryCollection(OpenApiBuildSecrets.Values);
}

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");
builder.Services.AddNirengiSecurity(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString(ModuleInfrastructureRegistration.ConnectionStringName)!,
        name: "postgres",
        tags: ["ready"]);

foreach (var module in ModuleCatalog.All)
{
    module.AddServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseNirengiSecurity();

app.MapOpenApi("/openapi/{documentName}.json");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

var v1 = app.MapGroup("/v1");
foreach (var module in ModuleCatalog.All)
{
    module.MapEndpoints(v1.MapGroup(string.Empty).WithTags(module.Name));
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await DatabaseMigrator.MigrateAll(app.Services);
}

await app.RunAsync();
return 0;

public partial class Program;
