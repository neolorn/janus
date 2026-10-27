using System;
using System.Globalization;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Privacy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The account endpoints of chapter 09 section 8a: the changes under
/// <c>account:manage</c>, and the reads of another person's account.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, LIB-API-005, IDN-LIFE-003, IDN-LIFE-013, AUTH-SESS-010,
/// PRIV-RIGHT-004, IDN-ATTR-003 and IDN-READ-003. Each is one line to
/// <see cref="IAccounts"/>, the standing to <see cref="IConsents"/>; the permission, the
/// step-up and the state are the service's to judge, so a host calling it in process
/// meets the same refusals.
/// </remarks>
internal static class AccountAdministrationEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    // IDN-ATTR-004: what is stored is JPEG, whatever was uploaded.
    private const string StoredPhoto = "image/jpeg";

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAccountAdministration(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/admin/accounts/{subject}");

        _ = SessionRequired.On(group.MapPost("/suspend", SuspendAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.StepUpRequired,
                    ErrorCodes.AccountNotFound, ErrorCodes.AccountStateConflict)
                .Binding<SubjectId>("subject"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPost("/reactivate", ReactivateAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.StepUpRequired,
                    ErrorCodes.AccountNotFound, ErrorCodes.AccountStateConflict)
                .Binding<SubjectId>("subject"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPost("/restriction/lift", LiftRestrictionAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.StepUpRequired,
                    ErrorCodes.AccountNotFound, ErrorCodes.AccountStateConflict)
                .Binding<SubjectId>("subject"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPost("/delete/cancel", CancelDeletionAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.Restricted, ErrorCodes.StepUpRequired,
                    ErrorCodes.AccountNotFound, ErrorCodes.AccountStateConflict,
                    ErrorCodes.TakedownActive, ErrorCodes.DeletionWindowElapsed)
                .Binding<SubjectId>("subject"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapGet("/photo", ReadPhotoAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Denied, ErrorCodes.PhotoNotFound, ErrorCodes.AccountNotFound)
                .Binding<SubjectId>("subject"))
            .Produces<byte[]>(StatusCodes.Status200OK, StoredPhoto);
        _ = SessionRequired.On(endpoints.MapGet("/admin/accounts/{subject}", ReadAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.OrganizationNotFound,
                    ErrorCodes.AccountNotFound)
                .Binding<SubjectId>("subject"))
            .Produces<HeldAccountView>();
        _ = SessionRequired.On(group.MapGet("/contact", ContactAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.OrganizationNotFound,
                    ErrorCodes.AccountNotFound)
                .Binding<SubjectId>("subject"))
            .Produces<ContactView>();
        _ = SessionRequired.On(group.MapGet("/standing", StandingAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.OrganizationNotFound,
                    ErrorCodes.AccountNotFound)
                .Binding<SubjectId>("subject"))
            .Produces<StandingView>();
        _ = SessionRequired.On(endpoints.MapPost("/admin/accounts/find", FindAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Denied, ErrorCodes.OrganizationNotFound,
                    ErrorCodes.AccountNotFound))
            .Accepts<FindAccountRequest>(MediaTypeNames.Application.Json)
            .Produces<FoundAccountView>();

        return endpoints;
    }

    private static async Task<IResult> SuspendAsync(
        IAccounts accounts,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await accounts
                .SuspendAsync(
                    browser.Asking,
                    browser.Required.Id,
                    subject,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ReactivateAsync(
        IAccounts accounts,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await accounts
                .ReactivateAsync(
                    browser.Asking,
                    browser.Required.Id,
                    subject,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> LiftRestrictionAsync(
        IAccounts accounts,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await accounts
                .LiftRestrictionAsync(
                    browser.Asking,
                    browser.Required.Id,
                    subject,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> CancelDeletionAsync(
        IAccounts accounts,
        RequestSession browser,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        return Answers.Of(
            await accounts
                .CancelDeletionAsync(
                    browser.Asking,
                    browser.Required.Id,
                    subject,
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ReadPhotoAsync(
        IAccounts accounts,
        RequestSession browser,
        HttpContext context,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        // IDN-ATTR-003 AC3: the image is served through the gate, never at a public
        // address, and carries nothing a shared cache could hand to anyone else.
        context.Response.Headers.CacheControl = "no-store";

        return Answers.Of(
            await accounts
                .ReadPhotoAsync(
                    browser.Asking,
                    subject,
                    cancellationToken)
                .ConfigureAwait(false),
            image => TypedResults.Bytes(image, StoredPhoto));
    }

    private static async Task<IResult> ReadAsync(
        IAccounts accounts,
        RequestSession browser,
        SubjectId subject,
        string? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        if (!Asked(organization, out OrganizationId? named))
        {
            return Answers.Malformed(nameof(organization));
        }

        return Answers.Of(
            await accounts
                .ReadAsync(browser.Asking, subject, named, cancellationToken)
                .ConfigureAwait(false),
            account => TypedResults.Json(
                HeldAccountView.Of(account),
                AccountJson.Default.HeldAccountView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> ContactAsync(
        IAccounts accounts,
        RequestSession browser,
        SubjectId subject,
        string? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);

        if (!Asked(organization, out OrganizationId? named))
        {
            return Answers.Malformed(nameof(organization));
        }

        return Answers.Of(
            await accounts
                .ContactAsync(browser.Asking, subject, named, cancellationToken)
                .ConfigureAwait(false),
            contact => TypedResults.Json(
                ContactView.Of(contact),
                AccountJson.Default.ContactView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> StandingAsync(
        IConsents consents,
        RequestSession browser,
        SubjectId subject,
        string? organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consents);
        ArgumentNullException.ThrowIfNull(browser);

        if (!Asked(organization, out OrganizationId? named))
        {
            return Answers.Malformed(nameof(organization));
        }

        return Answers.Of(
            await consents
                .StandingAsync(browser.Asking, subject, named, cancellationToken)
                .ConfigureAwait(false),
            standing => TypedResults.Json(
                StandingView.Of(standing),
                PrivacyJson.Default.StandingView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    // IDN-READ-003: the identifier rides in the body, so no address a proxy logs names it.
    private static async Task<IResult> FindAsync(
        IAccounts accounts,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        FindAccountRequest request = await JsonBody
            .ReadAsync<FindAccountRequest>(context.Request, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return Answers.Malformed("identifier");
        }

        return Answers.Of(
            await accounts
                .FindAsync(
                    browser.Asking,
                    request.Identifier,
                    request.Organization is Guid named ? new OrganizationId(named) : null,
                    cancellationToken)
                .ConfigureAwait(false),
            subject => TypedResults.Json(
                new FoundAccountView(subject.Value),
                AccountJson.Default.FoundAccountView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    // 09 section 8a: the organization a read is asked in may be left out, which asks it
    // in the administrative organization; one given that does not read is malformed.
    private static bool Asked(string? organization, out OrganizationId? named)
    {
        named = null;

        if (organization is null)
        {
            return true;
        }

        if (!OrganizationId.TryParse(organization, CultureInfo.InvariantCulture, out OrganizationId read))
        {
            return false;
        }

        named = read;

        return true;
    }
}
