using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;

namespace Nirengi.Host.IntegrationTests;

/// <summary>Thin client over the auth endpoints, shaped like the mobile app uses them.</summary>
public sealed class AuthApi(NirengiApiFactory factory)
{
    public const string Password = "dogruSifre123";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public HttpClient Client { get; } = factory.CreateClient();

    public static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    public static object Registration(string email, int birthYear = 2000, bool marketing = false, string terms = "1") => new
    {
        firstName = "Ada",
        lastName = "Yılmaz",
        email,
        password = Password,
        birthYear,
        termsVersion = terms,
        privacyNoticeVersion = "1",
        marketingConsent = marketing,
    };

    public Task<HttpResponseMessage> Post(string path, object body) =>
        Client.PostAsJsonAsync(path, body, Json, TestContext.Current.CancellationToken);

    public async Task<Tokens> RegisterAndVerify(string email)
    {
        (await Post("/v1/auth/register", Registration(email))).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var verified = await Post("/v1/auth/email/verify", new { email, code = factory.Mail.LatestCode(email) });
        verified.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await Read<SignIn>(verified)).Tokens!;
    }

    public async Task<HttpResponseMessage> GetMe(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<T> Read<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json, TestContext.Current.CancellationToken))!;

    public static async Task ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status, string type)
    {
        response.StatusCode.ShouldBe(status);
        (await Read<Problem>(response)).Type.ShouldBe(type);
    }

    public sealed record Tokens(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    public sealed record Mfa(string Step, string MfaToken, DateTimeOffset ExpiresAt);

    public sealed record SignIn(Tokens? Tokens, Mfa? Mfa);

    public sealed record Problem(string? Type, string? Title);

    public sealed record Profile(Guid Id, string FirstName, string LastName, string Email, int BirthYear, string Role, bool MarketingConsent, bool CanGiveMarketingConsent);
}
