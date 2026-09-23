using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Oidc;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Registration;

/// <summary>
/// The steps of registration over one staged session: nothing is written outside the
/// session store until the terms step, and the terms step writes everything in one
/// transaction or nothing at all.
/// </summary>
/// <param name="sessions">Where registration sessions are held.</param>
/// <param name="directory">Where an account is looked up and created.</param>
/// <param name="sending">What carries the codes, the links and the notices.</param>
/// <param name="notices">What remembers which addresses have been told.</param>
/// <param name="passwords">What screens and hashes a password.</param>
/// <param name="passwordStore">Where the account's password is written.</param>
/// <param name="recoveryCodes">What draws a set of recovery codes.</param>
/// <param name="recoveryCodeStore">Where the account's set is written.</param>
/// <param name="authenticators">Where the account's credentials are written.</param>
/// <param name="clients">The registry the originating client is resolved against.</param>
/// <param name="policies">Where the policy in force is resolved.</param>
/// <param name="issuing">What issues the session the person is signed in on.</param>
/// <param name="devices">What remembers the registering browser.</param>
/// <param name="capture">Where the consent controls the person ticked are recorded.</param>
/// <param name="configuration">Where the registration settings are read.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="events">Where the emitted events go.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the codes and the tokens are drawn from.</param>
/// <remarks>
/// Implements REG-SESS-001 to REG-SESS-008, REG-PROF-002, REG-IDENT-010,
/// API-REDIR-002, AUTH-FACT-004 and AUTH-ABUSE-003. Every answer is the same whether or not the
/// identifier presented belongs to an account already: the lookup decides only
/// whether a code goes out and whether the holder is told.
/// </remarks>
internal sealed class RegistrationService(
    IRegistrationSessionStore sessions,
    IRegistrationDirectory directory,
    INotificationHandler sending,
    INoticeLedger notices,
    PasswordService passwords,
    IPasswordStore passwordStore,
    RecoveryCodeService recoveryCodes,
    IRecoveryCodeStore recoveryCodeStore,
    IAuthenticatorStore authenticators,
    IOidcClientStore clients,
    PolicyResolution policies,
    SessionService issuing,
    DeviceService devices,
    IConsents capture,
    IConfigurationStore configuration,
    IUnitOfWork work,
    IEvents events,
    TimeProvider time,
    RandomNumberGenerator randomness) : IRegistration
{
    private const int Majority = 18;

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationSessionId>> BeginAsync(
        string client,
        string language,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.RegistrationSessionLifetime, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationSessionId>(failure);
        }

        // API-REDIR-002 AC1 and AC2: the identifier is resolved where it is captured,
        // and one the registry does not hold is stored as the configured default
        // rather than refused, so by the last step there is nothing left to validate.
        OidcClient? originating =
            await clients.FindAsync(client, cancellationToken).ConfigureAwait(false);

        var session = RegistrationSession.Open(
            RegistrationSessionId.New(time),
            SubjectId.New(randomness),
            originating?.ClientId
                ?? await DefaultClientAsync(cancellationToken).ConfigureAwait(false),
            language,
            source,
            time.GetUtcNow(),
            lifetime);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(session.Id);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> StateAsync(
        RegistrationSessionId session,
        CancellationToken cancellationToken)
    {
        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        return live is null ? Gone() : Result.Success(State(live));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> RecordAgeAsync(
        RegistrationSessionId session,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken)
    {
        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        // The screen locks once it has refused a date, so that a person cannot walk
        // the date forward until it passes (REG-PROF-002).
        if (live.AgeRefused)
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.ProfileUnderage));
        }

        if (live.Step is not RegistrationStep.Age)
        {
            return OutOfStep();
        }

        Error? failure = null;

        AttributeRequirement affirmation = (await configuration
                .ReadAsync(Settings.RegistrationAdultAffirmation, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AttributeRequirement>(error, ref failure));

        AttributeRequirement retention = (await configuration
                .ReadAsync(Settings.ProfileDateOfBirth, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AttributeRequirement>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationState>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        bool adult = IsAdult(dateOfBirth, DateOnly.FromDateTime(now.UtcDateTime));

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (affirmation is not AttributeRequirement.Off && !adult)
        {
            live.RefuseAge(now);

            await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.ProfileUnderage));
        }

        live.AnswerAge(
            affirmation is AttributeRequirement.Off ? null : adult,
            retention is AttributeRequirement.Off ? null : dateOfBirth,
            affirmation is AttributeRequirement.Off ? Band(adult) : null,
            now);

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(State(live));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> StageAsync(
        RegistrationSessionId session,
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        // No identifier field is reached before the age screen is answered
        // (REG-PROF-002 AC1).
        if (!live.AgeAnswered)
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.AffirmationRequired));
        }

        RegistrationStep collecting =
            kind is IdentifierKind.Email ? RegistrationStep.Email : RegistrationStep.Phone;

        if (kind is IdentifierKind.Username || live.Step != collecting)
        {
            return OutOfStep();
        }

        return await CollectAsync(live, kind, value, isExtra: false, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> SkipPhoneAsync(
        RegistrationSessionId session,
        CancellationToken cancellationToken)
    {
        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        if (live.Step is not RegistrationStep.Phone)
        {
            return OutOfStep();
        }

        Error? failure = null;

        AttributeRequirement phone = (await configuration
                .ReadAsync(Settings.RegistrationPhone, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AttributeRequirement>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationState>(failure);
        }

        if (phone is AttributeRequirement.Required)
        {
            return OutOfStep();
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        live.SkipPhone();

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(State(live));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> AddAsync(
        RegistrationSessionId session,
        IdentifierKind kind,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        if (live.Step is not RegistrationStep.Confirm || kind is IdentifierKind.Username)
        {
            return OutOfStep();
        }

        Error? failure = null;

        int maximum = (await configuration
                .ReadAsync(Maximum(kind), cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationState>(failure);
        }

        if (live.Count(kind) >= maximum)
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierMaximum));
        }

        return await CollectAsync(live, kind, value, isExtra: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> ChangeAsync(
        RegistrationSessionId session,
        IdentifierId identifier,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        if (live.Identity(identifier) is not StagedIdentity staged)
        {
            return OutOfStep();
        }

        if (staged.IsLocked)
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierLocked));
        }

        if (Canonical(staged.Kind, value) is not (string entered, string canonical))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(canonical))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierMixedScript));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        staged.Change(entered, canonical);

        if (await DispatchAsync(live, staged, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure<RegistrationState>(refused);
        }

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(State(live));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> DiscardAsync(
        RegistrationSessionId session,
        IdentifierId identifier,
        CancellationToken cancellationToken)
    {
        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        if (live.Identity(identifier) is not StagedIdentity staged)
        {
            return OutOfStep();
        }

        // Only an extra identifier is discardable: the ones the steps collected are
        // the minimum the deployment asks for (REG-SESS-004 AC1).
        if (!staged.IsExtra || staged.IsVerified)
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierLastOfKind));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        live.Discard(staged);

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(State(live));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> VerifyAsync(
        RegistrationSessionId session,
        IdentifierId identifier,
        string code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        if (live.Identity(identifier) is not StagedIdentity staged)
        {
            return OutOfStep();
        }

        Error? failure = null;

        int cap = (await configuration
                .ReadAsync(Settings.CodeVerificationAttempts, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationState>(failure);
        }

        // A code invalidated by wrong tries refuses the right one too (REG-SESS-003
        // AC3), which is the answer a code that was never outstanding also gets.
        if (staged.IsVerified || staged.Code is not byte[] held || staged.CodeSpent)
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.CodeInvalid));
        }

        DateTimeOffset now = time.GetUtcNow();

        if (staged.CodeExpiresAt <= now)
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.CodeExpired));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (!VerificationCode.Matches(held, code))
        {
            staged.Missed(cap);

            await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.CodeInvalid));
        }

        staged.Verify(now);

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(State(live));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<LinkLanding>> LandAsync(
        RegistrationSessionId? session,
        string linkToken,
        bool press,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);

        byte[] fingerprint = OpaqueToken.Of(linkToken).Fingerprint();

        RegistrationSession? sender =
            await sessions.FindByLinkAsync(fingerprint, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();

        if (sender is null
            || sender.HasExpired(now)
            || Sent(sender, fingerprint) is not StagedIdentity staged)
        {
            return Result.Failure<LinkLanding>(Error.From(ErrorCodes.CodeInvalid));
        }

        // The press completes the verification only from the browser that started the
        // flow; anywhere else the page shows the code and changes nothing, which is
        // what defeats a mail scanner's prefetch (REG-SESS-003, BFF-CSRF-005b).
        bool sameBrowser = session == sender.Id;

        if (!press || !sameBrowser)
        {
            return Result.Success(new LinkLanding(
                Verified: false,
                sameBrowser,
                sameBrowser || staged.Code is null ? null : VerificationCode.Read(staged.Code)));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        staged.Verify(now);

        await sessions.RecordAsync(sender, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new LinkLanding(Verified: true, SameBrowser: true, Code: null));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> ConfirmAsync(
        RegistrationSessionId session,
        CancellationToken cancellationToken)
    {
        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        if (live.Step is not RegistrationStep.Confirm || !live.EveryIdentifierVerified())
        {
            return OutOfStep();
        }

        Error? failure = null;

        AttributeRequirement phone = (await configuration
                .ReadAsync(Settings.RegistrationPhone, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<AttributeRequirement>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationState>(failure);
        }

        bool reachable = live.HasVerified(IdentifierKind.Email)
            && (phone is not AttributeRequirement.Required || live.HasVerified(IdentifierKind.Phone));

        if (!reachable)
        {
            return OutOfStep();
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        live.Reached(RegistrationStep.Security);

        // The security step is settled on arrival too: where the policy admits a
        // factor that rides a verified identifier, there is nothing left to collect
        // on that screen (REG-SESS-006 AC2).
        return await SettledAsync(live, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> SetPasswordAsync(
        RegistrationSessionId session,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        if (live.Step is not (RegistrationStep.Security or RegistrationStep.Terms))
        {
            return OutOfStep();
        }

        byte[] presented = Encoding.UTF8.GetBytes(password);

        try
        {
            Error? failure = null;

            // The lower floor applies because the session cannot complete with a
            // password that stands alone below the single-factor one: the second step
            // is the price of the shorter password, and the security step, not the
            // floor, is what collects it (AUTH-PASS-001a, REG-SESS-006).
            PreparedPassword prepared = (await passwords
                    .PrepareAsync(presented, OwnWords(live), AssuranceLevel.Aal2, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Held<PreparedPassword>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<RegistrationState>(failure);
            }

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            live.SetPassword(prepared.Hash, prepared.StandsAlone);

            return await SettledAsync(live, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presented);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationCompleted>> AcceptTermsAsync(
        RegistrationSessionId session,
        string termsVersion,
        string noticeVersion,
        IReadOnlyDictionary<string, bool> consents,
        DeviceDescription device,
        CancellationToken cancellationToken)
    {
        // API-REDIR-002 AC4: where the person is returned is read from what the
        // session stored at step 1, so it is resolved before the step that ends the
        // session and never from anything this request carries.
        string landing = await LandingAsync(session, cancellationToken).ConfigureAwait(false);

        return (await CompleteAsync(
                    session,
                    termsVersion,
                    noticeVersion,
                    consents,
                    device,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(
                outcome => Result.Success(
                    new RegistrationCompleted(outcome.Subject, outcome.Session.Id, landing)),
                Result.Failure<RegistrationCompleted>);
    }

    // API-REDIR-001: the one destination the library falls back to is a client the
    // deployment named, which startup has already read against the registry, so a
    // deployment that named none stores nothing and the frontend decides.
    private async ValueTask<string> DefaultClientAsync(CancellationToken cancellationToken) =>
        (await configuration
            .ReadAsync(Settings.RedirectDefaultClient, cancellationToken).ConfigureAwait(false))
        .Match(value => value, _ => string.Empty);

    private async ValueTask<string> LandingAsync(
        RegistrationSessionId session,
        CancellationToken cancellationToken)
    {
        if (await LiveAsync(session, cancellationToken).ConfigureAwait(false) is not
            { Client.Length: > 0 } live)
        {
            return string.Empty;
        }

        return await clients.FindAsync(live.Client, cancellationToken).ConfigureAwait(false)
            is OidcClient originating
            ? originating.Redirect
            : string.Empty;
    }

    /// <summary>
    /// The terms step, with what only the boundary can act on: the secrets the
    /// browser is to carry away. The contract method is this one without them,
    /// because a caller in process has no cookie to write them to.
    /// </summary>
    /// <param name="session">The registration session.</param>
    /// <param name="termsVersion">The version of the terms accepted.</param>
    /// <param name="noticeVersion">The version of the notice presented.</param>
    /// <param name="consents">What each consent control was left at.</param>
    /// <param name="device">What the browser said it is.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account, the session it is signed in on, and the browser token.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    internal async ValueTask<Result<RegistrationOutcome>> CompleteAsync(
        RegistrationSessionId session,
        string termsVersion,
        string noticeVersion,
        IReadOnlyDictionary<string, bool> consents,
        DeviceDescription device,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(termsVersion);
        ArgumentNullException.ThrowIfNull(noticeVersion);
        ArgumentNullException.ThrowIfNull(consents);
        ArgumentNullException.ThrowIfNull(device);

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Result.Failure<RegistrationOutcome>(Error.From(ErrorCodes.SessionExpired));
        }

        // The affirmation is what the age screen derived, and the terms step refuses
        // without it (REG-SESS-007, `09` section 2).
        if (!live.AgeAnswered)
        {
            return Result.Failure<RegistrationOutcome>(
                Error.From(ErrorCodes.AffirmationRequired));
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(live.Provisional, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationOutcome>(failure);
        }

        if (live.Step is not RegistrationStep.Terms
            || !SecurityStep.Complete(live, policy.LoginFactors))
        {
            return Result.Failure<RegistrationOutcome>(
                Error.From(ErrorCodes.RegistrationIncomplete));
        }

        int emails = (await configuration
                .ReadAsync(Settings.IdentifiersEmailMax, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        int phones = (await configuration
                .ReadAsync(Settings.IdentifiersPhoneMax, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationOutcome>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        live.AcceptTerms(termsVersion, noticeVersion);

        await directory
            .CreateAsync(Created(live, now, emails, phones), cancellationToken)
            .ConfigureAwait(false);
        await WriteCredentialsAsync(live, now, cancellationToken).ConfigureAwait(false);

        IssuedSession issued = (await issuing
                .BeginAsync(
                    live.Provisional,
                    SecurityStep.Presented(live, policy.LoginFactors),
                    new SessionOrigin(live.Source, device),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IssuedSession>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationOutcome>(failure);
        }

        // The browser that completed the step is remembered, so the account's next
        // sign-in from it is not held for a new-device code (REG-SESS-007 AC4).
        OpaqueToken browser = (await devices
                .RememberAsync(live.Provisional, device, cancellationToken)
                .ConfigureAwait(false))
            .Match(token => token, error => Held<OpaqueToken>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationOutcome>(failure);
        }

        // A control the deployment takes no consent for, or a step reached before any
        // notice was published, is a request that should not have been made: nothing
        // is written and the person is not registered under a record we cannot keep.
        if (await RecordedAsync(live.Provisional, consents, cancellationToken)
                .ConfigureAwait(false) is Error unrecorded)
        {
            return Result.Failure<RegistrationOutcome>(unrecorded);
        }

        // Nothing of the session survives it: an account exists now, and a staged
        // copy of what made it would be a second place the same facts live.
        await sessions.RemoveAsync(live.Id, cancellationToken).ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountRegistered(now, live.Provisional.ToString())
                {
                    Subject = live.Provisional,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure<RegistrationOutcome>(unpublished);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new RegistrationOutcome(live.Provisional, issued, browser));
    }

    // REG-SESS-007 AC1: one control per consent-based purpose, ticked by the person
    // and never by the library. A control left unticked writes nothing and stops
    // nothing, which is what lets registration complete with all of them unticked.
    private async ValueTask<Error?> RecordedAsync(
        SubjectId subject,
        IReadOnlyDictionary<string, bool> consents,
        CancellationToken cancellationToken)
    {
        var holder = AccessContext.Of(subject);

        foreach (KeyValuePair<string, bool> control in consents.OrderBy(
            control => control.Key,
            StringComparer.Ordinal))
        {
            if (!control.Value)
            {
                continue;
            }

            Error? refused = (await capture
                    .GrantAsync(holder, control.Key, ConsentMechanism.Registration, cancellationToken)
                    .ConfigureAwait(false))
                .Match(() => (Error?)null, error => error);

            if (refused is not null)
            {
                return refused;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<Result> AbandonAsync(
        RegistrationSessionId? session,
        string? linkToken,
        CancellationToken cancellationToken)
    {
        RegistrationSession? live = null;

        if (linkToken is not null)
        {
            live = await sessions
                .FindByLinkAsync(OpaqueToken.Of(linkToken).Fingerprint(), cancellationToken)
                .ConfigureAwait(false);
        }

        if (live is null && session is RegistrationSessionId held)
        {
            live = await sessions.FindAsync(held, cancellationToken).ConfigureAwait(false);
        }

        if (live is null)
        {
            return Result.Success();
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sessions.RemoveAsync(live.Id, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Removes every session that has lapsed, which is what leaves nothing behind.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were swept.</returns>
    public async ValueTask<int> SweepAsync(CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        int swept = await sessions
            .SweepAsync(time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return swept;
    }

    /// <summary>
    /// Stages a credential a ceremony enrolled against the session, which counts at
    /// the security step and is written when the account comes into being.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="credential">The credential the ceremony produced.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The state, carrying the recovery codes where this enrolment is what put a
    /// second step beside a password, or the refusal where the step admits none.
    /// </returns>
    /// <exception cref="ArgumentNullException">The credential is absent.</exception>
    public async ValueTask<Result<RegistrationState>> EnrolAsync(
        RegistrationSessionId session,
        StagedCredential credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        if (live.Step is not (RegistrationStep.Security or RegistrationStep.Terms))
        {
            return OutOfStep();
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        live.Enrol(credential);

        return await SettledAsync(live, cancellationToken).ConfigureAwait(false);
    }

    private static Result<RegistrationState> Gone() =>
        Result.Failure<RegistrationState>(Error.From(ErrorCodes.SessionExpired));

    private static Result<RegistrationState> OutOfStep() =>
        Result.Failure<RegistrationState>(Error.From(ErrorCodes.RegistrationIncomplete));

    private static AgeGroup Band(bool adult) => adult ? AgeGroup.Adult : AgeGroup.Minor;

    private static bool IsAdult(DateOnly dateOfBirth, DateOnly today) =>
        dateOfBirth <= today.AddYears(-Majority);

    private static IntegerSetting Maximum(IdentifierKind kind) =>
        kind is IdentifierKind.Email ? Settings.IdentifiersEmailMax : Settings.IdentifiersPhoneMax;

    private static StagedIdentity? Sent(RegistrationSession session, byte[] fingerprint)
    {
        foreach (StagedIdentity staged in session.Identifiers)
        {
            if (staged.Link is byte[] link
                && !staged.IsVerified
                && CryptographicOperations.FixedTimeEquals(link, fingerprint))
            {
                return staged;
            }
        }

        return null;
    }

    private static (string Entered, string Canonical)? Canonical(IdentifierKind kind, string value)
    {
        string entered = value.Trim();

        if (kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(entered, out EmailAddress address)
                ? (entered, address.Value)
                : null;
        }

        return PhoneNumber.TryParse(entered, out PhoneNumber number)
            ? (entered, number.Value)
            : null;
    }

    private static List<string> OwnWords(RegistrationSession session)
    {
        var words = new List<string>(session.Identifiers.Count);

        foreach (StagedIdentity staged in session.Identifiers)
        {
            words.Add(staged.Canonical);
        }

        return words;
    }

    private static NewAccount Created(
        RegistrationSession session,
        DateTimeOffset now,
        int emails,
        int phones)
    {
        var identifiers = new List<NewIdentifier>(session.Identifiers.Count);

        foreach (StagedIdentity staged in session.Identifiers)
        {
            identifiers.Add(new NewIdentifier(
                staged.Id,
                staged.Kind,
                staged.Entered,
                staged.Canonical,
                staged.IsLocked,
                staged.VerifiedAt ?? now));
        }

        return new NewAccount(
            session.Provisional,
            now,
            identifiers,
            session.DateOfBirth,
            session.AdultAffirmed,
            session.Group,
            session.AnsweredAgeAt ?? now,
            session.TermsVersion ?? string.Empty,
            session.NoticeVersion ?? string.Empty,
            emails,
            phones);
    }

    private static RegistrationState State(
        RegistrationSession session,
        IReadOnlyList<string>? drawn = null)
    {
        var staged = new List<StagedIdentifier>(session.Identifiers.Count);
        var steps = new List<Factor>(session.Credentials.Count);

        foreach (StagedIdentity identity in session.Identifiers)
        {
            staged.Add(new StagedIdentifier(
                identity.Id,
                identity.Kind,
                identity.Entered,
                identity.IsVerified,
                identity.IsLocked));
        }

        foreach (StagedCredential credential in session.Credentials)
        {
            steps.Add(credential.Factor);
        }

        return new RegistrationState(
            session.Step,
            session.ExpiresAt,
            staged,
            new RegistrationSecurity(session.Password is not null, steps, drawn));
    }

    private static Authenticator Enrolled(
        SubjectId subject,
        StagedCredential staged,
        DateTimeOffset now) =>
        staged.WebAuthn is WebAuthnMaterial material
            ? Authenticator.WebAuthnCredential(
                staged.Id,
                subject,
                staged.Factor,
                staged.Label,
                material,
                now)
            : Authenticator.Existing(
                staged.Id,
                subject,
                staged.Factor,
                staged.Label,
                AuthenticatorState.Active,
                now,
                lastUsedAt: null,
                invalidatesAt: null,
                confirmed: true,
                staged.Totp,
                webAuthn: null);

    private static SendDestination Destination(StagedIdentity staged) =>
        staged.Kind is IdentifierKind.Email
            ? SendDestination.Of(Address(staged.Canonical))
            : SendDestination.Of(Number(staged.Canonical));

    private static EmailAddress Address(string canonical) =>
        EmailAddress.TryParse(canonical, out EmailAddress address)
            ? address
            : throw new InvalidOperationException("A staged address is canonical already.");

    private static PhoneNumber Number(string canonical) =>
        PhoneNumber.TryParse(canonical, out PhoneNumber number)
            ? number
            : throw new InvalidOperationException("A staged number is canonical already.");

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<RegistrationSession?> LiveAsync(
        RegistrationSessionId id,
        CancellationToken cancellationToken)
    {
        RegistrationSession? session =
            await sessions.FindAsync(id, cancellationToken).ConfigureAwait(false);

        return session is null || session.HasExpired(time.GetUtcNow()) ? null : session;
    }

    // The security step has no completion of its own: it is done the moment the
    // account would reach a primary sign-in method with nothing outstanding, and
    // undone again by anything that takes that away (REG-SESS-006 AC3).
    private async ValueTask<Result<RegistrationState>> SettledAsync(
        RegistrationSession session,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy policy = (await policies.ForAsync(session.Provisional, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationState>(failure);
        }

        IReadOnlyList<string>? drawn = null;

        if (SecurityStep.PasswordSeconded(session) && session.RecoveryCodes is null)
        {
            PreparedRecoveryCodes set = (await recoveryCodes
                    .PrepareAsync(cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<PreparedRecoveryCodes>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<RegistrationState>(failure);
            }

            session.StageRecoveryCodes(set.Hashes);
            drawn = set.Codes;
        }

        session.Reached(
            SecurityStep.Complete(session, policy.LoginFactors)
                ? RegistrationStep.Terms
                : RegistrationStep.Security);

        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(State(session, drawn));
    }

    private async ValueTask<Result<RegistrationState>> CollectAsync(
        RegistrationSession session,
        IdentifierKind kind,
        string value,
        bool isExtra,
        CancellationToken cancellationToken)
    {
        if (Canonical(kind, value) is not (string entered, string canonical))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(canonical))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierMixedScript));
        }

        var staged = StagedIdentity.Of(
            IdentifierId.New(time),
            kind,
            entered,
            canonical,
            isLocked: false,
            isExtra);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        session.Stage(staged);

        if (await DispatchAsync(session, staged, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<RegistrationState>(refused);
        }

        if (!isExtra)
        {
            session.Reached(kind is IdentifierKind.Email
                ? RegistrationStep.Phone
                : RegistrationStep.Confirm);
        }

        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(State(session));
    }

    // The two cases are one path: the lookup decides only whether the code goes to
    // the person registering or the holder is told instead, and the caller cannot
    // tell which happened (REG-SESS-005, AUTH-ABUSE-003).
    private async ValueTask<Error?> DispatchAsync(
        RegistrationSession session,
        StagedIdentity staged,
        CancellationToken cancellationToken)
    {
        SubjectId? owner = await directory
            .OwnerAsync(staged.Kind, staged.Canonical, cancellationToken)
            .ConfigureAwait(false);

        return owner is SubjectId holder
            ? await TellHolderAsync(session, staged, holder, cancellationToken).ConfigureAwait(false)
            : await SendCodeAsync(session, staged, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Error?> SendCodeAsync(
        RegistrationSession session,
        StagedIdentity staged,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.CodeVerificationLifetime, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        string code = VerificationCode.Draw(randomness);
        var link = OpaqueToken.Draw(randomness);

        Result<SendReference> sent = await sending
            .SendAsync(
                new SendRequest(
                    Destination(staged),
                    MessageKind.VerificationCode,
                    RestrictionPurpose.Verification,
                    session.Source,
                    session.Language)
                {
                    Values = new Dictionary<string, string>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["code"] = code,
                        ["token"] = link.Value,
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        staged.Sent(VerificationCode.Held(code), link.Fingerprint(), time.GetUtcNow() + lifetime);

        return null;
    }

    private async ValueTask<Error?> TellHolderAsync(
        RegistrationSession session,
        StagedIdentity staged,
        SubjectId holder,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.AbuseNonexistentWindow, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        if (!await notices
            .FirstAsync(staged.Canonical, time.GetUtcNow(), window, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        // The holder is told and the person registering is told nothing: the message
        // names no requester and carries neither a code nor a link (REG-SESS-005 AC2).
        Result<SendReference> sent = await sending
            .SendAsync(
                new SendRequest(
                    Destination(staged),
                    MessageKind.AccountExists,
                    RestrictionPurpose.Notification,
                    session.Source,
                    session.Language)
                {
                    Subject = holder,
                },
                cancellationToken)
            .ConfigureAwait(false);

        // A refusal to tell the holder is not a refusal of the step: the person
        // registering sees the same outcome either way (REG-SESS-005 AC1).
        return sent.Match(_ => (Error?)null, _ => null);
    }

    private async ValueTask WriteCredentialsAsync(
        RegistrationSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (session.Password is PasswordHash hash)
        {
            await passwordStore
                .SetAsync(
                    Password.Set(session.Provisional, hash, session.PasswordStandsAlone, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (StagedCredential staged in session.Credentials)
        {
            await authenticators
                .AddAsync(Enrolled(session.Provisional, staged, now), cancellationToken)
                .ConfigureAwait(false);
        }

        if (session.RecoveryCodes is IReadOnlyList<PasswordHash> codes)
        {
            await recoveryCodeStore
                .ReplaceAsync(
                    RecoveryCodeSet.Of(session.Provisional, codes, now),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
