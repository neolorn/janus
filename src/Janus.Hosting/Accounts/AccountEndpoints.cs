using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The account endpoints of chapter 09 section 6.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-001, REG-ACCT-001, REG-PROF-001,
/// REG-PREF-001, REG-IDENT-002 to REG-IDENT-009 and IDN-ATTR-008. Who is asking is
/// what the session resolution stage established and nothing an endpoint reads from
/// the request; an endpoint that finds nobody there refuses rather than guessing.
/// </remarks>
internal static class AccountEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> NoPreferences =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly IResult Malformed = TypedResults.BadRequest();

    private static readonly IResult Nothing = TypedResults.NoContent();

    private static readonly IResult Accepted = TypedResults.StatusCode(StatusCodes.Status202Accepted);

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapAccount(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/account");

        _ = group.MapGet("/", ReadAsync);
        _ = group.MapPut("/profile", EditProfileAsync);
        _ = group.MapGet("/preferences", ReadPreferencesAsync);
        _ = group.MapPut("/preferences", SetPreferencesAsync);

        _ = group.MapPost("/identifiers", AddIdentifierAsync);
        _ = group.MapPost("/identifiers/{id:guid}/verify", VerifyIdentifierAsync);
        _ = group.MapPost("/identifiers/{id:guid}/primary", MakePrimaryAsync);
        _ = group.MapPut("/identifiers/backup", SetBackupAsync);
        _ = group.MapDelete("/identifiers/{id:guid}", RemoveIdentifierAsync);
        _ = group.MapPost("/identifiers/{id:guid}/undo", UndoIdentifierAsync);
        _ = group.MapPut("/identifiers/{id:guid}/replace", ReplaceIdentifierAsync);
        _ = group.MapPost("/identifiers/{id:guid}/abandon", AbandonIdentifierAsync);

        _ = group.MapGet("/credentials", ListCredentialsAsync);
        _ = group.MapPatch("/credentials/{id:guid}", LabelCredentialAsync);
        _ = group.MapPut("/secondstep/preferred", PreferSecondStepAsync);

        _ = group.MapGet("/sessions", ListSessionsAsync);
        _ = group.MapDelete("/sessions/{id:guid}", EndSessionAsync);

        _ = group.MapPost("/deactivate", DeactivateAsync);
        _ = group.MapPost("/reactivate", ReactivateAsync);
        _ = group.MapPost("/delete", DeleteAsync);
        _ = group.MapPost("/delete/cancel", CancelDeletionAsync);

        return endpoints;
    }

    private static async Task<IResult> ReadAsync(
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await accounts.ReadAsync(holder, cancellationToken).ConfigureAwait(false),
                account => TypedResults.Json(
                    AccountView.Of(account),
                    AccountJson.Default.AccountView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    private static async Task<IResult> EditProfileAsync(
        ProfileEditRequest request,
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(accounts);

        if (Asking(browser) is not AccessContext holder || browser.Live is null)
        {
            return Nobody();
        }

        var edit = new ProfileEdit(
            request.DisplayName,
            request.LegalName,
            request.Username,
            request.DateOfBirth);

        return Answers.Of(
            await accounts
                .EditProfileAsync(holder, browser.Live.Id, edit, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ReadPreferencesAsync(
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await accounts.ReadPreferencesAsync(holder, cancellationToken).ConfigureAwait(false),
                preferences => TypedResults.Json(
                    PreferencesView.Of(preferences),
                    AccountJson.Default.PreferencesView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    private static async Task<IResult> SetPreferencesAsync(
        PreferencesRequest request,
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(accounts);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await accounts
                    .SetPreferencesAsync(
                        holder,
                        request.Language,
                        request.TimeZone,
                        request.Declared ?? NoPreferences,
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // API-CONV-005: accepted whether or not the identifier belongs to another
    // account, and the answer is the same either way.
    private static async Task<IResult> AddIdentifierAsync(
        AddIdentifierToAccountRequest request,
        IIdentifiers identifiers,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(context);

        if (Asking(browser) is not AccessContext holder || browser.Live is null)
        {
            return Nobody();
        }

        return request.Value is not { Length: > 0 } value
            ? Malformed
            : Answers.Of(
                await identifiers
                    .AddAsync(
                        holder,
                        browser.Live.Id,
                        request.Kind,
                        value,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Accepted);
    }

    private static async Task<IResult> VerifyIdentifierAsync(
        Guid id,
        VerifyIdentifierRequest request,
        IIdentifiers identifiers,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(context);

        string source = RequestOrigin.Source(context.Request);

        // API-LAND-001: a link merely opened, or opened somewhere else, changes
        // nothing and is answered with the code to type instead.
        if (request.LinkToken is { Length: > 0 } token)
        {
            return Answers.Of(
                await identifiers
                    .LandAsync(browser.Live?.Id, token, request.Press, source, cancellationToken)
                    .ConfigureAwait(false),
                Landed);
        }

        if (request.Code is not { Length: > 0 } code)
        {
            return Malformed;
        }

        if (Asking(browser) is not AccessContext holder)
        {
            return Opened(browser) is not EnrolmentSessionId enrolment
                ? Nobody()
                : Answers.Of(
                    await identifiers
                        .VerifyAsync(enrolment, new IdentifierId(id), code, source, cancellationToken)
                        .ConfigureAwait(false),
                    Nothing);
        }

        return Answers.Of(
            await identifiers
                .VerifyAsync(holder, new IdentifierId(id), code, source, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> MakePrimaryAsync(
        Guid id,
        IIdentifiers identifiers,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(context);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await identifiers
                    .MakePrimaryAsync(
                        holder,
                        new IdentifierId(id),
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> SetBackupAsync(
        BackupRequest request,
        IIdentifiers identifiers,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(context);

        if (Asking(browser) is not AccessContext holder)
        {
            return Nobody();
        }

        if (!Chosen(request.Setting, out BackupChoice choice, out IdentifierId? named))
        {
            return Malformed;
        }

        return Answers.Of(
            await identifiers
                .SetBackupAsync(
                    holder,
                    request.Kind,
                    choice,
                    named,
                    RequestOrigin.Source(context.Request),
                    cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> DeactivateAsync(
        IAccount account,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(context);

        return Asking(browser) is not AccessContext holder || browser.Live is null
            ? Nobody()
            : Answers.Of(
                await account
                    .DeactivateAsync(
                        holder,
                        browser.Live.Id,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Accepted);
    }

    // IDN-LIFE-013: link-borne, because a suspended account cannot sign in and so has
    // no session that could reach this on its own.
    private static async Task<IResult> ReactivateAsync(
        LinkTokenRequest request,
        IAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(account);

        return request.LinkToken is not { Length: > 0 } token
            ? Malformed
            : Answers.Of(
                await account.ReactivateAsync(token, cancellationToken).ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> DeleteAsync(
        IAccount account,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(context);

        return Asking(browser) is not AccessContext holder || browser.Live is null
            ? Nobody()
            : Answers.Of(
                await account
                    .DeleteAsync(
                        holder,
                        browser.Live.Id,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                erasesAt => TypedResults.Accepted(
                    (string?)null,
                    new DeletionView(erasesAt)));
    }

    // IDN-LIFE-014: the deletion notice carries the link, because a deleting account
    // cannot sign in either.
    private static async Task<IResult> CancelDeletionAsync(
        LinkTokenRequest request,
        IAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(account);

        return request.LinkToken is not { Length: > 0 } token
            ? Malformed
            : Answers.Of(
                await account.CancelDeletionAsync(token, cancellationToken).ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> RemoveIdentifierAsync(
        Guid id,
        IIdentifiers identifiers,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(context);

        return Asking(browser) is not AccessContext holder || browser.Live is null
            ? Nobody()
            : Answers.Of(
                await identifiers
                    .RemoveAsync(
                        holder,
                        browser.Live.Id,
                        new IdentifierId(id),
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // REG-IDENT-006: link-borne, because after a hostile removal the account has no
    // session that could reach this on its own.
    private static async Task<IResult> UndoIdentifierAsync(
        Guid id,
        LinkTokenRequest request,
        IIdentifiers identifiers,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(context);

        return request.LinkToken is not { Length: > 0 } token
            ? Malformed
            : Answers.Of(
                await identifiers
                    .UndoAsync(token, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> ReplaceIdentifierAsync(
        Guid id,
        ReplaceIdentifierRequest request,
        IIdentifiers identifiers,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(context);

        if (request.Value is not { Length: > 0 } value)
        {
            return Malformed;
        }

        string source = RequestOrigin.Source(context.Request);

        // AUTH-RECOV-002: the enrolment session an approver opened for a lost mailbox
        // reaches this endpoint, where the new address confirms alone (REG-IDENT-007).
        if (Asking(browser) is not AccessContext holder || browser.Live is null)
        {
            return Opened(browser) is not EnrolmentSessionId enrolment
                ? Nobody()
                : Answers.Of(
                    await identifiers
                        .ReplaceAsync(
                            enrolment,
                            new IdentifierId(id),
                            value,
                            source,
                            cancellationToken)
                        .ConfigureAwait(false),
                    Accepted);
        }

        return Answers.Of(
            await identifiers
                .ReplaceAsync(
                    holder,
                    browser.Live.Id,
                    new IdentifierId(id),
                    value,
                    source,
                    cancellationToken)
                .ConfigureAwait(false),
            Accepted);
    }

    // REG-SESS-003: the ending control of a link opened in another browser, which
    // needs no session and answers the same way whatever the token resolves to.
    private static async Task<IResult> AbandonIdentifierAsync(
        Guid id,
        LinkTokenRequest request,
        IIdentifiers identifiers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);

        return request.LinkToken is not { Length: > 0 } token
            ? Malformed
            : Answers.Of(
                await identifiers.AbandonAsync(token, cancellationToken).ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> ListCredentialsAsync(
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await accounts.ListCredentialsAsync(holder, cancellationToken).ConfigureAwait(false),
                credentials => TypedResults.Json<IReadOnlyList<CredentialView>>(
                    [.. credentials.Select(CredentialView.Of)],
                    AccountJson.Default.IReadOnlyListCredentialView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    private static async Task<IResult> LabelCredentialAsync(
        Guid id,
        LabelRequest request,
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(accounts);

        if (Asking(browser) is not AccessContext holder)
        {
            return Nobody();
        }

        return request.Label is not { } label
            ? Malformed
            : Answers.Of(
                await accounts
                    .LabelCredentialAsync(holder, new AuthenticatorId(id), label, cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> PreferSecondStepAsync(
        PreferredSecondStepRequest request,
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(accounts);

        if (Asking(browser) is not AccessContext holder)
        {
            return Nobody();
        }

        return request.Credential is not Guid credential
            ? Malformed
            : Answers.Of(
                await accounts
                    .PreferSecondStepAsync(holder, new AuthenticatorId(credential), cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> ListSessionsAsync(
        ISessions sessions,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        return Asking(browser) is not AccessContext holder || browser.Live is null
            ? Nobody()
            : Answers.Of(
                await sessions
                    .ListAsync(holder, browser.Live.Id, cancellationToken)
                    .ConfigureAwait(false),
                held => TypedResults.Json<IReadOnlyList<SessionView>>(
                    [.. held.Select(SessionView.Of)],
                    AccountJson.Default.IReadOnlyListSessionView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    private static async Task<IResult> EndSessionAsync(
        Guid id,
        ISessions sessions,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        return Asking(browser) is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await sessions
                    .EndAsync(holder, new SessionId(id), cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // REG-IDENT-002: the setting is one of the two rules or the identifier of one
    // verified address of the kind.
    private static bool Chosen(string? setting, out BackupChoice choice, out IdentifierId? named)
    {
        named = null;

        switch (setting)
        {
            case "all-verified":
                choice = BackupChoice.AllVerified;

                return true;

            case "primary-only":
                choice = BackupChoice.PrimaryOnly;

                return true;

            default:
                choice = BackupChoice.Named;

                if (Guid.TryParse(setting, out Guid identifier))
                {
                    named = new IdentifierId(identifier);

                    return true;
                }

                return false;
        }
    }

    private static AccessContext? Asking(RequestSession browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return browser.Context;
    }

    // D-148: the enrolment session the browser's first contact carries, which reaches
    // the two operations chapter 09 section 3 names and nothing else here.
    private static EnrolmentSessionId? Opened(RequestSession browser) =>
        browser.FirstContact?.Enrolment;

    private static IResult Landed(LinkLanding landing)
    {
        ArgumentNullException.ThrowIfNull(landing);

        if (landing.Verified)
        {
            return Nothing;
        }

        return TypedResults.Json(
            IdentifierLandingView.Of(landing),
            AccountJson.Default.IdentifierLandingView,
            contentType: null,
            StatusCodes.Status200OK);
    }

    // API-CONV-003: nobody is asking, which is what 401 is for and what nothing else
    // is for.
    private static IResult Nobody() => Answers.Refused(ErrorCodes.SessionExpired);

}
