using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nirengi.BuildingBlocks.Application.Security;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Publishing changes the curriculum, so these run on their own database.</summary>
[CollectionDefinition(Name)]
public sealed class AdminCurriculumGroup : ICollectionFixture<NirengiApiFactory>
{
    public const string Name = "admin-curriculum";
}

[Trait("Category", "Integration")]
[Collection(AdminCurriculumGroup.Name)]
public sealed class AdminCurriculumTests(NirengiApiFactory factory)
{
    private static readonly string[] NewSubtopics = ["Alt"];
    private static readonly string[] UpdatedSubtopics = ["basamak değeri"];

    private readonly AuthApi _api = new(factory);

    [Fact]
    public async Task Students_cannot_reach_admin_endpoints()
    {
        var student = await _api.RegisterAndVerify(AuthApi.NewEmail());

        (await _api.Send(HttpMethod.Get, "/v1/admin/curriculum", student.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.Send(HttpMethod.Get, "/v1/admin/rule-sets", student.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _api.Client.GetAsync("/v1/admin/curriculum", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // One ordered scenario: the draft, its publish and the rollback build on each other.
    [Fact]
    public async Task Draft_is_published_as_a_new_version_and_rolled_back()
    {
        var admin = AdminToken();
        var student = await _api.RegisterAndVerify(AuthApi.NewEmail());
        var before = await AuthApi.Read<AdminCurriculum>(await _api.Send(HttpMethod.Get, "/v1/admin/curriculum", admin));
        before.VersionNumber.ShouldBeGreaterThanOrEqualTo(1);
        var studentEtag = (await _api.Send(HttpMethod.Get, "/v1/curriculum", student.AccessToken)).Headers.ETag!.ToString();

        var draft = await AuthApi.Read<Draft>(await _api.Send(HttpMethod.Post, "/v1/admin/curriculum/drafts", admin, new { }));
        (await AuthApi.Read<Draft>(await _api.Send(HttpMethod.Post, "/v1/admin/curriculum/drafts", admin, new { }))).Id.ShouldBe(draft.Id);

        var saved = await _api.Send(HttpMethod.Put, $"/v1/admin/curriculum/drafts/{draft.Id}", admin, new
        {
            revision = draft.Revision,
            title = "Deneme",
            operations = new object[]
            {
                new { type = "topic.add", subjectCode = "TYT.TMAT", name = "Yeni konu", subtopics = NewSubtopics, gradeMin = 9, gradeMax = 10 },
                new { code = "TYT.TMAT.02", name = "Basamak Kavramı", subtopics = UpdatedSubtopics, gradeMin = 9, gradeMax = 9, type = "topic.update" },
                new { type = "source.add", code = "TEST_YAYIN", name = "Test Yayın", kind = "Publisher" },
            },
        });
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var edited = await AuthApi.Read<Draft>(saved);
        edited.Validation.IsValid.ShouldBeTrue(edited.Validation.Problem);
        var newCode = edited.Operations[0].Code.ShouldNotBeNull();
        newCode.ShouldStartWith("TYT.TMAT.");

        await AuthApi.ShouldBeProblem(
            await _api.Send(HttpMethod.Put, $"/v1/admin/curriculum/drafts/{draft.Id}", admin, new { revision = draft.Revision, title = "x", operations = Array.Empty<object>() }),
            HttpStatusCode.Conflict,
            "curriculum.draft_changed");

        var published = await _api.Send(HttpMethod.Post, $"/v1/admin/curriculum/drafts/{draft.Id}/publish", admin, new { revision = edited.Revision, summary = "Yeni TMAT konusu" });
        published.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await AuthApi.Read<VersionSummary>(published)).Number.ShouldBe(before.VersionNumber + 1);

        // Students revalidate with their ETag and get the new topic.
        var refreshed = await _api.Send(HttpMethod.Get, "/v1/curriculum", student.AccessToken, ifNoneMatch: studentEtag);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await refreshed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain(newCode);

        var after = await AuthApi.Read<AdminCurriculum>(await _api.Send(HttpMethod.Get, "/v1/admin/curriculum", admin));
        after.ContentHash.ShouldNotBe(before.ContentHash);
        after.Drafts.ShouldBeEmpty();
        Topic(after, newCode).IsActive.ShouldBeTrue();
        Topic(after, newCode).Weights.ShouldBeEmpty(); // q = 0 until the seed knows it

        var history = await AuthApi.Read<List<VersionSummary>>(await _api.Send(HttpMethod.Get, "/v1/admin/curriculum/versions", admin));
        history[0].Number.ShouldBe(after.VersionNumber);
        history[0].OperationCount.ShouldBe(3);

        // Going back to the previous version retires what was added; nothing is deleted.
        var rollback = await AuthApi.Read<Draft>(await _api.Send(HttpMethod.Post, "/v1/admin/curriculum/drafts", admin, new { fromVersion = before.VersionNumber }));
        rollback.Operations.Select(o => o.Type).ShouldBe(["topic.update", "topic.retire", "source.retire"], ignoreOrder: true);
        (await _api.Send(HttpMethod.Post, $"/v1/admin/curriculum/drafts/{rollback.Id}/publish", admin, new { revision = rollback.Revision, summary = "Geri dönüş" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var restored = await AuthApi.Read<AdminCurriculum>(await _api.Send(HttpMethod.Get, "/v1/admin/curriculum", admin));
        restored.VersionNumber.ShouldBe(before.VersionNumber + 2);
        Topic(restored, newCode).IsActive.ShouldBeFalse();
        Topic(restored, "TYT.TMAT.02").Name.ShouldBe(Topic(before, "TYT.TMAT.02").Name);
        restored.Sources.Single(s => s.Code == "TEST_YAYIN").IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Invalid_draft_is_saved_but_cannot_be_published()
    {
        var admin = AdminToken();
        var draft = await AuthApi.Read<Draft>(await _api.Send(HttpMethod.Post, "/v1/admin/curriculum/drafts", admin, new { }));
        var edited = await AuthApi.Read<Draft>(await _api.Send(HttpMethod.Put, $"/v1/admin/curriculum/drafts/{draft.Id}", admin, new
        {
            revision = draft.Revision,
            title = "Hatalı",
            operations = new object[] { new { type = "topic.retire", code = "TYT.YOK.01" } },
        }));
        edited.Validation.IsValid.ShouldBeFalse();
        edited.Validation.Problem.ShouldNotBeNull().ShouldContain("TYT.YOK.01");

        await AuthApi.ShouldBeProblem(
            await _api.Send(HttpMethod.Post, $"/v1/admin/curriculum/drafts/{draft.Id}/publish", admin, new { revision = edited.Revision, summary = "olmaz" }),
            HttpStatusCode.BadRequest,
            "curriculum.invalid_draft");

        (await _api.Send(HttpMethod.Delete, $"/v1/admin/curriculum/drafts/{draft.Id}", admin)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Seed_export_and_rule_sets_are_readable()
    {
        var admin = AdminToken();

        var export = await _api.Send(HttpMethod.Get, "/v1/admin/curriculum/export", admin);
        export.StatusCode.ShouldBe(HttpStatusCode.OK);
        export.Content.Headers.ContentType!.MediaType.ShouldBe("application/zip");

        var rules = await AuthApi.Read<List<RuleSet>>(await _api.Send(HttpMethod.Get, "/v1/admin/rule-sets", admin));
        rules.Select(r => r.Module).ShouldBe(["Analytics", "Recommendation"]);
        rules.ShouldAllBe(r => r.Parameters.Count > 5);
    }

    private static AdminTopic Topic(AdminCurriculum curriculum, string code) =>
        curriculum.Subjects.SelectMany(s => s.Topics).Single(t => t.Code == code);

    /// <summary>An access token as Identity issues it after TOTP (the MFA flow itself is covered by AdminMfaTests).</summary>
    private string AdminToken()
    {
        var jwt = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(10),
            Claims = new Dictionary<string, object>
            {
                [NirengiClaims.Subject] = Guid.NewGuid().ToString(),
                [NirengiClaims.Role] = NirengiRoles.Admin,
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(jwt.SigningKeyBytes()), SecurityAlgorithms.HmacSha256),
        });
    }

    private sealed record AdminCurriculum(int VersionNumber, string ContentHash, List<AdminSubject> Subjects, List<AdminSource> Sources, List<DraftSummary> Drafts);

    private sealed record AdminSubject(string Code, List<AdminTopic> Topics);

    private sealed record AdminTopic(string Code, string Name, bool IsActive, List<object> Weights);

    private sealed record AdminSource(string Code, bool IsActive);

    private sealed record DraftSummary(Guid Id);

    private sealed record Draft(Guid Id, int Revision, List<Operation> Operations, Validation Validation);

    private sealed record Operation(string Type, string? Code);

    private sealed record Validation(bool IsValid, string? Problem);

    private sealed record VersionSummary(int Number, int OperationCount);

    private sealed record RuleSet(string Module, string Version, List<object> Parameters);
}
