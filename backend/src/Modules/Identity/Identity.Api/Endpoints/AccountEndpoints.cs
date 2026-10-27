using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nirengi.BuildingBlocks.Api.Results;
using Nirengi.BuildingBlocks.Api.Security;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.Modules.Identity.Application.Features.Deletion;
using Nirengi.Modules.Identity.Application.Features.Passwords;
using Nirengi.Modules.Identity.Application.SignIn;

namespace Nirengi.Modules.Identity.Api.Endpoints;

internal static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder v1)
    {
        var auth = v1.MapGroup("/auth").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        auth.MapPost("/password/forgot", async (ForgotPasswordCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(_ => Results.Accepted()))
            .WithName("ForgotPassword")
            .Produces(StatusCodes.Status202Accepted);

        auth.MapPost("/password/reset", async (ResetPasswordCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(_ => Results.NoContent()))
            .WithName("ResetPassword")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        auth.MapPost("/account/restore", async (RestoreAccountCommand command, IDispatcher dispatcher, CancellationToken ct) =>
                (await dispatcher.Send(command, ct)).Match(Results.Ok))
            .WithName("RestoreAccount")
            .Produces<SignInResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
