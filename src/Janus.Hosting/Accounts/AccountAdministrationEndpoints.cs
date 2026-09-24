using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The account endpoints of chapter 09 section 8a, under <c>account:manage</c>.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, LIB-API-005, IDN-LIFE-003, IDN-LIFE-013, AUTH-SESS-010
/// and PRIV-RIGHT-004. Each is one line to <see cref="IAccounts"/>; the permission, the
/// step-up and the state are the service's to judge, so a host calling it in process
/// meets the same refusals.
/// </remarks>
internal static class AccountAdministrationEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAccountAdministration(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/admin/accounts/{subject:guid}");

        _ = SessionRequired.On(group.MapPost("/suspend", SuspendAsync));
        _ = SessionRequired.On(group.MapPost("/reactivate", ReactivateAsync));
        _ = SessionRequired.On(group.MapPost("/restriction/lift", LiftRestrictionAsync));
        _ = SessionRequired.On(group.MapPost("/delete/cancel", CancelDeletionAsync));

        return endpoints;
    }

    private static async Task<IResult> SuspendAsync(
        IAccounts accounts,
        RequestSession browser,
        Guid subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await accounts
                .SuspendAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new SubjectId(subject),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ReactivateAsync(
        IAccounts accounts,
        RequestSession browser,
        Guid subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await accounts
                .ReactivateAsync(
                    AccessContext.Of(browser.Required.Subject),
                    browser.Required.Id,
                    new SubjectId(subject),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> LiftRestrictionAsync(
        IAccounts accounts,
        RequestSession browser,
        Guid subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await accounts
                .LiftRestrictionAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new SubjectId(subject),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> CancelDeletionAsync(
        IAccounts accounts,
        RequestSession browser,
        Guid subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await accounts
                .CancelDeletionAsync(
                    AccessContext.Of(browser.Required.Subject),
                    new SubjectId(subject),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }
}
