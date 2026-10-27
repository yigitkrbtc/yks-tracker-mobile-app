using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Api.Security;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Identity.Application.Features.Mfa;
using Nirengi.Modules.Identity.Application.SignIn;

namespace Nirengi.Modules.Identity.Api.Endpoints;

/// <summary>Admin second factor. The MFA token from login goes in the body, never in the Authorization header.</summary>
internal static class MfaEndpoints
{
    public static void MapMfaEndpoints(this IEndpointRouteBuilder v1)
    {
        var mfa = v1.MapGroup("/auth/mfa/totp").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        mfa.MapPost("/", async (CompleteTotpSignInCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(Results.Ok))
            .WithName("CompleteTotpSignIn")
            .Produces<AuthTokens>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        mfa.MapPost("/setup", async (BeginTotpSetupCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(Results.Ok))
            .WithName("BeginTotpSetup")
            .Produces<TotpSetup>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        mfa.MapPost("/setup/confirm", async (ConfirmTotpSetupCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(Results.Ok))
            .WithName("ConfirmTotpSetup")
            .Produces<TotpEnrollment>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
