using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        (await db.TopicWeights.CountAsync(ct)).ShouldBe(370);
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
