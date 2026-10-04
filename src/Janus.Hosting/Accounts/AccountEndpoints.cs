using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Accounts;

/// <summary>
/// The account endpoints of chapter 09 sections 6 and 6a.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-001, REG-ACCT-001, REG-PROF-001,
/// REG-PREF-001, REG-IDENT-002 to REG-IDENT-009, IDN-ATTR-008, REG-INV-001 and
/// REG-INV-002. Who is asking is what the session resolution stage established and
/// nothing an endpoint reads from the request; an endpoint that finds nobody there
/// refuses rather than guessing.
/// </remarks>
internal static class AccountEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> NoPreferences =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static readonly IResult Nothing = TypedResults.NoContent();

    private static readonly IResult Accepted = TypedResults.StatusCode(StatusCodes.Status202Accepted);

    // IDN-ATTR-004: what is stored is JPEG, whatever was uploaded.
    private const string StoredPhoto = "image/jpeg";

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

        _ = SessionRequired.On(group.MapGet("/", ReadAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces<AccountView>();
        _ = SessionRequired.On(group.MapPut("/profile", EditProfileAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.StepUpRequired, ErrorCodes.Restricted, ErrorCodes.Denied,
                    ErrorCodes.UsernameTaken, ErrorCodes.UsernameReserved,
                    ErrorCodes.UsernameCoolingOff, ErrorCodes.IdentifierMixedScript,
                    ErrorCodes.ProfileInvalid, ErrorCodes.ProfileNotAccepted))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapGet("/photo", ReadPhotoAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.PhotoNotFound))
            .Produces<byte[]>(StatusCodes.Status200OK, StoredPhoto);
        _ = SessionRequired.On(group.MapPut("/photo", SetPhotoAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Restricted, ErrorCodes.PhotoNotEnabled, ErrorCodes.PhotoInvalid,
                    ErrorCodes.PhotoTooLarge))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapDelete("/photo", RemovePhotoAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Restricted))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapGet("/preferences", ReadPreferencesAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces<PreferencesView>();
        _ = SessionRequired.On(group.MapPut("/preferences", SetPreferencesAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Restricted, ErrorCodes.PreferenceUndeclared,
                    ErrorCodes.PreferenceWrongType, ErrorCodes.PreferenceTooLarge,
                    ErrorCodes.PreferenceAdministratorOnly))
            .Produces(StatusCodes.Status204NoContent);

        _ = SessionRequired.On(group.MapPost("/identifiers", AddIdentifierAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.StepUpRequired, ErrorCodes.Restricted, ErrorCodes.Denied,
                    ErrorCodes.IdentifierMaximum, ErrorCodes.IdentifierMixedScript,
                    ErrorCodes.IdentifierDomainNotAllowed, ErrorCodes.IdentifierInvalid,
                    ErrorCodes.SmsBalanceFloor, ErrorCodes.RestrictionExceeded))
            .Produces(StatusCodes.Status202Accepted);
        _ = EnrolmentRoute.On(group.MapPost("/identifiers/{id}/verify", VerifyIdentifierAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.Restricted, ErrorCodes.IdentifierMaximum,
                    ErrorCodes.CodeInvalid, ErrorCodes.CodeExpired, ErrorCodes.Throttled)
                .Binding<IdentifierId>("id"))
            .Produces<IdentifierLandingView>()
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPost("/identifiers/{id}/primary", MakePrimaryAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Restricted, ErrorCodes.IdentifierUnverified)
                .Binding<IdentifierId>("id"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPut("/identifiers/backup", SetBackupAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Restricted, ErrorCodes.IdentifierUnverified))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapDelete("/identifiers/{id}", RemoveIdentifierAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.StepUpRequired, ErrorCodes.Restricted, ErrorCodes.IdentifierPrimary,
                    ErrorCodes.IdentifierLastOfKind)
                .Binding<IdentifierId>("id"))
            .Produces(StatusCodes.Status204NoContent);
        _ = group.MapPost("/identifiers/{id}/undo", UndoIdentifierAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.IdentifierMaximum, ErrorCodes.ChangeWindowElapsed)
                .Binding<IdentifierId>("id"))
            .Produces(StatusCodes.Status204NoContent);
        _ = EnrolmentRoute.On(group.MapPut("/identifiers/{id}/replace", ReplaceIdentifierAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.StepUpRequired, ErrorCodes.Restricted,
                    ErrorCodes.Denied, ErrorCodes.ChangePending, ErrorCodes.IdentifierMixedScript,
                    ErrorCodes.IdentifierDomainNotAllowed, ErrorCodes.SmsBalanceFloor,
                    ErrorCodes.RestrictionExceeded)
                .Binding<IdentifierId>("id"))
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPost("/identifiers/{id}/abandon", AbandonIdentifierAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Restricted).Binding<IdentifierId>("id"))
            .Produces(StatusCodes.Status204NoContent);

        _ = SessionRequired.On(group.MapGet("/credentials", ListCredentialsAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces<IReadOnlyList<CredentialView>>();
        _ = SessionRequired.On(group.MapPatch("/credentials/{id}", LabelCredentialAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.Restricted, ErrorCodes.CredentialNotFound,
                    ErrorCodes.CredentialLabelInvalid)
                .Binding<AuthenticatorId>("id"))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPut("/secondstep/preferred", PreferSecondStepAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.Restricted, ErrorCodes.RequestInvalid))
            .Produces(StatusCodes.Status204NoContent);

        _ = SessionRequired.On(group.MapGet("/sessions", ListSessionsAsync))
            .Declares(EndpointDeclaration.Answering())
            .Produces<IReadOnlyList<SessionView>>();
        _ = SessionRequired.On(group.MapDelete("/sessions/{id}", EndSessionAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.ResourceNotFound).Binding<SessionId>("id"))
            .Produces(StatusCodes.Status204NoContent);

        _ = SessionRequired.On(group.MapPost("/deactivate", DeactivateAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.StepUpRequired, ErrorCodes.Restricted, ErrorCodes.Denied))
            .Produces(StatusCodes.Status202Accepted);
        _ = group.MapPost("/reactivate", ReactivateAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.AccountAdministrativelySuspended,
                    ErrorCodes.ReactivationTokenInvalid))
            .Produces(StatusCodes.Status204NoContent);
        _ = SessionRequired.On(group.MapPost("/delete", DeleteAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.StepUpRequired, ErrorCodes.Restricted, ErrorCodes.Denied))
            .Produces<DeletionView>(StatusCodes.Status202Accepted);
        _ = group.MapPost("/delete/cancel", CancelDeletionAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.TakedownActive, ErrorCodes.DeletionWindowElapsed))
            .Produces(StatusCodes.Status204NoContent);

        _ = SessionRequired.On(group.MapGet("/invitation", ReadInvitationAsync))
            .Declares(EndpointDeclaration.Answering(ErrorCodes.InvitationNotFound))
            .Produces<AttachedInvitationView>();
        _ = SessionRequired.On(group.MapPost("/invitation/acknowledge", AcknowledgeInvitationAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.RequestMalformed, ErrorCodes.Restricted, ErrorCodes.StepUpRequired,
                    ErrorCodes.InvitationNotFound, ErrorCodes.MembershipLimitReached,
                    ErrorCodes.IdentifierMaximum, ErrorCodes.InvitationExpired,
                    ErrorCodes.InvitationIdentifierMismatch, ErrorCodes.IdentifierDomainNotAllowed))
            .Produces(StatusCodes.Status204NoContent);

        return endpoints;
    }

    private static async Task<IResult> ReadAsync(
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        AccessContext holder = Asking(browser);

        return Answers.Of(
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

        AccessContext holder = Asking(browser);

        var edit = new ProfileEdit(
            request.DisplayName,
            request.LegalName,
            request.Username,
            request.DateOfBirth);

        return Answers.Of(
            await accounts
                .EditProfileAsync(holder, browser.Required.Id, edit, cancellationToken)
                .ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ReadPhotoAsync(
        IAccount accounts,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(context);

        AccessContext holder = Asking(browser);

        // IDN-ATTR-003 AC3: the image is served through the gate the session is, and
        // carries nothing a shared cache could hand to anyone else.
        context.Response.Headers.CacheControl = "no-store";

        return Answers.Of(
            await accounts.ReadPhotoAsync(holder, cancellationToken).ConfigureAwait(false),
            image => TypedResults.Bytes(image, StoredPhoto));
    }

    private static async Task<IResult> SetPhotoAsync(
        IAccount accounts,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(context);

        AccessContext holder = Asking(browser);

        ReadOnlyMemory<byte> upload = await UploadedImage
            .ReadAsync(context.Request, cancellationToken)
            .ConfigureAwait(false);

        return Answers.Of(
            await accounts.SetPhotoAsync(holder, upload, cancellationToken).ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> RemovePhotoAsync(
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await accounts.RemovePhotoAsync(holder, cancellationToken).ConfigureAwait(false),
            Nothing);
    }

    private static async Task<IResult> ReadInvitationAsync(
        IInvitations invitations,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitations);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await invitations.AttachedAsync(holder, cancellationToken).ConfigureAwait(false),
            attached => TypedResults.Json(
                AttachedInvitationView.Of(attached),
                AccountJson.Default.AttachedInvitationView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    // The invitation is named because what is acknowledged is what the membership
    // step showed, and never one attached after it was read.
    private static async Task<IResult> AcknowledgeInvitationAsync(
        AcknowledgeInvitationRequest request,
        IInvitations invitations,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(invitations);
        ArgumentNullException.ThrowIfNull(context);

        AccessContext holder = Asking(browser);

        return request.InvitationId is not Guid invitation
            ? Answers.Malformed("invitationId")
            : Answers.Of(
                await invitations
                    .AcknowledgeAsync(
                        holder,
                        new InvitationId(invitation),
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> ReadPreferencesAsync(
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        AccessContext holder = Asking(browser);

        return Answers.Of(
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

        AccessContext holder = Asking(browser);

        return Answers.Of(
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

        AccessContext holder = Asking(browser);

        return request.Value is not { Length: > 0 } value
            ? Answers.Malformed("value")
            : Answers.Of(
                await identifiers
                    .AddAsync(
                        holder,
                        browser.Required.Id,
                        request.Kind,
                        value,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Accepted);
    }

    private static async Task<IResult> VerifyIdentifierAsync(
        IdentifierId id,
        VerifyIdentifierRequest request,
        IIdentifiers identifiers,
        SessionService sessions,
        BrowserSessionCookies cookies,
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
        // nothing and is answered with the code to type instead. IDN-LIFE-008: only a
        // press from the browser that staged the change completes it under a session,
        // so only that session is kept and rotated; the displaced address's press keeps
        // none.
        if (request.LinkToken is { Length: > 0 } token)
        {
            Result<LinkLanding> landed = await identifiers
                .LandAsync(browser.Live?.Id, token, request.Press, source, cancellationToken)
                .ConfigureAwait(false);

            return browser.Live is Session pressing
                && landed.Match(landing => landing.Verified && landing.SameBrowser, _ => false)
                ? await RotatedAsync(sessions, cookies, pressing, context, Answers.Of(landed, Landed), cancellationToken)
                    .ConfigureAwait(false)
                : Answers.Of(landed, Landed);
        }

        if (request.Code is not { Length: > 0 } code)
        {
            return Answers.Malformed("code");
        }

        if (browser.Live is not Session typing)
        {
            return Opened(browser) is not EnrolmentSessionId enrolment
                ? Nobody()
                : Answers.Of(
                    await identifiers
                        .VerifyAsync(enrolment, id, code, source, cancellationToken)
                        .ConfigureAwait(false),
                    Nothing);
        }

        Result verified = await identifiers
            .VerifyAsync(browser.Asking, typing.Id, id, code, source, cancellationToken)
            .ConfigureAwait(false);

        return verified.Match(() => true, _ => false)
            ? await RotatedAsync(sessions, cookies, typing, context, Nothing, cancellationToken)
                .ConfigureAwait(false)
            : Answers.Of(verified, Nothing);
    }

    private static async Task<IResult> MakePrimaryAsync(
        IdentifierId id,
        IIdentifiers identifiers,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(context);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await identifiers
                .MakePrimaryAsync(
                    holder,
                    id,
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

        AccessContext holder = Asking(browser);

        if (!Chosen(request.Setting, out BackupChoice choice, out IdentifierId? named))
        {
            return Answers.Malformed("setting");
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

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await account
                .DeactivateAsync(
                    holder,
                    browser.Required.Id,
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
            ? Answers.Malformed("linkToken")
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

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await account
                .DeleteAsync(
                    holder,
                    browser.Required.Id,
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
            ? Answers.Malformed("linkToken")
            : Answers.Of(
                await account.CancelDeletionAsync(token, cancellationToken).ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> RemoveIdentifierAsync(
        IdentifierId id,
        IIdentifiers identifiers,
        SessionService sessions,
        BrowserSessionCookies cookies,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentNullException.ThrowIfNull(context);

        AccessContext holder = Asking(browser);

        Result removed = await identifiers
            .RemoveAsync(
                holder,
                browser.Required.Id,
                id,
                RequestOrigin.Source(context.Request),
                cancellationToken)
            .ConfigureAwait(false);

        return removed.Match(() => true, _ => false)
            ? await RotatedAsync(sessions, cookies, browser.Required, context, Nothing, cancellationToken)
                .ConfigureAwait(false)
            : Answers.Of(removed, Nothing);
    }

    // BFF-SESS-004, IDN-LIFE-008: a change to what signs in to the account is a
    // privilege change, so the session that made it answers to a new secret from here
    // on and the one it held before answers nothing.
    private static async Task<IResult> RotatedAsync(
        SessionService sessions,
        BrowserSessionCookies cookies,
        Session live,
        HttpContext context,
        IResult answered,
        CancellationToken cancellationToken) =>
        Answers.Of(
            await sessions.RotateAsync(live, cancellationToken).ConfigureAwait(false),
            issued =>
            {
                cookies.Write(context.Response, issued);

                return answered;
            });

    // REG-IDENT-006: link-borne, because after a hostile removal the account has no
    // session that could reach this on its own.
    private static async Task<IResult> UndoIdentifierAsync(
        IdentifierId id,
        LinkTokenRequest request,
        IIdentifiers identifiers,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(context);

        return request.LinkToken is not { Length: > 0 } token
            ? Answers.Malformed("linkToken")
            : Answers.Of(
                await identifiers
                    .UndoAsync(token, RequestOrigin.Source(context.Request), cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> ReplaceIdentifierAsync(
        IdentifierId id,
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
            return Answers.Malformed("value");
        }

        string source = RequestOrigin.Source(context.Request);

        // AUTH-RECOV-002: the enrolment session an approver opened for a lost mailbox
        // reaches this endpoint, where the new address confirms alone (REG-IDENT-007).
        if (browser.Context is not AccessContext holder || browser.Live is null)
        {
            return Opened(browser) is not EnrolmentSessionId enrolment
                ? Nobody()
                : Answers.Of(
                    await identifiers
                        .ReplaceAsync(
                            enrolment,
                            id,
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
                    id,
                    value,
                    source,
                    cancellationToken)
                .ConfigureAwait(false),
            Accepted);
    }

    // REG-SESS-003: the ending control of a link opened in another browser, which
    // needs no session and answers the same way whatever the token resolves to.
    private static async Task<IResult> AbandonIdentifierAsync(
        IdentifierId id,
        LinkTokenRequest request,
        IIdentifiers identifiers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(identifiers);

        return request.LinkToken is not { Length: > 0 } token
            ? Answers.Malformed("linkToken")
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

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await accounts.ListCredentialsAsync(holder, cancellationToken).ConfigureAwait(false),
            credentials => TypedResults.Json<IReadOnlyList<CredentialView>>(
                [.. credentials.Select(CredentialView.Of)],
                AccountJson.Default.IReadOnlyListCredentialView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> LabelCredentialAsync(
        AuthenticatorId id,
        LabelRequest request,
        IAccount accounts,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(accounts);

        AccessContext holder = Asking(browser);

        return request.Label is not { } label
            ? Answers.Malformed("label")
            : Answers.Of(
                await accounts
                    .LabelCredentialAsync(holder, id, label, cancellationToken)
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

        AccessContext holder = Asking(browser);

        return request.Method is not Guid method
            ? Answers.Malformed("method")
            : Answers.Of(
                await accounts
                    .PreferSecondStepAsync(holder, new AuthenticatorId(method), cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static async Task<IResult> ListSessionsAsync(
        ISessions sessions,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await sessions
                .ListAsync(holder, browser.Required.Id, cancellationToken)
                .ConfigureAwait(false),
            held => TypedResults.Json<IReadOnlyList<SessionView>>(
                [.. held.Select(SessionView.Of)],
                AccountJson.Default.IReadOnlyListSessionView,
                contentType: null,
                StatusCodes.Status200OK));
    }

    private static async Task<IResult> EndSessionAsync(
        SessionId id,
        ISessions sessions,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        AccessContext holder = Asking(browser);

        return Answers.Of(
            await sessions
                .EndAsync(holder, id, cancellationToken)
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

    // BFF-STEP-001: the endpoints that read this are mounted as ones that need a
    // session, so the stage that requires one has already answered a request that
    // arrived without it.
    private static AccessContext Asking(RequestSession browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return browser.Asking;
    }

    // D-148, D-189: the enrolment session stage 5 resolved, which it does here on the
    // two routes chapter 09 section 3 names and on nothing else.
    private static EnrolmentSessionId? Opened(RequestSession browser) =>
        browser.Enrolment;

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
