using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.Modules.Platform.Infrastructure;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>
/// The change feed carries the server's sync epoch (ADR-0022 §4, F2 card 12). A restore renews it, so devices whose
/// cursor ran ahead of the restored sequence start over and send their records again.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class SyncEpochApiTests(NirengiApiFactory factory)
{
    private readonly AuthApi _api = new(factory);

    [Fact]
    public async Task Feed_carries_one_epoch_for_every_caller_and_page()
    {
        var first = await Sync(await _api.RegisterAndVerify(AuthApi.NewEmail()));
        var second = await Sync(await _api.RegisterAndVerify(AuthApi.NewEmail()));

        first.Epoch.ShouldNotBe(Guid.Empty);
        second.Epoch.ShouldBe(first.Epoch);
    }

    [Fact]
    public async Task Renewed_epoch_shows_on_the_next_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var before = (await Sync(tokens)).Epoch;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // The statement deploy/bin/restore.sh runs after pg_restore.
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database
                .ExecuteSqlRawAsync("UPDATE platform.sync_epoch SET epoch = gen_random_uuid()", ct);
        }

        var after = (await Sync(tokens)).Epoch;
        after.ShouldNotBe(before);
        after.ShouldNotBe(Guid.Empty);
    }

    private async Task<SyncItem> Sync(AuthApi.Tokens tokens) =>
        await AuthApi.Read<SyncItem>(await _api.Send(HttpMethod.Get, "/v1/sync?since=0", tokens.AccessToken));

    private sealed record SyncItem(Guid Epoch, long Cursor);
}
