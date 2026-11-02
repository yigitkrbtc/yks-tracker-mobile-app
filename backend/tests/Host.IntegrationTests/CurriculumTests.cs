using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain.Versions;
using Nirengi.Modules.Curriculum.Infrastructure.Persistence;
using Nirengi.Modules.Curriculum.Contracts;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Curriculum.Infrastructure;
using Nirengi.Modules.Curriculum.Infrastructure.Seed;
using Nirengi.Modules.Tracking.Domain.Profiles;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(ApiTestGroup.Name)]
public sealed class CurriculumTests(NirengiApiFactory factory)
{
    private readonly AuthApi _api = new(factory);

    [Fact]
    public async Task Curriculum_requires_sign_in()
    {
        var response = await _api.Client.GetAsync("/v1/curriculum", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Seeded_curriculum_is_served_whole_and_revalidated_by_etag()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var response = await _api.Send(HttpMethod.Get, "/v1/curriculum", tokens.AccessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var etag = response.Headers.ETag.ShouldNotBeNull().ToString();
        var curriculum = await AuthApi.Read<Curriculum>(response);
        etag.ShouldBe($"\"{curriculum.Version}\"");
        curriculum.ExamYear.ShouldBe(2027);
        curriculum.Subjects.Count.ShouldBe(20);
        curriculum.Subjects.Sum(s => s.Topics.Count).ShouldBe(370);
        curriculum.ScoreTypes.Single(s => s.Code == "SAY").TestCodes.ShouldContain("AYT-FEN");
        var integral = curriculum.Subjects.Single(s => s.Code == "AYT.AMAT").Topics.Single(t => t.Code == "AYT.AMAT.26");
        integral.Id.ShouldBe(TopicIdentity.For("AYT.AMAT.26"));
        integral.Tier.ShouldBe("High");
        curriculum.MockSections.Single(s => s.Code == "AYT-SB2.FEL2").AlternativeOf.ShouldBe("AYT-SB2.DKAB");
        var pastPapers = curriculum.Sources.Single(s => s.Code == "OSYM");
        pastPapers.Id.ShouldBe(SourceIdentity.For("OSYM"));
        pastPapers.Kind.ShouldBe("Official");

        var revalidated = await _api.Send(HttpMethod.Get, "/v1/curriculum", tokens.AccessToken, ifNoneMatch: etag);
        revalidated.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Tracking_score_types_match_curriculum_codes()
    {
        var tokens = await _api.RegisterAndVerify(AuthApi.NewEmail());

        var curriculum = await AuthApi.Read<Curriculum>(await _api.Send(HttpMethod.Get, "/v1/curriculum", tokens.AccessToken));

        curriculum.ScoreTypes.Select(s => s.Code).ShouldBe(Enum.GetNames<ScoreType>(), ignoreOrder: true);
    }

    [Fact]
    public async Task Reseeding_is_idempotent_and_retires_dropped_topics()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<CurriculumSeeder>();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        (await seeder.Seed(ct)).ShouldBeFalse();

        var shipped = CurriculumSeed.LoadEmbedded();
        var dropped = shipped.Topics[0];
        var trimmed = shipped with { Topics = [.. shipped.Topics.Skip(1)], Hash = new string('0', 64) };
        try
        {
            (await seeder.Apply(trimmed, ct)).ShouldBeTrue();
            var retired = await db.Topics.AsNoTracking().SingleAsync(t => t.Code == dropped.Code, ct);
            retired.Status.ShouldBe(TopicStatus.Retired);
            (await db.TopicWeights.AnyAsync(w => w.TopicId == retired.Id, ct)).ShouldBeFalse();
        }
        finally
        {
            (await seeder.Seed(ct)).ShouldBeTrue();
        }

        var restored = await db.Topics.AsNoTracking().SingleAsync(t => t.Code == dropped.Code, ct);
        restored.Status.ShouldBe(TopicStatus.Active);
        (await db.Topics.CountAsync(ct)).ShouldBe(370);
        (await db.TopicWeights.CountAsync(ct)).ShouldBe(shipped.Topics.Sum(t => t.Weights.Count));
    }

    [Fact]
    public async Task Without_auto_publish_a_changed_seed_waits_as_a_draft()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<CurriculumDbContext>();
        var seeder = new CurriculumSeeder(
            db,
            services.GetRequiredService<CurriculumPublisher>(),
            services.GetRequiredService<IClock>(),
            Options.Create(new CurriculumOptions { AutoPublishSeed = false }),
            NullLogger<CurriculumSeeder>.Instance);
        var shipped = CurriculumSeed.LoadEmbedded();
        var before = await db.Releases.AsNoTracking().SingleAsync(ct);
        var renamed = shipped with
        {
            Topics = [shipped.Topics[0] with { Name = "Yeniden adlandırıldı" }, .. shipped.Topics.Skip(1)],
            Hash = new string('1', 64),
        };

        // An upgraded database: calendars never stored, weights from before per-test rows.
        await db.Calendars.ExecuteDeleteAsync(ct);
        await db.TopicWeights.Where(w => w.TestCode == "TYT-MAT").ExecuteDeleteAsync(ct);

        try
        {
            (await seeder.Apply(renamed, ct)).ShouldBeTrue();

            var draft = await db.Drafts.AsNoTracking().SingleAsync(d => d.Kind == DraftKind.SeedImport, ct);
            var update = CurriculumJson.Operations(draft.Operations).ShouldHaveSingleItem().ShouldBeOfType<UpdateTopic>();
            update.Code.ShouldBe(shipped.Topics[0].Code);
            update.Name.ShouldBe("Yeniden adlandırıldı");
            var after = await db.Releases.AsNoTracking().SingleAsync(ct);
            after.VersionNumber.ShouldBe(before.VersionNumber);
            after.ContentHash.ShouldNotBe(before.ContentHash);

            // Seed-only parts did not wait for the admin: q and the calendar are served right away.
            (await db.Calendars.CountAsync(ct)).ShouldBe(shipped.Calendars.Count);
            (await db.TopicWeights.CountAsync(ct)).ShouldBe(shipped.Topics.Sum(t => t.Weights.Count));
            (await db.Topics.AsNoTracking().SingleAsync(t => t.Code == shipped.Topics[0].Code, ct)).Name.ShouldBe(shipped.Topics[0].Name);

            (await seeder.Apply(renamed, ct)).ShouldBeFalse(); // already waiting
        }
        finally
        {
            await db.Drafts.Where(d => d.Kind == DraftKind.SeedImport).ExecuteDeleteAsync(ct);
            (await services.GetRequiredService<CurriculumSeeder>().Seed(ct)).ShouldBeTrue(); // back to the shipped seed
        }
    }

    [Fact]
    public async Task Catalog_tells_other_modules_which_topics_and_sources_exist()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<ICurriculumCatalog>();
        var integral = TopicIdentity.For("AYT.AMAT.26");

        var topics = await catalog.GetTopics([integral, Guid.NewGuid()], ct);

        topics.ShouldHaveSingleItem().ShouldBe(new CatalogTopic(integral, "AYT.AMAT", true));
        (await catalog.GetSource(SourceIdentity.For("OSYM"), ct)).ShouldBe(new CatalogSource(SourceIdentity.For("OSYM"), true));
        (await catalog.GetSource(Guid.NewGuid(), ct)).ShouldBeNull();

        var sections = await catalog.GetMockSections(ct);
        sections.Count.ShouldBe(27);
        sections.Single(s => s.Code == "TYT-MAT").SubjectCodes.ShouldBe(["TYT.TMAT", "TYT.GEO"]);
        sections.Single(s => s.Code == "AYT-SB2.TAR").ShouldSatisfyAllConditions(
            s => s.Session.ShouldBe("AYT"),
            s => s.SubjectCodes.ShouldBe(["AYT.TAR"]),
            s => s.ParentCode.ShouldBe("AYT-SB2"));
    }

    private sealed record Curriculum(
        string Version,
        int ExamYear,
        List<ScoreTypeItem> ScoreTypes,
        List<SubjectItem> Subjects,
        List<SourceItem> Sources,
        List<MockSectionItem> MockSections);

    private sealed record MockSectionItem(string Code, string TestCode, string? SubjectCode, int QuestionCount, string? ParentCode, string? AlternativeOf);

    private sealed record SourceItem(Guid Id, string Code, string Name, string Kind);

    private sealed record ScoreTypeItem(string Code, List<string> TestCodes);

    private sealed record SubjectItem(string Code, List<TopicItem> Topics);

    private sealed record TopicItem(Guid Id, string Code, string Tier);
}
