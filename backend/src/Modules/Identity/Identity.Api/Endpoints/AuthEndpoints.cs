using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Api.Security;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Identity.Application.Features.Registration;
using Nirengi.Modules.Identity.Application.Features.Sessions;
using Nirengi.Modules.Identity.Application.Features.SignIn;
using Nirengi.Modules.Identity.Application.SignIn;

namespace Nirengi.Modules.Identity.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder v1)
    {
        var auth = v1.MapGroup("/auth").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        auth.MapPost("/register", async (RegisterCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(_ => Results.Accepted()))
            .WithName("Register")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        auth.MapPost("/email/verify", async (VerifyEmailCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(Results.Ok))
            .WithName("VerifyEmail")
            .Produces<SignInResult>()
            .ProducesValidationProblem();

        auth.MapPost("/email/resend", async (ResendVerificationCodeCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(_ => Results.Accepted()))
            .WithName("ResendVerificationCode")
            .Produces(StatusCodes.Status202Accepted);

        auth.MapPost("/login", async (LoginCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(Results.Ok))
            .WithName("Login")
            .Produces<SignInResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var session = v1.MapGroup("/auth").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Session);

        session.MapPost("/refresh", async (RefreshSessionCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(Results.Ok))
            .WithName("RefreshSession")
            .Produces<AuthTokens>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        session.MapPost("/logout", async (LogoutCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(_ => Results.NoContent()))
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);
    }
}
