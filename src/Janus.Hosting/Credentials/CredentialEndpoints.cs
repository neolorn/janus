using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The credential endpoints of chapter 09 sections 4 and 6.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-006, API-CONV-001, LIB-API-005, AUTH-FACT-001,
/// AUTH-FACT-002b, AUTH-FACT-007, AUTH-FACT-008, AUTH-STEP-007 and AUTH-RECOV-006.
/// Each of these answers to a session or to the enrolment session the browser
/// carries, and to nothing else: which of the two it is, is what the session
/// resolution stage established and never what the request says (D-148).
/// </remarks>
internal static class CredentialEndpoints
{
    private static readonly IResult Nothing = TypedResults.NoContent();

    /// <summary>
    /// Mounts them.
    /// </summary>
    /// <param name="endpoints">Where the host is mounting the library.</param>
    /// <returns>The builder, so the caller can go on.</returns>
    /// <exception cref="ArgumentNullException">The route builder is absent.</exception>
    public static IEndpointRouteBuilder MapCredentials(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder account = endpoints.MapGroup("/account");

        _ = account.MapPost("/password", SetPasswordAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.StepUpRequired, ErrorCodes.Restricted,
                    ErrorCodes.Denied, ErrorCodes.PasswordBlocklisted, ErrorCodes.PasswordTooShort,
                    ErrorCodes.PasswordTooLong))
            .Produces(StatusCodes.Status204NoContent);
        _ = account.MapDelete("/credentials/{id}", RemoveAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.CredentialLastSecondFactor,
                    ErrorCodes.StepUpRequired, ErrorCodes.Restricted)
                .Binding<AuthenticatorId>("id"))
            .Produces(StatusCodes.Status204NoContent);
        _ = account.MapPost("/credentials/{id}/upgrade", UpgradeAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.StepUpRequired, ErrorCodes.Restricted,
                    ErrorCodes.Denied, ErrorCodes.CredentialNotFound,
                    ErrorCodes.CredentialNotUpgradable)
                .Binding<AuthenticatorId>("id"))
            .Produces<CredentialCeremonyView>();
        _ = account.MapPost("/factors/totp/begin", BeginGeneratorAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.StepUpRequired, ErrorCodes.Restricted,
                    ErrorCodes.Denied, ErrorCodes.FactorPasswordRequired))
            .Produces<GeneratorEnrolmentView>();
        _ = account.MapPost("/factors/totp/confirm", ConfirmGeneratorAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.StepUpRequired, ErrorCodes.Restricted,
                    ErrorCodes.Denied, ErrorCodes.FactorPasswordRequired))
            .Produces<EnrolledCredentialView>();
        _ = SessionRequired.On(account.MapPost("/recoverycodes", GenerateRecoveryCodesAsync))
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.StepUpRequired, ErrorCodes.Restricted, ErrorCodes.Denied,
                    ErrorCodes.FactorPasswordRequired))
            .Produces<RecoveryCodesView>();
        _ = account.MapPost("/recoverycodes/exported", MarkRecoveryCodesExportedAsync)
            .Declares(EndpointDeclaration.Answering(ErrorCodes.SessionExpired, ErrorCodes.FactorNotEnrolled))
            .Produces(StatusCodes.Status204NoContent);

        RouteGroupBuilder ceremonies = endpoints.MapGroup("/auth/webauthn/register");

        _ = ceremonies.MapPost("/begin", BeginKeyAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.StepUpRequired, ErrorCodes.Restricted,
                    ErrorCodes.Denied, ErrorCodes.FactorPasswordRequired,
                    ErrorCodes.WebAuthnAlgorithmNotAllowed, ErrorCodes.WebAuthnUserVerificationRequired,
                    ErrorCodes.CredentialLabelInvalid))
            .Produces<CredentialCeremonyView>();
        _ = ceremonies.MapPost("/complete", CompleteKeyAsync)
            .Declares(EndpointDeclaration
                .Answering(
                    ErrorCodes.SessionExpired, ErrorCodes.StepUpRequired, ErrorCodes.Restricted,
                    ErrorCodes.Denied, ErrorCodes.FactorPasswordRequired,
                    ErrorCodes.WebAuthnAlgorithmNotAllowed, ErrorCodes.WebAuthnUserVerificationRequired,
                    ErrorCodes.CredentialLabelInvalid))
            .Produces<EnrolledCredentialView>();

        return endpoints;
    }

    // AUTH-PASS-001a, AUTH-RECOV-007a: the floor follows what the account reaches
    // now, and a password set here clears the mark an invalidation left.
    private static async Task<IResult> SetPasswordAsync(
        SetPasswordRequest request,
        ICredentials credentials,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(context);

        if (Asking(browser) is not CredentialAuthority authority)
        {
            return Nobody();
        }

        return request.Password is not { Length: > 0 } password
            ? Answers.Malformed("password")
            : Answers.Of(
                await credentials
                    .SetPasswordAsync(
                        authority,
                        password,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // AUTH-FACT-002b: the kind decides what the browser is asked for, and a second
    // step is refused on an account that holds no password.
    private static async Task<IResult> BeginKeyAsync(
        BeginKeyRequest request,
        ICredentials credentials,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credentials);

        if (Enrolling(browser) is not CredentialAuthority authority)
        {
            return Nobody();
        }

        return request.Kind is not Factor kind
            ? Answers.Malformed("kind")
            : Answers.Of(
                await credentials.BeginKeyAsync(authority, kind, cancellationToken)
                    .ConfigureAwait(false),
                Ceremony);
    }

    // AUTH-FACT-014: what the browser sends back is judged against the challenge the
    // ceremony was opened with, which the request never carries.
    private static async Task<IResult> CompleteKeyAsync(
        CompleteKeyRequest request,
        ICredentials credentials,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(context);

        if (Enrolling(browser) is not CredentialAuthority authority)
        {
            return Nobody();
        }

        if (request.Credential is not AuthenticatorAttestation attestation)
        {
            return Answers.Malformed("credential");
        }

        if (request.Label is not { Length: > 0 } label)
        {
            return Answers.Malformed("label");
        }

        return Answers.Of(
                await credentials
                    .CompleteKeyAsync(
                        authority,
                        attestation,
                        label,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Enrolled);
    }

    // AUTH-FACT-002b: the ceremony the upgrade opens is completed at the same place
    // any other is, because it is the same ceremony.
    private static async Task<IResult> UpgradeAsync(
        AuthenticatorId id,
        ICredentials credentials,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        return Asking(browser) is not CredentialAuthority authority
            ? Nobody()
            : Answers.Of(
                await credentials
                    .UpgradeKeyAsync(authority, id, cancellationToken)
                    .ConfigureAwait(false),
                Ceremony);
    }

    // AUTH-FACT-007: the secret leaves the library once, here, and the enrolment is
    // not usable until a code of it is presented.
    private static async Task<IResult> BeginGeneratorAsync(
        GeneratorRequest request,
        ICredentials credentials,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credentials);

        if (Enrolling(browser) is not CredentialAuthority authority)
        {
            return Nobody();
        }

        return request.Label is not { Length: > 0 } label
            ? Answers.Malformed("label")
            : Answers.Of(
                await credentials.BeginGeneratorAsync(authority, label, cancellationToken)
                    .ConfigureAwait(false),
                begun => TypedResults.Json(
                    GeneratorEnrolmentView.Of(begun),
                    CredentialsJson.Default.GeneratorEnrolmentView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    // AUTH-RECOV-006: the codes a second step beside a password brings with it come
    // back with the confirmation and are never read back.
    private static async Task<IResult> ConfirmGeneratorAsync(
        ConfirmGeneratorRequest request,
        ICredentials credentials,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(context);

        if (Enrolling(browser) is not CredentialAuthority authority)
        {
            return Nobody();
        }

        if (!Guid.TryParse(request.CredentialId, out Guid credential))
        {
            return Answers.Malformed("credentialId");
        }

        if (request.Code is not { Length: > 0 } code)
        {
            return Answers.Malformed("code");
        }

        return Answers.Of(
                await credentials
                    .ConfirmGeneratorAsync(
                        authority,
                        new AuthenticatorId(credential),
                        code,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Enrolled);
    }

    // AUTH-FACT-009: the whole previous set stops validating, and the new one is
    // shown once.
    // AUTH-RECOV-002: the enrolment session is not among those the set is generated
    // under, so only the holder of a session asks here.
    private static async Task<IResult> GenerateRecoveryCodesAsync(
        ICredentials credentials,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(browser);

        return Holding(browser) is not CredentialAuthority authority
            ? Nobody()
            : Answers.Of(
                await credentials.GenerateRecoveryCodesAsync(authority, cancellationToken)
                    .ConfigureAwait(false),
                generated => TypedResults.Json(
                    RecoveryCodesView.Of(generated),
                    CredentialsJson.Default.RecoveryCodesView,
                    contentType: null,
                    StatusCodes.Status200OK));
    }

    // AUTH-FACT-008: the report is about the set of the account whose session the
    // browser holds, and an enrolment session reports none.
    private static async Task<IResult> MarkRecoveryCodesExportedAsync(
        ICredentials credentials,
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(browser);

        return browser.Context is not AccessContext holder
            ? Nobody()
            : Answers.Of(
                await credentials.MarkRecoveryCodesExportedAsync(holder, cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    // AUTH-RECOV-007, D-141: a removal that would lower what the account reaches
    // suspends the credential for the notified window instead, which the contract
    // says with the code that carries when the window ends.
    private static async Task<IResult> RemoveAsync(
        AuthenticatorId id,
        ICredentials credentials,
        RequestSession browser,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(context);

        return Asking(browser) is not CredentialAuthority authority
            ? Nobody()
            : Answers.Of(
                await credentials
                    .RemoveAsync(
                        authority,
                        id,
                        RequestOrigin.Source(context.Request),
                        cancellationToken)
                    .ConfigureAwait(false),
                Nothing);
    }

    private static IResult Ceremony(CredentialCeremony ceremony) =>
        TypedResults.Json(
            CredentialCeremonyView.Of(ceremony),
            CredentialsJson.Default.CredentialCeremonyView,
            contentType: null,
            StatusCodes.Status200OK);

    private static IResult Enrolled(EnrolledCredential enrolled) =>
        TypedResults.Json(
            EnrolledCredentialView.Of(enrolled),
            CredentialsJson.Default.EnrolledCredentialView,
            contentType: null,
            StatusCodes.Status200OK);

    // D-148: a session, or the enrolment session the browser's first contact carries,
    // and never both at once.
    private static CredentialAuthority? Asking(RequestSession browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return Holding(browser)
            ?? (browser.FirstContact?.Enrolment is EnrolmentSessionId opened
                ? CredentialAuthority.Of(opened)
                : null);
    }

    private static CredentialAuthority? Holding(RequestSession browser) =>
        browser.Context is AccessContext holder && browser.Live is Session live
            ? CredentialAuthority.Of(holder, live.Id)
            : null;

    // API-CONV-003: nobody is asking, which is what 401 is for and what nothing else
    // is for.
    // REG-SESS-006: an enrolment is also asked for under the registration session the
    // browser carries, which the service accepts in place of an account's session at
    // the security step and at no other.
    private static CredentialAuthority? Enrolling(RequestSession browser) =>
        Asking(browser)
        ?? (browser.FirstContact?.Registration is RegistrationSessionId registering
            ? CredentialAuthority.Of(registering)
            : null);

    private static IResult Nobody() => Answers.Refused(ErrorCodes.SessionExpired);
}
