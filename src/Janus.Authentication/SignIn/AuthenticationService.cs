using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Organizations;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.SignIn;

/// <summary>
/// Signing in: one identifier opens a challenge, factors are presented against it
/// until what they reach admits a session, and the new-device check may hold the last
/// step for a code.
/// </summary>
/// <param name="challenges">Where sign-ins in progress are held.</param>
/// <param name="links">The links and codes a channel carries.</param>
/// <param name="identifiers">Where an identifier is resolved to an account.</param>
/// <param name="accounts">Where the account's state and profile are read.</param>
/// <param name="authenticators">Where enrolled credentials are read.</param>
/// <param name="passwordStore">Where the account's password is read.</param>
/// <param name="passwords">What judges a password.</param>
/// <param name="totp">What judges a generated code.</param>
/// <param name="recoveryCodes">What judges a one-use code.</param>
/// <param name="webAuthn">What judges a ceremony.</param>
/// <param name="devices">The browsers the account knows.</param>
/// <param name="sessionStore">Where a live session is read.</param>
/// <param name="sessions">What begins and raises a session.</param>
/// <param name="audit">Where a refused factor is written down.</param>
/// <param name="policies">What policy governs the account, and what it has raised.</param>
/// <param name="domainLock">Whether the address a sign-in was opened with is one a member may use.</param>
/// <param name="throttle">The progressive delay.</param>
/// <param name="sending">Where a message goes out.</param>
/// <param name="signals">What is known about a number before a text leans on it.</param>
/// <param name="guard">What a step-up left without its text code is judged by.</param>
/// <param name="codes">The verification codes, which live and die on their own rules.</param>
/// <param name="configuration">Where the lifetimes and the limits come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a handle and a code are drawn from.</param>
/// <remarks>
/// Implements LIB-API-005, AUTH-FACT-001 to AUTH-FACT-004, AUTH-FACT-015 to
/// AUTH-FACT-017, AUTH-STEP-001, AUTH-ABUSE-001 to AUTH-ABUSE-003, REG-DOM-001 and
/// CONV-LOG-005. An identifier that resolves to nothing is carried through every step
/// exactly as one that resolves to an account, so that nothing in the shape of an answer
/// tells the two apart. A domain lock is judged once a factor has succeeded, as
/// everything else about the account is. A factor refused at sign-in or at a step-up is
/// written to the audit trail, which no log level governs.
/// </remarks>
internal sealed class AuthenticationService(
    IChallengeStore challenges,
    SignInLinks links,
    IIdentifierDirectory identifiers,
    IAccountDirectory accounts,
    IAuthenticatorStore authenticators,
    IPasswordStore passwordStore,
    PasswordService passwords,
    TotpService totp,
    RecoveryCodeService recoveryCodes,
    WebAuthnService webAuthn,
    DeviceService devices,
    ISessionStore sessionStore,
    SessionService sessions,
    ISessionAudit audit,
    PolicyResolution policies,
    DomainLock domainLock,
    ThrottleService throttle,
    IGovernedSend sending,
    PhoneSignals signals,
    StepUpGuard guard,
    VerificationCodes codes,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : IAuthentication
{
    // AUTH-ABUSE-001: every code a factor refuses what was presented to it with. A
    // failure under any other code is no judgement of what was presented.
    private static readonly FrozenSet<ErrorCode> Refusals = new[]
    {
        ErrorCodes.FactorRejected,
        ErrorCodes.FactorNotPermitted,
        ErrorCodes.CodeInvalid,
        ErrorCodes.CodeExpired,
        ErrorCodes.CodeReplayed,
        ErrorCodes.CredentialSuspended,
        ErrorCodes.WebAuthnAlgorithmNotAllowed,
        ErrorCodes.WebAuthnCounterMismatch,
        ErrorCodes.WebAuthnRelyingPartyChanged,
        ErrorCodes.WebAuthnUserVerificationRequired,
    }.ToFrozenSet();

    /// <summary>
    /// Opens a sign-in for an identifier, with the tokens only the browser boundary
    /// can read.
    /// </summary>
    /// <param name="identifier">The identifier as it was entered.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="remembered">
    /// The token saying this browser has passed the new-device check, or nothing.
    /// </param>
    /// <param name="trusted">
    /// The token saying this browser is trusted for the second step, or nothing.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The challenge, or the delay the attempt has earned.</returns>
    /// <exception cref="ArgumentNullException">The identifier is absent.</exception>
    /// <remarks>
    /// Implements AUTH-ABUSE-001 AC5. A browser the account knows by a token that
    /// stands for this account is not held by the components an attacker raises from
    /// anywhere; a token that stands for nothing, or for another account, exempts it
    /// from nothing.
    /// </remarks>
    public async ValueTask<Result<SignInChallenge>> BeginAsync(
        string identifier,
        string source,
        [NeverLogged] string? remembered,
        [NeverLogged] string? trusted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        Error? failure = null;

        (SubjectId? subject, IdentifierId? email, byte[] counted) = await OpenerAsync(identifier, cancellationToken)
            .ConfigureAwait(false);

        var attempt = new ThrottleAttempt(source, counted)
        {
            Account = subject,
            Recognised = await RecognisedAsync(subject, remembered, trusted, cancellationToken)
                .ConfigureAwait(false),
        };

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error held)
        {
            return Result.Failure<SignInChallenge>(held);
        }

        Policy system = (await configuration.ReadAsync(Settings.PolicyDefault, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.CodeVerificationLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInChallenge>(failure);
        }

        RelyingParty party = await RelyingParty.ForAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        var handle = OpaqueToken.Draw(randomness);
        var ceremony = OpaqueToken.Draw(randomness);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<SignInChallenge>(notBegun);
        }

        await challenges
            .AddAsync(
                Challenge.Open(handle, subject, email, counted, ceremony.Value, time.GetUtcNow(), lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<SignInChallenge>(notCommitted);
        }

        // The list is the deployment's enabled primary set and never the account's, so
        // that the answer is the same for an identifier that exists and one that does
        // not; what the account holds surfaces only after a factor succeeds
        // (AUTH-ABUSE-003, 09 section 3).
        return Result.Success(new SignInChallenge(
            handle.Value,
            [.. system.LoginFactors.Where(factor => FactorCatalogue.Of(factor).CanBePrimary).Order()],
            new WebAuthnChallenge(party.Id, ceremony.Value)));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<SignInProgress>> PresentAsync(
        string challenge,
        FactorPresentation presented,
        DeviceDescription device,
        string source,
        CancellationToken cancellationToken) =>
        (await PresentAsync(
                challenge,
                presented,
                new SessionOrigin(source, device),
                remembered: null,
                trusted: null,
                cancellationToken)
            .ConfigureAwait(false))
        .Match(outcome => Result.Success(outcome.Progress), Result.Failure<SignInProgress>);

    /// <inheritdoc/>
    public async ValueTask<Result<SignInProgress>> VerifyDeviceAsync(
        string challenge,
        [NeverLogged] string code,
        DeviceDescription device,
        string source,
        CancellationToken cancellationToken) =>
        (await VerifyDeviceAsync(
                challenge,
                code,
                new SessionOrigin(source, device),
                cancellationToken)
            .ConfigureAwait(false))
        .Match(outcome => Result.Success(outcome.Progress), Result.Failure<SignInProgress>);

    /// <inheritdoc/>
    public async ValueTask<Result<SignInProgress>> StepUpAsync(
        AccessContext context,
        SessionId session,
        string challenge,
        FactorPresentation presented,
        string source,
        CancellationToken cancellationToken) =>
        (await RaiseAsync(context, session, challenge, presented, source, cancellationToken)
            .ConfigureAwait(false))
        .Match(outcome => Result.Success(outcome.Progress), Result.Failure<SignInProgress>);

    /// <summary>
    /// Whether an entry is a code the library texts when it is asked for, as a second
    /// step, rather than one the person already holds.
    /// </summary>
    /// <param name="factor">The entry.</param>
    /// <returns>Whether it is asked for.</returns>
    public static bool Asks(Factor factor) => PendingSignIn.NamesCredential(factor);

    /// <summary>
    /// Asks for the code of a second step the library texts, for a sign-in a first
    /// factor has been accepted for or for a step-up of the asking account's own.
    /// </summary>
    /// <param name="challenge">The handle the sign-in or step-up opened with.</param>
    /// <param name="factor">The second step asked for.</param>
    /// <param name="stepping">The account stepping up, or nothing at a sign-in.</param>
    /// <param name="session">The session the step-up raises, or nothing at a sign-in.</param>
    /// <param name="source">The address the ask came from.</param>
    /// <param name="language">The language the ask was made in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the ask is answered as every ask is; at a sign-in whose number's
    /// signal answers <c>risk</c>, what the challenge then offers, or
    /// <c>auth.factor.rejected</c> where it offers nothing; at a step-up whose number's
    /// signal answers <c>risk</c>, the factors of the combinations left without the
    /// entry, none where the session already meets the gate, or
    /// <c>auth.stepup.required</c> where no combination is left; or the refusal of the
    /// send.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <remarks>
    /// Implements AUTH-FACT-002 AC6 and AC7 and AUTH-FACT-002b AC6. A handle that opens
    /// nothing, a sign-in no first factor has been accepted for, an account holding no
    /// such credential and a policy that does not permit it are sent nothing and
    /// answered as an ask that sent its code. A number whose signal answers
    /// <c>risk</c> after a first factor is sent nothing either, and the sign-in is told
    /// what is left to present, which an anonymous caller is never told. At a step-up,
    /// whose challenge names no action, what is left is judged against the strictest of
    /// the policy's gates, field by field, and answered as a sign-in's ask is
    /// (AUTH-STEP-002, D-187, D-188). An ask after a first factor, or under a session,
    /// that names a suspended number sends nothing and is refused
    /// <c>auth.credential.suspended</c>, counting nothing, since an ask presents no
    /// factor (AUTH-RECOV-007 AC8, D-190). The code an ask sends is issued for one
    /// credential, which its record names (AUTH-FACT-004): of the account's active
    /// credentials of the entry, the one offered first (IDN-ATTR-008).
    /// </remarks>
    public async ValueTask<Result<SignInProgress?>> AskAsync(
        string challenge,
        Factor factor,
        SubjectId? stepping,
        SessionId? session,
        string source,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(language);

        Challenge? open = await OpenAsync(challenge, cancellationToken).ConfigureAwait(false);

        if (!Asks(factor)
            || open?.Subject is not SubjectId subject
            || (stepping is SubjectId asking ? asking != subject : open.Presented.Count is 0)
            || await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false) is not AccountState.Active)
        {
            return Result.Success<SignInProgress?>(null);
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators.OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);
        Authenticator? issuedFor = SecondStep.Preferred(
            enrolled.Where(credential => credential.IsUsable && credential.Factor == factor));

        if (issuedFor is null
            && !enrolled.Any(credential => credential.IsAwaitingInvalidation && credential.Factor == factor))
        {
            return Result.Success<SignInProgress?>(null);
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        // IDN-LIFE-009b: a second step the policy in force does not permit would be
        // refused when presented, so nothing is sent for it.
        if (failure is not null || !policy.LoginFactors.Contains(factor))
        {
            return failure is null
                ? Result.Success<SignInProgress?>(null)
                : Result.Failure<SignInProgress?>(failure);
        }

        // AUTH-RECOV-007 AC8: the ask names a suspended number, after a first factor or
        // under a session, so its caller is told so. Nothing is sent, and nothing is
        // counted or recorded, an ask presenting no factor.
        if (issuedFor is null)
        {
            return Result.Failure<SignInProgress?>(Error.From(ErrorCodes.CredentialSuspended));
        }

        bool sent = (await links
                .SendSecondStepAsync(open.Fingerprint, issuedFor, source, language, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInProgress?>(failure);
        }

        if (sent)
        {
            return Result.Success<SignInProgress?>(null);
        }

        if (stepping is not null)
        {
            return await WithoutTextsAsync(subject, session, cancellationToken).ConfigureAwait(false);
        }

        // AUTH-FACT-002b AC6: the entries a text carries ride the one number, so the
        // signal withholds them all, and the sign-in is answered with the second steps
        // it still offers; one left with none is refused, never completed.
        Assurance reached = Assurance.Reached(Properties(open.Presented))
            ?? new Assurance(AssuranceLevel.Delegated, PhishingResistant: false);

        List<Factor> wanted = Wanted(
            policy,
            await authenticators.OfAsync(subject, cancellationToken).ConfigureAwait(false),
            reached,
            trusts: false);

        _ = wanted.RemoveAll(entry => FactorCatalogue.Of(entry).Restricted);

        return wanted.Count is 0
            ? Result.Failure<SignInProgress?>(Error.From(ErrorCodes.FactorRejected))
            : Result.Success<SignInProgress?>(new SignInProgress(
                SignInStatus.FactorRequired,
                reached.Level,
                reached.PhishingResistant,
                wanted,
                TrustDeviceOffered: false,
                Session: null,
                Requirement: null,
                PasswordChangeRequired: false));
    }

    /// <inheritdoc/>
    public ValueTask<Result> SendLinkAsync(
        string identifier,
        string language,
        string source,
        string? browser,
        CancellationToken cancellationToken) =>
        links.SendLinkAsync(identifier, language, source, browser, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> SendCodeAsync(
        string identifier,
        string language,
        string source,
        CancellationToken cancellationToken) =>
        links.SendCodeAsync(identifier, language, source, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<Result<SignInLanding>> LandAsync(
        string challenge,
        string? browser,
        [NeverLogged] string linkToken,
        Factor factor,
        bool press,
        DeviceDescription device,
        string source,
        CancellationToken cancellationToken) =>
        (await LandAsync(
                challenge,
                browser,
                linkToken,
                factor,
                press,
                new SessionOrigin(source, device),
                remembered: null,
                cancellationToken)
            .ConfigureAwait(false))
        .Match(
            landed => Result.Success(
                new SignInLanding(landed.Outcome?.Progress, landed.SameBrowser, landed.Code)),
            Result.Failure<SignInLanding>);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<DeviceSummary>>> ListDevicesAsync(
        AccessContext context,
        CancellationToken cancellationToken) =>
        devices.ListAsync(context, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> ForgetDeviceAsync(
        AccessContext context,
        DeviceId device,
        CancellationToken cancellationToken) =>
        devices.RemoveAsync(context, device, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> AbandonLinkAsync([NeverLogged] string linkToken, CancellationToken cancellationToken) =>
        links.AbandonAsync(linkToken, cancellationToken);

    /// <summary>
    /// Signs in the account a social provider's identity is linked to, which the
    /// provider has just vouched for.
    /// </summary>
    /// <param name="provider">Which provider vouched.</param>
    /// <param name="providerSubject">The provider's own identifier for the person.</param>
    /// <param name="origin">Where the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The completed sign-in, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <remarks>
    /// Implements AUTH-FACT-002a, REG-IDENT-008, AUTH-ABUSE-001, CONV-LOG-005 and
    /// AUTH-RECOV-007 AC9. The provider established who this is, so no second step is
    /// asked for and the session records <c>delegated</c>. An identity linked to no
    /// account, a credential invalidated, and an account that is not active are one
    /// refusal, as they are for every other factor (AUTH-ABUSE-003), recorded and
    /// counted as a refused factor is, behind the same delay. A credential that is
    /// suspended, on a window or held after its provider's security event, is refused
    /// <c>auth.credential.suspended</c> on an account that would otherwise sign in,
    /// since the provider's vouching is the proof that verifies (IDN-LIFE-012a, D-191).
    /// </remarks>
    public async ValueTask<Result<SignInOutcome>> DelegatedAsync(
        Factor provider,
        [NeverLogged] string providerSubject,
        SessionOrigin origin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerSubject);
        ArgumentNullException.ThrowIfNull(origin);

        Authenticator? linked = await authenticators
            .ByProviderAsync(provider, providerSubject, cancellationToken)
            .ConfigureAwait(false);

        var attempt = new ThrottleAttempt(origin.Source, null) { Account = linked?.Subject };

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error held)
        {
            return Result.Failure<SignInOutcome>(held);
        }

        // AUTH-RECOV-007: a suspended credential is judged first as an active one would
        // be, so the state of its account refuses it as it refuses an active one.
        bool suspended = linked is not null && (linked.IsAwaitingInvalidation || linked.IsHeldByProvider);

        // IDN-ACCT-007: a restricted account signs in and reads, as an active one does.
        if (linked is null
            || !(linked.IsUsable || suspended)
            || await accounts.StateAsync(linked.Subject, cancellationToken).ConfigureAwait(false)
                is not (AccountState.Active or AccountState.Restricted))
        {
            return Result.Failure<SignInOutcome>(
                await CountedAsync(attempt, provider, linked?.Subject, null, cancellationToken).ConfigureAwait(false)
                ?? Error.From(ErrorCodes.FactorRejected));
        }

        // AUTH-RECOV-007 AC9, IDN-LIFE-012a: the provider's vouching is the proof that
        // verifies, so the credential's state is what refuses it now, and its owner is
        // told so: a failed attempt, recorded and counted.
        if (suspended)
        {
            return Result.Failure<SignInOutcome>(
                await CountedAsync(attempt, provider, linked.Subject, null, cancellationToken).ConfigureAwait(false)
                ?? Error.From(ErrorCodes.CredentialSuspended));
        }

        await throttle.SucceededAsync(attempt, cancellationToken).ConfigureAwait(false);

        return await IssueAsync(
                linked.Subject,
                [provider],
                origin,
                trustDevice: false,
                changeRequired: false,
                remembered: null,
                trusted: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Answers a provider's round trip whose identity did not hold up: the code was
    /// not traded, or the token it was traded for was not the provider's, for this
    /// application, now.
    /// </summary>
    /// <param name="provider">Which provider the round trip went to.</param>
    /// <param name="source">The address the browser came back from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The refusal, which is the delay where one stands.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    /// <remarks>
    /// Implements AUTH-ABUSE-001 and CONV-LOG-005. An identity that did not hold up is
    /// a refused factor naming no account: it is recorded and counted against its
    /// source, and while a delay stands it is neither, so records are written no
    /// faster than the delay lets attempts through.
    /// </remarks>
    public async ValueTask<Error> ProviderRefusedAsync(
        Factor provider,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var attempt = new ThrottleAttempt(source, null);

        return await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false)
            ?? await CountedAsync(attempt, provider, null, null, cancellationToken).ConfigureAwait(false)
            ?? Error.From(ErrorCodes.FactorRejected);
    }

    /// <summary>
    /// The delay a provider's round trip answers to before its code is traded, which
    /// only its source can have earned: nothing else about it is known yet.
    /// </summary>
    /// <param name="source">The address the browser came back from.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The refusal where a delay stands, or nothing.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    /// <remarks>
    /// Implements AUTH-ABUSE-001. Asked before the exchange, so an address that has
    /// earned a delay makes this server call no provider on its behalf.
    /// </remarks>
    public ValueTask<Error?> ExchangeDelayedAsync(string source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        return DelayedAsync(new ThrottleAttempt(source, null), cancellationToken);
    }

    /// <summary>
    /// Presents one factor, with what only the browser boundary can act on: the
    /// tokens this browser carries and the secrets a completed sign-in hands back.
    /// </summary>
    /// <param name="challenge">The handle the sign-in opened with.</param>
    /// <param name="presented">The factor and what proves it.</param>
    /// <param name="origin">Where the request came from.</param>
    /// <param name="remembered">
    /// The token saying this browser has passed the new-device check, or nothing.
    /// </param>
    /// <param name="trusted">
    /// The token saying this browser is trusted for the second step, or nothing.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the sign-in reached, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<SignInOutcome>> PresentAsync(
        string challenge,
        FactorPresentation presented,
        SessionOrigin origin,
        string? remembered,
        string? trusted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(origin);

        Challenge? open = await OpenAsync(challenge, cancellationToken).ConfigureAwait(false);

        // AUTH-ABUSE-001 AC5: only a token that stands for this sign-in's account
        // recognises the browser; carrying one proves nothing.
        var attempt = new ThrottleAttempt(origin.Source, open?.Identifier)
        {
            Account = open?.Subject,
            Recognised = await RecognisedAsync(open?.Subject, remembered, trusted, cancellationToken)
                .ConfigureAwait(false),
        };

        // The delay is asked before anything is judged, a handle that opens nothing
        // included, which its source alone answers for (AUTH-ABUSE-001).
        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error held)
        {
            return Result.Failure<SignInOutcome>(held);
        }

        // An identifier that resolved to nothing, and a handle that opens nothing,
        // reach exactly this point and stop, having been told what an account with the
        // wrong password is told (AUTH-ABUSE-003), and are recorded and counted as it
        // is (CONV-LOG-005).
        if (open?.Subject is not SubjectId subject
            || await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false)
                is not (AccountState.Active or AccountState.Restricted))
        {
            return Result.Failure<SignInOutcome>(
                await CountedAsync(attempt, presented.Factor, null, null, cancellationToken).ConfigureAwait(false)
                ?? Error.From(ErrorCodes.FactorRejected));
        }

        Error? refusal = await GivenUpAsync(open, subject, cancellationToken).ConfigureAwait(false)
            ? Error.From(ErrorCodes.FactorRejected)
            : null;

        if (refusal is not null)
        {
            return Result.Failure<SignInOutcome>(
                await CountedAsync(attempt, presented.Factor, subject, trusted, cancellationToken).ConfigureAwait(false)
                ?? refusal);
        }

        // A refusal the factor's judgement answers comes back counted and recorded.
        bool changeRequired = (await AcceptsAsync(
                    open,
                    subject,
                    presented,
                    token => CountedAsync(attempt, presented.Factor, subject, trusted, token),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<SignInOutcome>(refusal);
        }

        await throttle.SucceededAsync(attempt, cancellationToken).ConfigureAwait(false);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<SignInOutcome>(notBegun);
        }

        await challenges.RecordAsync(open, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<SignInOutcome>(notCommitted);
        }

        return await SettleAsync(
                open,
                subject,
                origin,
                presented.TrustDevice,
                changeRequired,
                remembered,
                trusted,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Completes a held sign-in with the code the account's primary email carried,
    /// handing back the secrets the browser is to carry away.
    /// </summary>
    /// <param name="challenge">The handle the sign-in opened with.</param>
    /// <param name="code">What was typed.</param>
    /// <param name="origin">Where the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the sign-in reached, or the failure the code produced.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<SignInOutcome>> VerifyDeviceAsync(
        string challenge,
        [NeverLogged] string code,
        SessionOrigin origin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(origin);

        Challenge? open = await OpenAsync(challenge, cancellationToken).ConfigureAwait(false);
        var attempt = new ThrottleAttempt(origin.Source, open?.Identifier) { Account = open?.Subject };

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error held)
        {
            return Result.Failure<SignInOutcome>(held);
        }

        // CONV-LOG-005: a refused code of the new-device check is recorded as the
        // verification it is, naming no factor, and counted against the delay, a
        // handle that opens nothing included.
        if (open?.Subject is not SubjectId subject)
        {
            return Result.Failure<SignInOutcome>(
                await CountedAsync(attempt, presented: null, null, null, cancellationToken).ConfigureAwait(false)
                ?? Error.From(ErrorCodes.CodeExpired));
        }

        Error? failure = null;

        // AUTH-FACT-004: the code answers to its own rules, so the sign-in learns only
        // whether it was the one outstanding and never holds it. The try is decided in
        // this unit of work, so a refused one commits its count on the code's record
        // with the refusal's record and the delay's counts (CONV-DESIGN-003).
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<SignInOutcome>(notBegun);
        }

        Result presented = await codes
            .PresentAsync(open.Fingerprint, code, cancellationToken)
            .ConfigureAwait(false);

        if (presented.Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure<SignInOutcome>(
                await RefusedAsync(
                        refused,
                        Answered(refused),
                        token => CountedAsync(attempt, presented: null, subject, null, token),
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<SignInOutcome>(notCommitted);
        }

        await throttle.SucceededAsync(attempt, cancellationToken).ConfigureAwait(false);

        OpaqueToken? browser = (await devices
                .VerifiedAsync(subject, origin.Device, cancellationToken)
                .ConfigureAwait(false))
            .Match(token => (OpaqueToken?)token, error => Withheld<OpaqueToken?>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        return await CompleteAsync(
                open,
                subject,
                origin,
                trustDevice: false,
                changeRequired: false,
                browser,
                trusted: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Raises a live session's assurance, handing back the secret it now answers to.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the request arrived on.</param>
    /// <param name="challenge">The handle the step-up opened with.</param>
    /// <param name="presented">The factor and what proves it.</param>
    /// <param name="source">The address the attempt came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the factors accepted so far reach together, and the factors still to present
    /// where they do not reach the gate; or the refusal.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <remarks>
    /// Implements AUTH-STEP-001, AUTH-STEP-002 step 2, AC4c and AC4d, AUTH-ABUSE-001 and
    /// CONV-LOG-005. A refused step-up factor is a failed authentication: it answers to
    /// the delay a sign-in answers to, counted against the same source and the same
    /// account, and the session it is presented on exempts it from nothing, since a
    /// session in someone else's hands is what a step-up is asked of. A step-up is
    /// called once per factor: each accepted factor is held on its challenge with those
    /// accepted before it, what they reach together is written into the session and
    /// answered, and the challenge ends once the session meets the strictest of the
    /// policy's gates, which is the gate of a step-up that names no action, or once no
    /// combination still offered can be completed with the factors accepted (D-187,
    /// D-190, D-191).
    /// </remarks>
    public async ValueTask<Result<SignInOutcome>> RaiseAsync(
        AccessContext context,
        SessionId session,
        string challenge,
        FactorPresentation presented,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId asking)
        {
            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.SessionExpired));
        }

        Session? live = await sessionStore.FindAsync(session, cancellationToken)
            .ConfigureAwait(false);

        if (live is null || live.Subject != asking || live.EndedAt is not null)
        {
            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.SessionExpired));
        }

        var attempt = new ThrottleAttempt(source, null) { Account = asking };

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error held)
        {
            return Result.Failure<SignInOutcome>(held);
        }

        // A step-up is a factor presented against a challenge exactly as a sign-in is,
        // so that a ceremony has a server-issued value to sign over; the challenge is
        // the asking principal's own or it is nobody's (AUTH-STEP-001).
        Challenge? open = await OpenAsync(challenge, cancellationToken).ConfigureAwait(false);

        if (open is null || open.Subject != asking)
        {
            return Result.Failure<SignInOutcome>(
                await StepUpRefusedAsync(attempt, live, presented.Factor, cancellationToken)
                    .ConfigureAwait(false)
                ?? Error.From(ErrorCodes.FactorRejected));
        }

        Error? unread = null;

        Policy policy = (await policies.ForAsync(asking, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref unread));

        if (unread is not null)
        {
            return Result.Failure<SignInOutcome>(unread);
        }

        // AUTH-STEP-002, IDN-LIFE-009b: a factor the policy in force does not permit
        // satisfies no gate, so at a step-up it is refused before it is verified, right
        // or wrong, and counted as a refused step-up factor. A sign-in refuses it only
        // after the factor succeeds, since its caller is not yet authenticated.
        if (!policy.LoginFactors.Contains(presented.Factor))
        {
            return Result.Failure<SignInOutcome>(
                await StepUpRefusedAsync(attempt, live, presented.Factor, cancellationToken)
                    .ConfigureAwait(false)
                ?? Error.From(ErrorCodes.FactorNotPermitted));
        }

        Error? refusal = null;

        // A refusal the factor's judgement answers comes back counted and recorded.
        _ = (await AcceptsAsync(
                    open,
                    asking,
                    presented,
                    token => StepUpRefusedAsync(attempt, live, presented.Factor, token),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<SignInOutcome>(refusal);
        }

        await throttle.SucceededAsync(attempt, cancellationToken).ConfigureAwait(false);

        Error? failure = null;

        // AUTH-STEP-002 step 2: the step-up names no action, so what it goes on asking
        // for is read from the strictest of the policy's gates, before the unit of work
        // begins, since the carrier's signal may be asked for it (AUTH-FACT-002b).
        StepUpChallenge asked = (await guard
                .ChallengeUnnamedAsync(asking, session, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<StepUpChallenge>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<SignInOutcome>(notBegun);
        }

        // D-166 X3: the challenge is held under its lock from before the session is
        // raised until it is recorded or removed, so a second raise on it waits, and
        // one that finds it gone is refused.
        Challenge? holding = await challenges.FindForUpdateAsync(open.Fingerprint, cancellationToken)
            .ConfigureAwait(false);

        if (holding is null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.FactorRejected));
        }

        // AUTH-STEP-002 step 2 (D-190): the factor accepted at this call joins those the
        // challenge holds from the calls before it, and the session is raised to what
        // they reach together.
        foreach (Factor accepted in open.Presented)
        {
            holding.Accepted(accepted);
        }

        await challenges.RecordAsync(holding, cancellationToken).ConfigureAwait(false);

        IssuedSession? raised = (await sessions
                .PresentAsync(live, holding.Presented, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => (IssuedSession?)value, error => Withheld<IssuedSession?>(error, ref failure));

        bool reached = failure is null
            && (await guard.ReachedAsync(live, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Withheld<bool>(error, ref failure));

        if (failure is not null || raised is null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<SignInOutcome>(failure ?? Error.From(ErrorCodes.FactorRejected));
        }

        // AUTH-STEP-002 step 2 (D-191): the step-up goes on asking until the gate is
        // reached, or until no combination still offered can be completed with the
        // factors accepted, which is one with a factor yet to present. The challenge
        // holds the factors no longer than that: one left behind would let a later
        // factor stand beside them again.
        List<Factor> required = reached
            ? []
            : [.. asked.Combinations
                .Select(combination => combination.Except(holding.Presented))
                .SelectMany(left => left)
                .Distinct()];

        // What the call answers with is what the factors accepted on the challenge
        // reach together, which is what it wrote, and never what the session reached
        // before them (AUTH-STEP-002 AC4d).
        Assurance together = Assurance.Proved(Properties(holding.Presented))
            ?? new Assurance(AssuranceLevel.Delegated, PhishingResistant: false);

        if (required.Count is 0)
        {
            await challenges.RemoveAsync(holding.Fingerprint, cancellationToken).ConfigureAwait(false);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<SignInOutcome>(notCommitted);
        }

        return Result.Success(new SignInOutcome(
            new SignInProgress(
                required.Count is 0 ? SignInStatus.Complete : SignInStatus.FactorRequired,
                together.Level,
                together.PhishingResistant,
                required,
                TrustDeviceOffered: false,
                raised.Id,
                Requirement: null,
                PasswordChangeRequired: false),
            raised,
            Remembered: null,
            Trusted: null));
    }

    /// <summary>
    /// What a sign-in link does where it was opened, handing back the secrets a press
    /// in the requesting browser produced.
    /// </summary>
    /// <param name="challenge">The handle the sign-in began with.</param>
    /// <param name="browser">What the asking browser carries, or nothing.</param>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="factor">The link factor the request named.</param>
    /// <param name="press">Whether the person pressed the control.</param>
    /// <param name="origin">Where the request came from.</param>
    /// <param name="remembered">
    /// The token saying this browser has passed the new-device check, or nothing.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The sign-in, or what the landing shows instead.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<LandedSignIn>> LandAsync(
        string challenge,
        string? browser,
        [NeverLogged] string linkToken,
        Factor factor,
        bool press,
        SessionOrigin origin,
        string? remembered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);
        ArgumentNullException.ThrowIfNull(origin);

        PendingSignIn? held = await links.FindAsync(linkToken, cancellationToken)
            .ConfigureAwait(false);

        if (held is null)
        {
            return Result.Failure<LandedSignIn>(
                press
                    ? await GoneAsync(new ThrottleAttempt(origin.Source, null), factor, cancellationToken)
                        .ConfigureAwait(false)
                    : Error.From(ErrorCodes.CodeExpired));
        }

        bool sameBrowser = held.SameBrowser(SignInLinks.Fingerprint(browser));

        // A plain open changes nothing, which is what defeats a mail scanner's
        // prefetch, and an open anywhere else shows the code to type where the sign-in
        // began (AUTH-FACT-003, REG-SESS-003).
        if (!press || !sameBrowser)
        {
            return Result.Success(new LandedSignIn(
                null,
                sameBrowser,
                sameBrowser ? null : VerificationCode.Read(held.Code)));
        }

        Challenge? open = await OpenAsync(challenge, cancellationToken).ConfigureAwait(false);

        var attempt = new ThrottleAttempt(origin.Source, open?.Identifier)
        {
            Account = held.Subject,
            Recognised = await RecognisedAsync(held.Subject, remembered, trusted: null, cancellationToken)
                .ConfigureAwait(false),
        };

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure<LandedSignIn>(delayed);
        }

        // CONV-LOG-005: a pressed link that lands on no sign-in of its account, or on
        // one opened with an address the account has given up since, is a refused
        // factor, recorded and counted as one (REG-IDENT-006 AC6).
        if (open is null
            || open.Subject != held.Subject
            || await GivenUpAsync(open, held.Subject, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<LandedSignIn>(
                await CountedAsync(attempt, held.Factor, held.Subject, null, cancellationToken).ConfigureAwait(false)
                ?? Error.From(ErrorCodes.FactorRejected));
        }

        // A link sent to an address given up since is refused and counted as one that
        // lands on no sign-in; a lock's refusal is only told (REG-IDENT-006 AC6).
        if (await links.LockedAsync(held, cancellationToken).ConfigureAwait(false) is Error locked)
        {
            return Result.Failure<LandedSignIn>(
                locked.Code == ErrorCodes.FactorRejected
                    ? await CountedAsync(attempt, held.Factor, held.Subject, null, cancellationToken)
                        .ConfigureAwait(false)
                        ?? locked
                    : locked);
        }

        open.Accepted(held.Factor);

        await throttle.SucceededAsync(attempt, cancellationToken).ConfigureAwait(false);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<LandedSignIn>(notBegun);
        }

        await challenges.RecordAsync(open, cancellationToken).ConfigureAwait(false);
        await links.SpendAsync(held, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<LandedSignIn>(notCommitted);
        }

        return (await SettleAsync(
                    open,
                    held.Subject,
                    origin,
                    trustDevice: false,
                    changeRequired: false,
                    remembered,
                    trusted: null,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(
                outcome => Result.Success(new LandedSignIn(outcome, SameBrowser: true, Code: null)),
                Result.Failure<LandedSignIn>);
    }

    private static FactorProperties[] Properties(IReadOnlyCollection<Factor> presented) =>
        [.. presented.Select(FactorCatalogue.Of)];

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // AUTH-FACT-002 AC7, AUTH-STEP-002 (D-187): a step-up whose text code the signal
    // withheld is answered with what it then offers. Its challenge names no action, so
    // that is judged against the strictest of the policy's gates, field by field, on the
    // session the step-up raises; a session that is not the account's judges no gate.
    // The ask is answered as a sign-in's is (D-188): with the factors of the
    // combinations left, or with none where the session already meets that gate, and
    // it is refused only where no combination is left, with the gate and what the
    // account does next.
    private async ValueTask<Result<SignInProgress?>> WithoutTextsAsync(
        SubjectId subject,
        SessionId? session,
        CancellationToken cancellationToken)
    {
        if (session is not SessionId raising
            || await sessionStore.FindAsync(raising, cancellationToken).ConfigureAwait(false) is not Session live
            || live.Subject != subject)
        {
            return Result.Failure<SignInProgress?>(Error.From(ErrorCodes.StepUpRequired));
        }

        Error? failure = null;

        StepUpChallenge left = (await guard
                .ChallengeWithoutTextsAsync(subject, raising, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<StepUpChallenge>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInProgress?>(failure);
        }

        List<Factor> required = left.Outcome is StepUpOutcome.Present
            ? [.. left.Combinations.SelectMany(combination => combination).Distinct()]
            : [];

        if (!StepUpRefusal.Met(left) && required.Count is 0)
        {
            return Result.Failure<SignInProgress?>(StepUpRefusal.Of(left));
        }

        return Result.Success<SignInProgress?>(new SignInProgress(
            required.Count is 0 ? SignInStatus.Complete : SignInStatus.FactorRequired,
            live.Attained,
            live.PhishingResistant,
            required,
            TrustDeviceOffered: false,
            Session: null,
            Requirement: null,
            PasswordChangeRequired: false));
    }

    // The account an identifier opens a sign-in for, the identifier itself where it is
    // an email address, which is what a domain lock is later judged on, and what the
    // identifier is counted under, which is worked out alike whether or not an account
    // holds it (AUTH-ABUSE-001).
    private async ValueTask<(SubjectId? Subject, IdentifierId? Email, byte[] Counted)> OpenerAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        bool usernames = (await configuration
                .ReadAsync(Settings.IdentifiersUsernameEnabled, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        string entered = identifier.Trim();
        byte[] counted = throttle.Identify(entered, usernames);

        if (failure is not null || IdentifierKinds.Detect(entered, usernames) is not { } kind)
        {
            return (null, null, counted);
        }

        string? canonical = kind switch
        {
            IdentifierKind.Email => EmailAddress.TryParse(entered, out EmailAddress address)
                ? address.Value
                : null,
            IdentifierKind.Phone => PhoneNumber.TryParse(entered, out PhoneNumber number)
                ? number.Value
                : null,
            _ => Username.TryParse(entered, out Username username) ? username.Value : null,
        };

        if (canonical is null
            || await identifiers.HolderAsync(kind, canonical, cancellationToken).ConfigureAwait(false)
                is not { } holder)
        {
            return (null, null, counted);
        }

        return (holder.Subject, kind is IdentifierKind.Email ? holder.Identifier : null, counted);
    }

    // AUTH-ABUSE-001 AC5: a browser is recognised by a token that resolves, stands for
    // the account being signed into and has not lapsed; asking changes nothing about the
    // token, so a stolen one is not refreshed by being tried. The tokens are looked up
    // whether or not the identifier resolved (AUTH-ABUSE-003).
    private ValueTask<bool> RecognisedAsync(
        SubjectId? subject,
        [NeverLogged] string? remembered,
        [NeverLogged] string? trusted,
        CancellationToken cancellationToken) =>
        devices.RecognisesAsync(subject, remembered, trusted, cancellationToken);

    private async ValueTask<Error?> DelayedAsync(ThrottleAttempt attempt, CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan delay = (await throttle.DelayAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        return delay > TimeSpan.Zero ? Error.Throttled(time.GetUtcNow() + delay) : null;
    }

    private async ValueTask<Challenge?> OpenAsync(string handle, CancellationToken cancellationToken)
    {
        if (handle is not { Length: > 0 })
        {
            return null;
        }

        Challenge? open = await challenges
            .FindAsync(OpaqueToken.Of(handle).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        return open is null || open.HasExpired(time.GetUtcNow()) ? null : open;
    }

    // Which service judges a presentation is read from the catalogue and never from
    // the entry's name, and only what that service accepted is recorded against the
    // challenge (AUTH-FACT-001). What comes back
    // beside the acceptance is whether the account is to be asked to change the
    // password it just used (AUTH-PASS-004).
    // A refusal comes back counted and recorded already, by the count its caller hands
    // in: a factor whose refusal keeps a write of its own (a code's wrong try, a
    // counter that did not advance) commits that write with the count, in one unit of
    // work (CONV-DESIGN-003). A fault of the library's own comes back as it is, neither
    // counted nor recorded (AUTH-ABUSE-001).
    private async ValueTask<Result<bool>> AcceptsAsync(
        Challenge open,
        SubjectId subject,
        FactorPresentation presented,
        Func<CancellationToken, ValueTask<Error?>> counted,
        CancellationToken cancellationToken)
    {
        if (FactorCatalogue.Entries.TryGetValue(presented.Factor, out FactorProperties? properties)
            && presented.Factor != FactorCatalogue.Password)
        {
            if (properties.IsWebAuthn)
            {
                return await CeremonyAsync(open, subject, presented, counted, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (FactorCatalogue.Delivered.Contains(presented.Factor))
            {
                return await CodeAsync(open, subject, presented, counted, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        Error? refusal = null;

        bool changeRequired = (await JudgedAsync(open, subject, presented, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref refusal));

        if (refusal is null)
        {
            return Result.Success(changeRequired);
        }

        return Result.Failure<bool>(
            Refuses(refusal)
                ? await counted(cancellationToken).ConfigureAwait(false) ?? refusal
                : refusal);
    }

    // The factors whose services end their own unit of work, a refusal of theirs
    // keeping no write of its own.
    private async ValueTask<Result<bool>> JudgedAsync(
        Challenge open,
        SubjectId subject,
        FactorPresentation presented,
        CancellationToken cancellationToken)
    {
        if (!FactorCatalogue.Entries.TryGetValue(
            presented.Factor,
            out FactorProperties? properties))
        {
            return Result.Failure<bool>(Error.From(ErrorCodes.FactorNotPermitted));
        }

        if (presented.Factor == FactorCatalogue.Password)
        {
            return await PasswordAsync(open, subject, presented, cancellationToken)
                .ConfigureAwait(false);
        }

        if (properties.SingleUse)
        {
            return Accepted(
                open,
                presented.Factor,
                await recoveryCodes
                    .SpendAsync(subject, presented.Value ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false));
        }

        if (presented.Factor == FactorCatalogue.Generated)
        {
            return Accepted(
                open,
                presented.Factor,
                await totp
                    .PresentAsync(subject, presented.Value ?? string.Empty, cancellationToken)
                    .ConfigureAwait(false));
        }

        // Nothing else in the catalogue is presented here: the federated entries
        // arrive through their provider and the emergency credential through its own
        // endpoint (AUTH-FACT-002, AUTH-STEP-004).
        return Result.Failure<bool>(Error.From(ErrorCodes.FactorNotPermitted));
    }

    private static Result<bool> Accepted(Challenge open, Factor factor, Result outcome)
    {
        Error? refusal = null;

        _ = outcome.Match(() => true, error => Withheld<bool>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<bool>(refusal);
        }

        open.Accepted(factor);

        return Result.Success(false);
    }

    private static Result<bool> Accepted<TValue>(Challenge open, Factor factor, Result<TValue> outcome)
    {
        Error? refusal = null;

        _ = outcome.Match(_ => true, error => Withheld<bool>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<bool>(refusal);
        }

        open.Accepted(factor);

        return Result.Success(false);
    }

    private async ValueTask<Result<bool>> PasswordAsync(
        Challenge open,
        SubjectId subject,
        FactorPresentation presented,
        CancellationToken cancellationToken)
    {
        byte[] entered = Encoding.UTF8.GetBytes(presented.Value ?? string.Empty);

        try
        {
            Error? refusal = null;

            PasswordVerification verified = (await passwords
                    .VerifyAsync(
                        subject,
                        entered,
                        await OwnWordsAsync(subject, cancellationToken).ConfigureAwait(false),
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Withheld<PasswordVerification>(error, ref refusal));

            if (refusal is not null)
            {
                return Result.Failure<bool>(refusal);
            }

            open.Accepted(Factor.Password);

            return Result.Success(verified.ChangeRequired);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entered);
        }
    }

    // AUTH-FACT-014 AC3: the ceremony is judged in this unit of work, so the record of a
    // counter that did not advance commits with the failed authentication's record and
    // the failure's counts, and nothing else. Any other refusal wrote nothing to keep
    // and is rolled back before it is counted.
    private async ValueTask<Result<bool>> CeremonyAsync(
        Challenge open,
        SubjectId subject,
        FactorPresentation presented,
        Func<CancellationToken, ValueTask<Error?>> counted,
        CancellationToken cancellationToken)
    {
        if (presented.Assertion is null)
        {
            return Result.Failure<bool>(
                await counted(cancellationToken).ConfigureAwait(false) ?? Error.From(ErrorCodes.FactorRejected));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<bool>(notBegun);
        }

        Error? refusal = null;

        Authenticator answered = (await webAuthn
                .AssertAsync(presented.Assertion, open.WebAuthn, subject, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<Authenticator>(error, ref refusal));

        // A credential of another account answering this challenge is refused as any
        // other wrong credential is: whose it is is not disclosed.
        if (refusal is null && answered.Subject != subject)
        {
            refusal = Error.From(ErrorCodes.FactorRejected);
        }

        if (refusal is not null)
        {
            return Result.Failure<bool>(
                await RefusedAsync(
                        refusal,
                        refusal.Code == ErrorCodes.WebAuthnCounterMismatch,
                        counted,
                        cancellationToken)
                    .ConfigureAwait(false));
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<bool>(notCommitted);
        }

        open.Accepted(answered.Factor);

        return Result.Success(false);
    }

    // AUTH-FACT-004: the try is decided in this unit of work, so a wrong try's count and
    // the invalidation at the cap commit with the refusal's record and the failure's
    // counts; a code gone or out of life changes nothing on its record and commits
    // those alone; and a right code's spend commits in it whether the sign-in then
    // goes on or a domain lock refuses it (CONV-DESIGN-003).
    private async ValueTask<Result<bool>> CodeAsync(
        Challenge open,
        SubjectId subject,
        FactorPresentation presented,
        Func<CancellationToken, ValueTask<Error?>> counted,
        CancellationToken cancellationToken)
    {
        PendingSignIn? held = await links
            .FindAsync(subject, presented.Factor, cancellationToken)
            .ConfigureAwait(false);

        // AUTH-FACT-002 AC4: a code issued for another sign-in or step-up is no code of
        // this one.
        if (held is null || !held.Answers(open.Fingerprint))
        {
            return Result.Failure<bool>(
                await counted(cancellationToken).ConfigureAwait(false) ?? Error.From(ErrorCodes.CodeExpired));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<bool>(notBegun);
        }

        Result spent = await links
            .SpendCodeAsync(held, presented.Value ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        if (spent.Match<Error?>(() => null, error => error) is Error refusal)
        {
            return Result.Failure<bool>(
                await RefusedAsync(refusal, Answered(refusal), counted, cancellationToken).ConfigureAwait(false));
        }

        // AUTH-FACT-004 AC7, AUTH-RECOV-007: a code is judged against the credential its
        // record names only once it is right, so only the right one learns that the
        // credential is gone or suspended: it is spent, and the refusal is a failed
        // attempt whose record and counts commit with the spend.
        if (await UnansweredAsync(held, cancellationToken).ConfigureAwait(false) is Error unanswered)
        {
            return Result.Failure<bool>(
                await RefusedAsync(unanswered, kept: true, counted, cancellationToken).ConfigureAwait(false));
        }

        // A right code is spent whatever follows, and the lock is judged only after it,
        // so that a wrong code learns nothing of the lock (REG-DOM-001).
        if (await links.LockedAsync(held, cancellationToken).ConfigureAwait(false) is Error locked)
        {
            return Result.Failure<bool>(await SpentAsync(locked, counted, cancellationToken).ConfigureAwait(false));
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<bool>(notCommitted);
        }

        open.Accepted(presented.Factor);

        return Result.Success(false);
    }

    // AUTH-FACT-004 AC7, AUTH-RECOV-007: what refuses a right code of a second step the
    // library texts, read from the one credential its record names from its issue,
    // whatever other credential of the factor the account holds. Where that credential
    // is suspended, by a loss report or by a removal that would lower reachable
    // assurance, the code answers that it is suspended; where it has been removed or
    // invalidated since, the code is refused as one sent to an address given up since
    // is. A code of any other entry names no credential.
    private async ValueTask<Error?> UnansweredAsync(PendingSignIn held, CancellationToken cancellationToken)
    {
        if (held.Credential is not AuthenticatorId named)
        {
            return null;
        }

        Authenticator? issuedFor = await authenticators.FindAsync(named, cancellationToken).ConfigureAwait(false);

        if (issuedFor is { IsUsable: true })
        {
            return null;
        }

        return Error.From(
            issuedFor is { IsAwaitingInvalidation: true }
                ? ErrorCodes.CredentialSuspended
                : ErrorCodes.FactorRejected);
    }

    // AUTH-FACT-004, CONV-DESIGN-003: ends the unit of work a right code was spent in
    // where the sign-in it would complete is then refused. A domain lock's refusal
    // commits the spend alone, the factor having succeeded, so no failure is counted
    // and no failed authentication recorded; an address given up since is a refused
    // factor, whose record and counts commit with the spend (REG-IDENT-006); and a
    // lock that could not be judged is rolled back.
    private async ValueTask<Error> SpentAsync(
        Error locked,
        Func<CancellationToken, ValueTask<Error?>> counted,
        CancellationToken cancellationToken)
    {
        if (locked.Code == ErrorCodes.FactorRejected)
        {
            return await RefusedAsync(locked, kept: true, counted, cancellationToken).ConfigureAwait(false);
        }

        if (locked.Code != ErrorCodes.IdentifierDomainNotAllowed)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return locked;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => locked, error => error);
    }

    // What a code answers a try with, as against a failure to judge it.
    private static bool Answered(Error refusal) =>
        refusal.Code == ErrorCodes.CodeInvalid || refusal.Code == ErrorCodes.CodeExpired;

    // AUTH-ABUSE-001, CONV-LOG-005: whether a failure is a factor's refusal of what was
    // presented, which is a failed attempt, as against a fault of the library's own (a
    // setting that does not read, the database failing), which is none: nothing is
    // counted or recorded for it and the request answers system.fault.
    internal static bool Refuses(Error failure) => Refusals.Contains(failure.Code);

    // CONV-DESIGN-003: ends the unit of work a refusal was decided in. A refusal that
    // keeps a write is counted inside it, the count joining it, and the whole is
    // committed together; any other is rolled back and then counted, as a refusal
    // that never began one is, save a fault of the library's own, which is rolled back
    // and counted nowhere. A count that fails is what the caller is told, with nothing
    // committed.
    private async ValueTask<Error> RefusedAsync(
        Error refusal,
        bool kept,
        Func<CancellationToken, ValueTask<Error?>> counted,
        CancellationToken cancellationToken)
    {
        if (!kept)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Refuses(refusal)
                ? await counted(cancellationToken).ConfigureAwait(false) ?? refusal
                : refusal;
        }

        if (await counted(cancellationToken).ConfigureAwait(false) is Error uncounted)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return uncounted;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => refusal, error => error);
    }

    private async ValueTask<IReadOnlyList<string>> OwnWordsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);
        HeldProfile profile = await accounts.ProfileAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var words = new List<string>(held.All.Count + 2);

        foreach (HeldIdentifier identifier in held.All)
        {
            words.Add(identifier.Canonical);
        }

        if (profile.DisplayName is { } display)
        {
            words.Add(display.Value);
        }

        if (profile.LegalName is { } legal)
        {
            words.Add(legal.Value);
        }

        return words;
    }

    private async ValueTask<Result<SignInOutcome>> SettleAsync(
        Challenge open,
        SubjectId subject,
        SessionOrigin origin,
        bool trustDevice,
        bool changeRequired,
        string? remembered,
        string? trusted,
        CancellationToken cancellationToken)
    {
        // REG-DOM-001: a member's sign-in email is in every lock the member is under,
        // which is told only once a factor has succeeded (AUTH-ABUSE-003).
        if (await LockedAsync(open, subject, cancellationToken).ConfigureAwait(false) is Error locked)
        {
            return Result.Failure<SignInOutcome>(locked);
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);
        bool password = await HasPasswordAsync(subject, cancellationToken).ConfigureAwait(false);

        Assurance reached = Assurance.Reached(Properties(open.Presented))
            ?? new Assurance(AssuranceLevel.Delegated, PhishingResistant: false);

        bool trusts = trusted is not null
            && await devices.TrustsAsync(subject, trusted, cancellationToken).ConfigureAwait(false);

        List<Factor> wanted = Wanted(policy, enrolled, reached, trusts);

        // AUTH-FACT-002b: where the carrier reports a recent change of SIM or of
        // network, the entries a text carries are withheld from this sign-in and the
        // account's other second steps are offered in their place.
        int offered = wanted.Count;

        List<Factor> textable = [.. wanted.Where(entry => FactorCatalogue.Of(entry).Restricted)];

        foreach (Factor carried in textable)
        {
            if (!await TextableAsync(subject, carried, cancellationToken).ConfigureAwait(false))
            {
                _ = wanted.Remove(carried);
            }
        }

        // Withholding the only second step cannot let the sign-in through below the
        // level the account's own asked for, so a challenge left with nothing to
        // present is refused rather than completed.
        if (offered > 0 && wanted.Count is 0)
        {
            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.FactorRejected));
        }

        if (wanted.Count > 0)
        {
            return Result.Success(new SignInOutcome(
                new SignInProgress(
                    SignInStatus.FactorRequired,
                    reached.Level,
                    reached.PhishingResistant,
                    wanted,
                    TrustDeviceOffered: false,
                    Session: null,
                    Requirement: null,
                    changeRequired),
                Session: null,
                Remembered: null,
                Trusted: null));
        }

        bool checks = (await devices
                .ChecksAsync(
                    subject,
                    StepUp.Reachable(HeldFactors.Of(enrolled, password).Standing),
                    reached,
                    remembered,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        return checks
            ? await HoldAsync(open, subject, reached, origin.Source, changeRequired, cancellationToken)
                .ConfigureAwait(false)
            : await CompleteAsync(
                    open,
                    subject,
                    origin,
                    trustDevice,
                    changeRequired,
                    remembered is null ? null : OpaqueToken.Of(remembered),
                    trusted,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    private async ValueTask<Error?> LockedAsync(
        Challenge open,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (open.Email is not IdentifierId email)
        {
            return null;
        }

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // REG-IDENT-006 AC6: an address the account gave up while the sign-in was open
        // signs nothing in.
        return held.Find(email) is HeldIdentifier opened
            ? await domainLock.RefusedAsync(subject, opened.Canonical, cancellationToken).ConfigureAwait(false)
            : Error.From(ErrorCodes.FactorRejected);
    }

    // REG-IDENT-006 AC6: whether the sign-in was opened with an address the account
    // has given up since, which no factor presented to it signs in with.
    private async ValueTask<bool> GivenUpAsync(Challenge open, SubjectId subject, CancellationToken cancellationToken) =>
        open.Email is IdentifierId email
        && (await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false)).Find(email) is null;

    // AUTH-FACT-002b: the entry rides the number the account would be texted at, so
    // it is that number the signal is asked about.
    private async ValueTask<bool> TextableAsync(
        SubjectId subject,
        Factor carried,
        CancellationToken cancellationToken) =>
        await signals
            .AllowsAsync(
                carried,
                await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false),
                subject,
                cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<bool> HasPasswordAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await passwordStore.FindAsync(subject, cancellationToken).ConfigureAwait(false) is not null;

    // The account's own second step raises what a sign-in has to reach above the
    // policy's floor: enrolling one is asking to be asked (AUTH-FACT-002b). A trusted
    // browser stands in for that step and for nothing else, never for the floor the
    // policy itself states (AUTH-FACT-015).
    private static List<Factor> Wanted(
        Policy policy,
        IReadOnlyList<Authenticator> enrolled,
        Assurance reached,
        bool trusts)
    {
        Authenticator? preferred = SecondStep.Preferred(enrolled);
        AssuranceLevel required =
            preferred is not null && policy.RequiredAssurance < AssuranceLevel.Aal2
                ? AssuranceLevel.Aal2
                : policy.RequiredAssurance;

        if (reached.Level >= required || (trusts && reached.Level >= policy.RequiredAssurance))
        {
            return [];
        }

        List<Factor> offered = [.. enrolled
            .Where(credential => credential.IsUsable && SecondStep.Is(credential.Factor))
            .Select(credential => credential.Factor)
            .Distinct()
            .Order()];

        // The account's preferred method is offered first and the others from it
        // (IDN-ATTR-008, AUTH-FACT-002b AC4).
        if (preferred is not null && offered.Remove(preferred.Factor))
        {
            offered.Insert(0, preferred.Factor);
        }

        return offered;
    }

    private async ValueTask<Result<SignInOutcome>> HoldAsync(
        Challenge open,
        SubjectId subject,
        Assurance reached,
        string source,
        bool changeRequired,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);
        HeldIdentifier? primary = held.OfKind(IdentifierKind.Email)
            .FirstOrDefault(identifier => identifier.IsPrimary);

        if (primary is null || !EmailAddress.TryParse(primary.Canonical, out EmailAddress address))
        {
            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.FactorRejected));
        }

        // IDN-ATTR-001: the step carries no locale of the request, so the code goes out
        // in the account's language, or in every declared one where it holds none.
        string? settled = await identifiers.LanguageAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        // AUTH-ABUSE-004: the code is issued and its message undertaken in one unit of
        // work, so a send the restrictions refuse leaves no code behind it.
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<SignInOutcome>(notBegun);
        }

        string code = (await codes.IssueAsync(open.Fingerprint, cancellationToken)
                .ConfigureAwait(false))
            .Match(drawn => drawn, error => Withheld<string>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<SignInOutcome>(failure);
        }

        _ = (await sending
                .UndertakeAsync(
                    new OutboundMessage(
                        SendDestination.Of(address),
                        MessageKind.VerificationCode,
                        RestrictionPurpose.Verification,
                        source,
                        RecipientLanguage.Of(settled, requested: null, languages))
                    {
                        Subject = subject,
                        Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                        {
                            ["code"] = code,
                        },
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(_ => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<SignInOutcome>(failure);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<SignInOutcome>(notCommitted);
        }

        return Result.Success(new SignInOutcome(
            new SignInProgress(
                SignInStatus.DeviceVerificationRequired,
                reached.Level,
                reached.PhishingResistant,
                [],
                TrustDeviceOffered: false,
                Session: null,
                Requirement: null,
                changeRequired),
            Session: null,
            Remembered: null,
            Trusted: null));
    }

    private async ValueTask<Result<SignInOutcome>> CompleteAsync(
        Challenge open,
        SubjectId subject,
        SessionOrigin origin,
        bool trustDevice,
        bool changeRequired,
        OpaqueToken? remembered,
        string? trusted,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<SignInOutcome>(notBegun);
        }

        // D-166 X3: the challenge is held under its lock from before the session is
        // issued until it is removed, so a second completion of it waits for this one
        // and is refused as a handle that opens nothing.
        if (await challenges.FindForUpdateAsync(open.Fingerprint, cancellationToken).ConfigureAwait(false) is null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.FactorRejected));
        }

        Result<SignInOutcome> completed = await IssueAsync(
                subject,
                open.Presented,
                origin,
                trustDevice,
                changeRequired,
                remembered,
                trusted,
                cancellationToken)
            .ConfigureAwait(false);

        if (completed.Match(_ => false, _ => true))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return completed;
        }

        await challenges.RemoveAsync(open.Fingerprint, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<SignInOutcome>(notCommitted);
        }

        return completed;
    }

    private async ValueTask<Result<SignInOutcome>> IssueAsync(
        SubjectId subject,
        IReadOnlyCollection<Factor> presented,
        SessionOrigin origin,
        bool trustDevice,
        bool changeRequired,
        OpaqueToken? remembered,
        string? trusted,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        PolicyHold? hold = await HeldAsync(subject, policy, cancellationToken).ConfigureAwait(false);

        // After the run-up the sign-in stops at enrolment and no session is issued
        // (AUTH-FACT-017).
        if (hold is { Expired: true })
        {
            return Result.Failure<SignInOutcome>(Error.From(
                ErrorCodes.PolicyGraceExpired,
                "outcome",
                JsonSerializer.SerializeToElement("enrol")));
        }

        // Inside the run-up the sign-in is told the requirement and continues, so the
        // raised floor does not refuse it (AUTH-FACT-017).
        Result<IssuedSession> begun = hold is null
            ? await sessions
                .BeginAsync(subject, presented, origin, cancellationToken)
                .ConfigureAwait(false)
            : await sessions
                .BeginDuringGraceAsync(subject, presented, origin, cancellationToken)
                .ConfigureAwait(false);
        IssuedSession issued = begun
            .Match(value => value, error => Withheld<IssuedSession>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        Assurance reached = Assurance.Reached(Properties(presented))
            ?? new Assurance(AssuranceLevel.Aal1, PhishingResistant: false);

        // IDN-ACCT-007: trusting a device writes to the account, so a restricted
        // account's sign-in is offered none and records none.
        bool offered = DeviceService.MayTrust(
                policy,
                reached,
                await MeetsSingleFactorFloorAsync(subject, cancellationToken).ConfigureAwait(false))
            && await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false)
                is not AccountState.Restricted;

        OpaqueToken? trust = null;

        if (offered && trustDevice)
        {
            trust = (await devices.TrustAsync(subject, origin.Device, cancellationToken)
                    .ConfigureAwait(false))
                .Match(token => (OpaqueToken?)token, error => Withheld<OpaqueToken?>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<SignInOutcome>(failure);
            }
        }

        return Result.Success(new SignInOutcome(
            new SignInProgress(
                SignInStatus.Complete,
                reached.Level,
                reached.PhishingResistant,
                [],
                offered,
                issued.Id,
                hold?.Requirement,
                changeRequired),
            issued,
            remembered,
            trust ?? (trusted is null ? null : OpaqueToken.Of(trusted))));
    }

    private async ValueTask<bool> MeetsSingleFactorFloorAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Password? held = await passwordStore.FindAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return held is not null && held.MeetsSingleFactorFloor;
    }

    // What the account has yet to comply with, and when its run-up ends. A raise the
    // account already meets holds nothing (AUTH-FACT-017).
    private async ValueTask<PolicyHold?> HeldAsync(
        SubjectId subject,
        Policy policy,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PolicyRaise> raised = await policies
            .RaisesAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (raised.Count is 0)
        {
            return null;
        }

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.PolicyEnforcementGrace, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);
        var factors = HeldFactors.Of(
            enrolled,
            await HasPasswordAsync(subject, cancellationToken).ConfigureAwait(false));
        DateTimeOffset registered = await accounts
            .CreatedAtAsync(subject, cancellationToken)
            .ConfigureAwait(false) ?? DateTimeOffset.MinValue;
        DateTimeOffset now = time.GetUtcNow();

        // Where two raises stand over one account, the one whose run-up ends first is
        // the one the sign-in is told about and the one that stops it.
        return raised
            .Select(raise => PolicyGrace.On(
                raise,
                grace,
                raise.Field is PolicyField.RequiredAssurance
                    ? StepUp.Reachable(factors.Standing).Level >= policy.RequiredAssurance
                    : Redundancy.Satisfied(enrolled),
                registered,
                now))
            .OfType<PolicyHold>()
            .OrderBy(held => held.Requirement.Deadline)
            .FirstOrDefault();
    }

    // A failure to count an attempt is a failure of the gate itself, so it is what the
    // caller is told rather than the refusal it was counting (AUTH-ABUSE-001). The
    // refusal is written to the trail first, against the account the challenge
    // resolved to or none, and never with the identifier as typed (CONV-LOG-005); an
    // identifier that resolved to nothing reaches this point as one that resolved to
    // an account does, so the record costs the one what it costs the other
    // (AUTH-ABUSE-003).
    // The refused value is a factor, or, where none is named, the new-device check's
    // code, which is recorded as that verification (CONV-LOG-005).
    // The record, the delay's counts and a trusted device's failure are the refusal's
    // kept writes and commit together, in this unit of work or in the one it joins,
    // and none of them stands where one cannot be written (CONV-DESIGN-003).
    private async ValueTask<Error?> CountedAsync(
        ThrottleAttempt attempt,
        Factor? presented,
        SubjectId? subject,
        string? trusted,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return notBegun;
        }

        if (presented is Factor factor)
        {
            await audit.FailedAsync(attempt.Account, factor, time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await audit.DeviceVerificationFailedAsync(attempt.Account, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false);
        }

        Error? failure = (await throttle.FailedAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);

        if (failure is null && subject is SubjectId account)
        {
            failure = (await devices.FailedAsync(account, trusted, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error);
        }

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return failure;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
    }

    // CONV-LOG-005, AUTH-ABUSE-001: a pressed link token that opens nothing, unknown or
    // expired, is held to the delay its source has earned; outside it the press is a
    // refused factor against no account, recorded under the factor the request named and
    // counted against the source.
    private async ValueTask<Error> GoneAsync(
        ThrottleAttempt attempt,
        Factor factor,
        CancellationToken cancellationToken) =>
        await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false)
            ?? await CountedAsync(attempt, factor, null, null, cancellationToken).ConfigureAwait(false)
            ?? Error.From(ErrorCodes.CodeExpired);

    // CONV-LOG-005: a factor refused at a step-up is written to the trail against the
    // session it was presented on, whatever the log level, and is counted against the
    // delay as a factor refused at sign-in is (AUTH-ABUSE-001), the record and the
    // counts committed together (CONV-DESIGN-003).
    private async ValueTask<Error?> StepUpRefusedAsync(
        ThrottleAttempt attempt,
        Session session,
        Factor presented,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return notBegun;
        }

        await audit
            .StepUpFailedAsync(
                session.Id,
                session.Subject,
                session.BreakGlassReason,
                presented,
                time.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await throttle.FailedAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error failure)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return failure;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
    }
}
