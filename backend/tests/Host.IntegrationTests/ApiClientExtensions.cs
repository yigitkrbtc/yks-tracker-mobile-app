using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Nirengi.Host.IntegrationTests;

internal static class ApiClientExtensions
{
    public static Task<HttpResponseMessage> Send(this AuthApi api, HttpMethod method, string path, string accessToken, object? body = null, string? ifNoneMatch = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: AuthApi.Json);
        }

        if (ifNoneMatch is not null)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(ifNoneMatch));
        }

        return api.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
