using System;
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
/// <param name="policies">What policy governs the account, and what it has raised.</param>
/// <param name="throttle">The progressive delay.</param>
/// <param name="sending">Where a message goes out.</param>
/// <param name="configuration">Where the lifetimes and the limits come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a handle and a code are drawn from.</param>
/// <remarks>
/// Implements LIB-API-005, AUTH-FACT-001 to AUTH-FACT-004, AUTH-FACT-015 to
/// AUTH-FACT-017, AUTH-STEP-001 and AUTH-ABUSE-001 to AUTH-ABUSE-003. An identifier
/// that resolves to nothing is carried through every step exactly as one that
/// resolves to an account, so that nothing in the shape of an answer tells the two
/// apart.
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
    PolicyResolution policies,
    ThrottleService throttle,
    SendingService sending,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : IAuthentication
{
    /// <inheritdoc/>
    public async ValueTask<Result<SignInChallenge>> BeginAsync(
        string identifier,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        Error? failure = null;

        SubjectId? subject = await OwnerAsync(identifier, cancellationToken).ConfigureAwait(false);

        if (await DelayedAsync(source, identifier, subject, cancellationToken).ConfigureAwait(false)
            is Error held)
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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await challenges
            .AddAsync(
                Challenge.Open(handle, subject, ceremony.Value, time.GetUtcNow(), lifetime),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
        SessionLocation? location,
        string source,
        CancellationToken cancellationToken) =>
        (await PresentAsync(
                challenge,
                presented,
                new SessionOrigin(source, device, location),
                remembered: null,
                trusted: null,
                cancellationToken)
            .ConfigureAwait(false))
        .Match(outcome => Result.Success(outcome.Progress), Result.Failure<SignInProgress>);

    /// <inheritdoc/>
    public async ValueTask<Result<SignInProgress>> VerifyDeviceAsync(
        string challenge,
        string code,
        DeviceDescription device,
        SessionLocation? location,
        string source,
        CancellationToken cancellationToken) =>
        (await VerifyDeviceAsync(
                challenge,
                code,
                new SessionOrigin(source, device, location),
                cancellationToken)
            .ConfigureAwait(false))
        .Match(outcome => Result.Success(outcome.Progress), Result.Failure<SignInProgress>);

    /// <inheritdoc/>
    public async ValueTask<Result<SignInProgress>> StepUpAsync(
        AccessContext context,
        SessionId session,
        string challenge,
        FactorPresentation presented,
        CancellationToken cancellationToken) =>
        (await RaiseAsync(context, session, challenge, presented, cancellationToken)
            .ConfigureAwait(false))
        .Match(outcome => Result.Success(outcome.Progress), Result.Failure<SignInProgress>);

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
        string linkToken,
        bool press,
        DeviceDescription device,
        SessionLocation? location,
        string source,
        CancellationToken cancellationToken) =>
        (await LandAsync(
                challenge,
                browser,
                linkToken,
                press,
                new SessionOrigin(source, device, location),
                remembered: null,
                cancellationToken)
            .ConfigureAwait(false))
        .Match(
            landed => Result.Success(
                new SignInLanding(landed.Outcome?.Progress, landed.SameBrowser, landed.Code)),
            Result.Failure<SignInLanding>);

    /// <inheritdoc/>
    public ValueTask<Result> AbandonLinkAsync(string linkToken, CancellationToken cancellationToken) =>
        links.AbandonAsync(linkToken, cancellationToken);

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

        if (open is null)
        {
            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.FactorRejected));
        }

        if (await DelayedAsync(origin.Address, identifier: null, open.Subject, cancellationToken)
            .ConfigureAwait(false) is Error held)
        {
            return Result.Failure<SignInOutcome>(held);
        }

        var attempt = new ThrottleAttempt(origin.Address, null)
        {
            Account = open.Subject,
            Recognised = remembered is not null || trusted is not null,
        };

        // An identifier that resolved to nothing reaches exactly this point and stops,
        // having been told what an account with the wrong password is told
        // (AUTH-ABUSE-003).
        if (open.Subject is not SubjectId subject
            || await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false)
                is not AccountState.Active)
        {
            return Result.Failure<SignInOutcome>(
                await CountedAsync(attempt, null, null, cancellationToken).ConfigureAwait(false)
                ?? Error.From(ErrorCodes.FactorRejected));
        }

        Error? refusal = null;

        bool changeRequired = (await AcceptsAsync(open, subject, presented, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<SignInOutcome>(
                await CountedAsync(attempt, subject, trusted, cancellationToken).ConfigureAwait(false)
                ?? refusal);
        }

        await throttle.SucceededAsync(attempt, cancellationToken).ConfigureAwait(false);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await challenges.RecordAsync(open, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
        string code,
        SessionOrigin origin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(origin);

        Challenge? open = await OpenAsync(challenge, cancellationToken).ConfigureAwait(false);

        if (open?.Subject is not SubjectId subject || open.DeviceCode is null)
        {
            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.CodeExpired));
        }

        Error? failure = null;

        int attempts = (await configuration
                .ReadAsync(Settings.CodeVerificationAttempts, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        if (!VerificationCode.Matches(open.DeviceCode, code))
        {
            open.Missed();

            // Enough wrong codes end the code, and the sign-in with it: a correct one
            // afterwards is refused too (AUTH-FACT-004).
            if (open.DeviceAttempts >= attempts)
            {
                open.Spent();
            }

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);
            await challenges.RecordAsync(open, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Failure<SignInOutcome>(
                Error.From(open.IsHeld ? ErrorCodes.CodeInvalid : ErrorCodes.CodeExpired));
        }

        open.Spent();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await challenges.RecordAsync(open, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the session now reaches, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<SignInOutcome>> RaiseAsync(
        AccessContext context,
        SessionId session,
        string challenge,
        FactorPresentation presented,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(presented);

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

        // A step-up is a factor presented against a challenge exactly as a sign-in is,
        // so that a ceremony has a server-issued value to sign over; the challenge is
        // the asking principal's own or it is nobody's (AUTH-STEP-001).
        Challenge? open = await OpenAsync(challenge, cancellationToken).ConfigureAwait(false);

        if (open is null || open.Subject != asking)
        {
            return Result.Failure<SignInOutcome>(Error.From(ErrorCodes.FactorRejected));
        }

        Error? refusal = null;

        _ = (await AcceptsAsync(open, asking, presented, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<SignInOutcome>(refusal);
        }

        Error? failure = null;

        IssuedSession raised = (await sessions
                .PresentAsync(live, open.Presented, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IssuedSession>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await challenges.RemoveAsync(open.Fingerprint, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new SignInOutcome(
            new SignInProgress(
                SignInStatus.Complete,
                live.Attained,
                live.PhishingResistant,
                [],
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
        string linkToken,
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
            return Result.Failure<LandedSignIn>(Error.From(ErrorCodes.CodeExpired));
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

        if (open is null || open.Subject != held.Subject)
        {
            return Result.Failure<LandedSignIn>(Error.From(ErrorCodes.FactorRejected));
        }

        open.Accepted(held.Factor);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await challenges.RecordAsync(open, cancellationToken).ConfigureAwait(false);
        await links.SpendAsync(held, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

    private async ValueTask<SubjectId?> OwnerAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        bool usernames = (await configuration
                .ReadAsync(Settings.IdentifiersUsernameEnabled, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        string entered = identifier.Trim();

        if (failure is not null || IdentifierKinds.Detect(entered, usernames) is not { } kind)
        {
            return null;
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

        return canonical is null
            ? null
            : await identifiers.OwnerAsync(kind, canonical, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Error?> DelayedAsync(
        string source,
        string? identifier,
        SubjectId? subject,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan delay = (await throttle
                .DelayAsync(
                    new ThrottleAttempt(source, identifier) { Account = subject },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        return delay > TimeSpan.Zero
            ? Error.From(
                ErrorCodes.Throttled,
                "retryAt",
                JsonSerializer.SerializeToElement(time.GetUtcNow() + delay))
            : null;
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
    private async ValueTask<Result<bool>> AcceptsAsync(
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

        if (properties.IsWebAuthn)
        {
            return await CeremonyAsync(open, subject, presented, cancellationToken)
                .ConfigureAwait(false);
        }

        if (FactorCatalogue.Delivered.Contains(presented.Factor))
        {
            return await CodeAsync(open, subject, presented, cancellationToken)
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

    private async ValueTask<Result<bool>> CeremonyAsync(
        Challenge open,
        SubjectId subject,
        FactorPresentation presented,
        CancellationToken cancellationToken)
    {
        if (presented.Assertion is null)
        {
            return Result.Failure<bool>(Error.From(ErrorCodes.FactorRejected));
        }

        Error? refusal = null;

        Authenticator answered = (await webAuthn
                .AssertAsync(presented.Assertion, open.WebAuthn, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<Authenticator>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<bool>(refusal);
        }

        // A credential of another account answering this challenge is refused as any
        // other wrong credential is: whose it is is not disclosed.
        if (answered.Subject != subject)
        {
            return Result.Failure<bool>(Error.From(ErrorCodes.FactorRejected));
        }

        open.Accepted(answered.Factor);

        return Result.Success(false);
    }

    private async ValueTask<Result<bool>> CodeAsync(
        Challenge open,
        SubjectId subject,
        FactorPresentation presented,
        CancellationToken cancellationToken)
    {
        PendingSignIn? held = await links
            .FindAsync(subject, presented.Factor, cancellationToken)
            .ConfigureAwait(false);

        if (held is null)
        {
            return Result.Failure<bool>(Error.From(ErrorCodes.CodeExpired));
        }

        Error? refusal = null;

        _ = (await links
                .SpendCodeAsync(held, presented.Value ?? string.Empty, cancellationToken)
                .ConfigureAwait(false))
            .Match(() => true, error => Withheld<bool>(error, ref refusal));

        if (refusal is not null)
        {
            return Result.Failure<bool>(refusal);
        }

        open.Accepted(presented.Factor);

        return Result.Success(false);
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
            ? await HoldAsync(open, subject, reached, changeRequired, cancellationToken)
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

        string language = await identifiers.LanguageAsync(subject, cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;
        string code = VerificationCode.Draw(randomness);

        _ = (await sending
                .SendAsync(
                    new SendRequest(
                        SendDestination.Of(address),
                        MessageKind.VerificationCode,
                        RestrictionPurpose.Verification,
                        primary.Canonical,
                        language)
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
            return Result.Failure<SignInOutcome>(failure);
        }

        open.Holding(VerificationCode.Held(code));

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await challenges.RecordAsync(open, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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
                .BeginAsync(subject, open.Presented, origin, cancellationToken)
                .ConfigureAwait(false)
            : await sessions
                .BeginDuringGraceAsync(subject, open.Presented, origin, cancellationToken)
                .ConfigureAwait(false);
        IssuedSession issued = begun
            .Match(value => value, error => Withheld<IssuedSession>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SignInOutcome>(failure);
        }

        Assurance reached = Assurance.Reached(Properties(open.Presented))
            ?? new Assurance(AssuranceLevel.Aal1, PhishingResistant: false);
        bool offered = DeviceService.MayTrust(
            policy,
            reached,
            await MeetsSingleFactorFloorAsync(subject, cancellationToken).ConfigureAwait(false));

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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await challenges.RemoveAsync(open.Fingerprint, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

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

        Error? failure = null;

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.PolicyEnforcementGrace, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return null;
        }

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
                    : Redundant(enrolled),
                registered,
                now))
            .OfType<PolicyHold>()
            .OrderBy(held => held.Requirement.Deadline)
            .FirstOrDefault();
    }

    // A failure to count an attempt is a failure of the gate itself, so it is what the
    // caller is told rather than the refusal it was counting (AUTH-ABUSE-001).
    private async ValueTask<Error?> CountedAsync(
        ThrottleAttempt attempt,
        SubjectId? subject,
        string? trusted,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        _ = (await throttle.FailedAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(() => true, error => Withheld<bool>(error, ref failure));

        if (subject is not SubjectId account)
        {
            return failure;
        }

        Error? revoked = null;

        _ = (await devices.FailedAsync(account, trusted, cancellationToken).ConfigureAwait(false))
            .Match(() => true, error => Withheld<bool>(error, ref revoked));

        return failure ?? revoked;
    }

    // A synced passkey satisfies redundancy on its own, because it survives the
    // device; a device-bound credential is gone with the device, so a second one
    // stands behind it (AUTH-RECOV-001).
    private static bool Redundant(IReadOnlyList<Authenticator> enrolled) =>
        enrolled.Count(credential => credential.IsUsable) > 1
        || enrolled.Any(credential => credential.IsUsable && credential.WebAuthn?.BackupState is true);
}
