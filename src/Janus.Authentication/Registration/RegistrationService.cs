using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Invitations;
using Janus.Authentication.Oidc;
using Janus.Authentication.Organizations;
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
/// <param name="landing">Where a link the message carries lands.</param>
/// <param name="notices">What remembers which addresses have been told.</param>
/// <param name="passwords">What screens and hashes a password.</param>
/// <param name="passwordStore">Where the account's password is written.</param>
/// <param name="recoveryCodes">What draws a set of recovery codes.</param>
/// <param name="recoveryCodeStore">Where the account's set is written.</param>
/// <param name="authenticators">Where the account's credentials are written.</param>
/// <param name="keys">What opens and reads a WebAuthn creation ceremony.</param>
/// <param name="generators">What judges a code of a generator's secret.</param>
/// <param name="clients">The registry the originating client is resolved against.</param>
/// <param name="policies">Where the policy in force is resolved.</param>
/// <param name="invitations">Where the invitation a registration was opened by is kept.</param>
/// <param name="opening">What attaches an invitation a signed-in person opens to their account.</param>
/// <param name="locks">Whether the inviting organization's domain lock admits an address.</param>
/// <param name="issuing">What issues the session the person is signed in on.</param>
/// <param name="devices">What remembers the registering browser.</param>
/// <param name="throttle">The progressive delay a registration's asks and tries are held to.</param>
/// <param name="codes">Where the code that verifies a staged identifier is issued and answered.</param>
/// <param name="restrictions">What an ask that sends nothing draws on.</param>
/// <param name="capture">Where the consent controls the person ticked are recorded.</param>
/// <param name="configuration">Where the registration settings are read.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="events">Where the emitted events go.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the codes and the tokens are drawn from.</param>
/// <remarks>
/// Implements REG-SESS-001 to REG-SESS-008, REG-PROF-002, REG-IDENT-010, REG-INV-001,
/// REG-INV-002, REG-MAIL-001, REG-DOM-001, IDN-LIFE-009a, API-REDIR-002, AUTH-FACT-004,
/// AUTH-ABUSE-001 and AUTH-ABUSE-003. Every answer is the same whether or not the identifier presented
/// belongs to an account already: the lookup decides only whether a code goes out and
/// whether the holder is told. The one exception is the email an invitation binds,
/// whose link only its mailbox received.
/// </remarks>
internal sealed class RegistrationService(
    IRegistrationSessionStore sessions,
    IRegistrationDirectory directory,
    IGovernedSend sending,
    LandingLinks landing,
    INoticeLedger notices,
    PasswordService passwords,
    IPasswordStore passwordStore,
    RecoveryCodeService recoveryCodes,
    IRecoveryCodeStore recoveryCodeStore,
    IAuthenticatorStore authenticators,
    WebAuthnService keys,
    TotpService generators,
    IOidcClientStore clients,
    PolicyResolution policies,
    IInvitationStore invitations,
    InvitationOpening opening,
    DomainLock locks,
    SessionService issuing,
    DeviceService devices,
    ThrottleService throttle,
    VerificationCodes codes,
    ISendingRestrictions restrictions,
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
        AccessContext? signedIn,
        string client,
        string language,
        string source,
        [NeverLogged] string? invitationToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(source);

        if (signedIn is not null)
        {
            return await SignedInAsync(signedIn, invitationToken, cancellationToken).ConfigureAwait(false);
        }

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

        DateTimeOffset now = time.GetUtcNow();

        var session = RegistrationSession.Open(
            RegistrationSessionId.New(time),
            SubjectId.New(randomness),
            originating?.ClientId
                ?? await DefaultClientAsync(cancellationToken).ConfigureAwait(false),
            language,
            source,
            now,
            lifetime);

        Invitation? invitation = null;

        if (invitationToken is not null)
        {
            invitation = (await InvitedAsync(session, invitationToken, now, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Held<Invitation>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<RegistrationSessionId>(failure);
            }
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationSessionId>(notBegun);
        }

        // D-166 X3: the link is judged again on the invitation under its lock, so of two
        // presses at once, or a press and a revocation, only the first stands.
        if (invitation is not null)
        {
            if (await invitations.FindForUpdateAsync(invitation.Id, cancellationToken).ConfigureAwait(false)
                is not Invitation unopened
                || !unopened.Opens(now))
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure<RegistrationSessionId>(Error.From(ErrorCodes.InvitationExpired));
            }

            unopened.AttachTo(session.Id, now);
            invitation = unopened;
        }

        await sessions.AddAsync(session, cancellationToken).ConfigureAwait(false);

        if (invitation is not null)
        {
            await invitations.RecordAsync(invitation, cancellationToken).ConfigureAwait(false);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationSessionId>(notCommitted);
        }

        return Result.Success(session.Id);
    }

    // REG-SESS-002: a person already signed in is refused and sent to their account,
    // and no registration session is created for them. An invitation link they press
    // attaches to that account, whose membership step reads it (REG-INV-002).
    private async ValueTask<Result<RegistrationSessionId>> SignedInAsync(
        AccessContext signedIn,
        [NeverLogged] string? invitationToken,
        CancellationToken cancellationToken)
    {
        Error? unopened = invitationToken is null
            ? null
            : (await opening.OpenAsync(signedIn, invitationToken, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error);

        return Result.Failure<RegistrationSessionId>(unopened ?? Error.From(ErrorCodes.RegistrationSignedIn));
    }

    // REG-INV-001 and REG-MAIL-001: the token is single use, so the invitation it opens
    // is attached, under its lock, to this registration and to no other. The link went to the email the
    // invitation binds and nowhere else, so the press is that address's verification,
    // and only its mailbox can learn that an account holds it already (REG-INV-002). A
    // bound phone is staged locked and verified by its code at the phone step.
    private async ValueTask<Result<Invitation>> InvitedAsync(
        RegistrationSession session,
        [NeverLogged] string invitationToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Invitation? invitation = await invitations
            .FindByTokenAsync(OpaqueToken.Of(invitationToken).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null
            || !invitation.Opens(now)
            || invitation.Identifiers is not InvitedIdentifiers bound)
        {
            return Result.Failure<Invitation>(Error.From(ErrorCodes.InvitationExpired));
        }

        if (bound.Email is string email)
        {
            StagedIdentity staged = Locked(IdentifierKind.Email, email);

            // REG-IDENT-006: an address held out of reach for its owner's undo is
            // refused as one an account holds, so the undo still finds it free.
            if (await TakenAsync(IdentifierKind.Email, staged.Canonical, cancellationToken)
                    .ConfigureAwait(false))
            {
                return Result.Failure<Invitation>(Error.From(ErrorCodes.InvitationIdentifierMismatch));
            }

            staged.Verify(now);
            session.Stage(staged);
        }

        if (bound.Phone is string phone)
        {
            session.Stage(Locked(IdentifierKind.Phone, phone));
        }

        session.Invited(invitation.Id);

        return Result.Success(invitation);
    }

    private StagedIdentity Locked(IdentifierKind kind, string value) =>
        Canonical(kind, value) is (string entered, string canonical)
            ? StagedIdentity.Of(IdentifierId.New(time), kind, entered, canonical, isLocked: true, isExtra: false)
            : throw new InvalidOperationException("An invitation binds what its issue read.");

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
    public ValueTask<Result<RegistrationState>> RecordAgeAsync(
        RegistrationSessionId session,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken) =>
        HeldAsync(session, live => AgeAsync(live, dateOfBirth, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<RegistrationState>> VerifyAsync(
        RegistrationSessionId session,
        IdentifierId identifier,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        return HeldAsync(
            session,
            live => VerifiedAsync(live, identifier, code, source, cancellationToken),
            cancellationToken);
    }

    private async ValueTask<(Result<RegistrationState> Answer, bool Commits)> AgeAsync(
        RegistrationSession? live,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken)
    {
        if (live is null)
        {
            return (Gone(), false);
        }

        // The screen locks once it has refused a date, so that a person cannot walk
        // the date forward until it passes (REG-PROF-002).
        if (live.AgeRefused)
        {
            return (Result.Failure<RegistrationState>(Error.From(ErrorCodes.ProfileUnderage)), false);
        }

        if (live.Step is not RegistrationStep.Age)
        {
            return (OutOfStep(), false);
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
            return (Result.Failure<RegistrationState>(failure), false);
        }

        DateTimeOffset now = time.GetUtcNow();
        bool adult = IsAdult(dateOfBirth, DateOnly.FromDateTime(now.UtcDateTime));

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return (Result.Failure<RegistrationState>(notBegun), false);
        }

        if (affirmation is not AttributeRequirement.Off && !adult)
        {
            live.RefuseAge(now);

            await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notCommittedAgain)
            {
                return (Result.Failure<RegistrationState>(notCommittedAgain), true);
            }

            return (Result.Failure<RegistrationState>(Error.From(ErrorCodes.ProfileUnderage)), true);
        }

        live.AnswerAge(
            affirmation is AttributeRequirement.Off ? null : adult,
            retention is AttributeRequirement.Off ? null : dateOfBirth,
            affirmation is AttributeRequirement.Off ? Band(adult) : null,
            now);

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return (Result.Failure<RegistrationState>(notCommitted), true);
        }

        return (Result.Success(State(live)), true);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> StageAsync(
        RegistrationSessionId session,
        IdentifierKind kind,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(source);

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Gone();
        }

        // No identifier field is reached before the age screen is answered
        // (REG-PROF-002 AC1): the step is asked for before the one it follows, which
        // chapter 09 section 2 answers as every step out of its order.
        if (!live.AgeAnswered)
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.RegistrationIncomplete));
        }

        RegistrationStep collecting =
            kind is IdentifierKind.Email ? RegistrationStep.Email : RegistrationStep.Phone;

        if (kind is IdentifierKind.Username || live.Step != collecting)
        {
            return OutOfStep();
        }

        if (await DelayedAsync(Attempt(source, value), cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure<RegistrationState>(delayed);
        }

        if (live.Bound(kind) is StagedIdentity bound)
        {
            return await ResentAsync(live, bound, value, source, cancellationToken).ConfigureAwait(false);
        }

        return await CollectAsync(live, kind, value, isExtra: false, source, cancellationToken)
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

        // REG-MAIL-001 AC3: a phone the invitation bound is verified before the
        // membership step, so its step is never passed over.
        if (phone is AttributeRequirement.Required || live.Bound(IdentifierKind.Phone) is not null)
        {
            return OutOfStep();
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationState>(notBegun);
        }

        live.SkipPhone();

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationState>(notCommitted);
        }

        return Result.Success(State(live));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> AddAsync(
        RegistrationSessionId session,
        IdentifierKind kind,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(source);

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

        if (await DelayedAsync(Attempt(source, value), cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure<RegistrationState>(delayed);
        }

        return await CollectAsync(live, kind, value, isExtra: true, source, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> ChangeAsync(
        RegistrationSessionId session,
        IdentifierId identifier,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(source);

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

        if (await DelayedAsync(Attempt(source, value), cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure<RegistrationState>(delayed);
        }

        if (staged.IsLocked)
        {
            return await ResentAsync(live, staged, value, source, cancellationToken).ConfigureAwait(false);
        }

        if (Canonical(staged.Kind, value) is not (string entered, string canonical))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(canonical))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierMixedScript));
        }

        if (await LockRefusedAsync(live, staged.Kind, canonical, cancellationToken).ConfigureAwait(false)
            is Error outside)
        {
            return Result.Failure<RegistrationState>(outside);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationState>(notBegun);
        }

        staged.Change(entered, canonical);

        if (await DispatchAsync(live, staged, source, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<RegistrationState>(refused);
        }

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationState>(notCommitted);
        }

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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationState>(notBegun);
        }

        live.Discard(staged);

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationState>(notCommitted);
        }

        return Result.Success(State(live));
    }

    private async ValueTask<(Result<RegistrationState> Answer, bool Commits)> VerifiedAsync(
        RegistrationSession? live,
        IdentifierId identifier,
        [NeverLogged] string code,
        string source,
        CancellationToken cancellationToken)
    {
        if (live is null)
        {
            return (Gone(), false);
        }

        if (live.Identity(identifier) is not StagedIdentity staged)
        {
            return (OutOfStep(), false);
        }

        // REG-SESS-003 AC6: a try is held to the delay the source and the identifier have
        // earned, and every refused one is counted towards it.
        ThrottleAttempt attempt = Attempt(source, staged.Canonical);

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return (Result.Failure<RegistrationState>(delayed), false);
        }

        if (staged.IsVerified)
        {
            return await CountedAsync(attempt, Error.From(ErrorCodes.CodeInvalid), cancellationToken)
                .ConfigureAwait(false);
        }

        // AUTH-FACT-004: the try is read, compared and counted on the verification-code
        // record under its lock, the same for a code that was sent and for the record
        // of a held or reserved value, which no code matches (REG-SESS-005 AC5).
        Result presented = await codes
            .PresentAsync(Holder(live.Id, staged.Id), code, cancellationToken)
            .ConfigureAwait(false);

        if (presented.Match(() => (Error?)null, error => error) is Error refused)
        {
            return refused.Code == ErrorCodes.CodeInvalid || refused.Code == ErrorCodes.CodeExpired
                ? await CountedAsync(attempt, refused, cancellationToken).ConfigureAwait(false)
                : (Result.Failure<RegistrationState>(refused), false);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return (Result.Failure<RegistrationState>(notBegun), false);
        }

        staged.Verify(time.GetUtcNow());

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return (Result.Failure<RegistrationState>(notCommitted), true);
        }

        return (Result.Success(State(live)), true);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<LinkLanding>> LandAsync(
        RegistrationSessionId? session,
        [NeverLogged] string linkToken,
        bool press,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);
        ArgumentNullException.ThrowIfNull(source);

        byte[] fingerprint = OpaqueToken.Of(linkToken).Fingerprint();

        RegistrationSession? sender =
            await sessions.FindByLinkAsync(fingerprint, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();

        if (sender is null
            || sender.HasExpired(now)
            || Sent(sender, fingerprint) is not StagedIdentity staged)
        {
            // REG-SESS-003 AC6: a pressed token that opens nothing names no identifier,
            // so it is held to the delay of the source that presents it and counted
            // against that source alone; one merely opened counts nothing.
            return press
                ? await GoneAsync(source, cancellationToken).ConfigureAwait(false)
                : Result.Failure<LinkLanding>(Error.From(ErrorCodes.CodeInvalid));
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
                sameBrowser
                    ? null
                    : await codes.ShownAsync(Holder(sender.Id, staged.Id), cancellationToken).ConfigureAwait(false)));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<LinkLanding>(notBegun);
        }

        // The press proved the address, so the code that would have is ended with it.
        await codes.EndAsync(Holder(sender.Id, staged.Id), cancellationToken).ConfigureAwait(false);

        staged.Verify(now);

        await sessions.RecordAsync(sender, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<LinkLanding>(notCommitted);
        }

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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationState>(notBegun);
        }

        live.Reached(RegistrationStep.Security);

        // The security step is settled on arrival too: where the policy admits a
        // factor that rides a verified identifier, there is nothing left to collect
        // on that screen (REG-SESS-006 AC2).
        return await SettledAsync(live, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<RegistrationState>> SetPasswordAsync(
        RegistrationSessionId session,
        [NeverLogged] string password,
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

            if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notBegun)
            {
                return Result.Failure<RegistrationState>(notBegun);
            }

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
                    address: null,
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
        .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

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
            ? RedirectValidation.Landing(originating)
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
    /// <param name="address">
    /// The whole address of the request completing the step, which the first session
    /// records (REG-SESS-007, AUTH-SESS-013), or nothing where a caller in process
    /// completes it, whose session records the address the registration began on.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The account, the session it is signed in on, and the browser token.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    internal async ValueTask<Result<RegistrationOutcome>> CompleteAsync(
        RegistrationSessionId session,
        string termsVersion,
        string noticeVersion,
        IReadOnlyDictionary<string, bool> consents,
        DeviceDescription device,
        string? address,
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

        Policy policy = (await PolicyAsync(live, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationOutcome>(failure);
        }

        // REG-SESS-007: the account is never created without the terms version
        // accepted and the notice version presented, so a step that carries either
        // blank has not completed.
        if (live.Step is not RegistrationStep.Terms
            || !SecurityStep.Complete(live, policy.LoginFactors)
            || string.IsNullOrWhiteSpace(termsVersion)
            || string.IsNullOrWhiteSpace(noticeVersion))
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

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<RegistrationOutcome>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationOutcome>(notBegun);
        }

        // REG-SESS-005, CONV-DESIGN-003: every value the account is about to hold is
        // locked before any is judged, so none is taken or reserved between the
        // judgement and the write.
        await directory
            .LockValuesAsync([.. live.Identifiers.Select(staged => (staged.Kind, staged.Canonical))], cancellationToken)
            .ConfigureAwait(false);

        // REG-SESS-005 AC4: a staged identifier another account took, or that became
        // reserved for an undo, since it was staged ends the session here, before the
        // account is written, and the step is answered as an expired session.
        foreach (StagedIdentity staged in live.Identifiers)
        {
            if (!await TakenAsync(staged.Kind, staged.Canonical, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await sessions.RemoveAsync(live.Id, cancellationToken).ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error unended)
            {
                return Result.Failure<RegistrationOutcome>(unended);
            }

            return Result.Failure<RegistrationOutcome>(Error.From(ErrorCodes.SessionExpired));
        }

        live.AcceptTerms(termsVersion, noticeVersion);

        // IDN-ATTR-001: registration settles the account's language from the locale
        // it was begun under, so a later message finds a preference to go out in.
        await directory
            .CreateAsync(
                Created(
                    live,
                    termsVersion,
                    noticeVersion,
                    now,
                    emails,
                    phones,
                    RecipientLanguage.Found(live.Language, languages)),
                cancellationToken)
            .ConfigureAwait(false);
        await WriteCredentialsAsync(live, now, cancellationToken).ConfigureAwait(false);

        // REG-INV-001: until the person acknowledges it at the membership step, the
        // account holds the invitation and nothing of its organization.
        // D-166 X3: read under its lock, so a revocation committed meanwhile is carried
        // and not written over.
        if (live.Invitation is InvitationId invited
            && await invitations.FindForUpdateAsync(invited, cancellationToken).ConfigureAwait(false) is { } invitation
            && invitation.Session == live.Id)
        {
            invitation.Registered(live.Provisional);

            await invitations.RecordAsync(invitation, cancellationToken).ConfigureAwait(false);
        }

        IssuedSession issued = (await issuing
                .BeginRegisteredAsync(
                    live.Provisional,
                    SecurityStep.Presented(live, policy.LoginFactors),
                    new SessionOrigin(address ?? live.Source, device),
                    live.Client.Length > 0 ? live.Client : null,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IssuedSession>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

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
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<RegistrationOutcome>(failure);
        }

        // A control the deployment takes no consent for, or a step reached before any
        // notice was published, is a request that should not have been made: nothing
        // is written and the person is not registered under a record we cannot keep.
        if (await RecordedAsync(live.Provisional, consents, cancellationToken)
                .ConfigureAwait(false) is Error unrecorded)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<RegistrationOutcome>(unrecorded);
        }

        // Nothing of the session survives it: an account exists now, and a staged
        // copy of what made it would be a second place the same facts live.
        await EndCodesAsync(live, cancellationToken).ConfigureAwait(false);
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
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<RegistrationOutcome>(unpublished);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationOutcome>(notCommitted);
        }

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
        [NeverLogged] string? linkToken,
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await EndCodesAsync(live, cancellationToken).ConfigureAwait(false);
        await sessions.RemoveAsync(live.Id, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <summary>
    /// Removes every session that has lapsed, which is what leaves nothing behind.
    /// </summary>
    /// <param name="context">The system principal the pass runs as.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many were swept.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may sweep what has expired.</exception>
    public async ValueTask<int> SweepAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Sweeping(context);

        (await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        int swept = await sessions
            .SweepAsync(time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

        return swept;
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the pass runs as a named
    // principal that may sweep what has expired, and never as nobody.
    private static SystemPrincipal Sweeping(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.ExpirySweep)
            ? principal
            : throw new ArgumentException(
                "The pass runs as a system principal that may sweep what has expired.",
                nameof(context));

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
    public ValueTask<Result<RegistrationState>> EnrolAsync(
        RegistrationSessionId session,
        StagedCredential credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return HeldAsync(
            session,
            async live =>
            {
                if (live is null)
                {
                    return (Gone(), false);
                }

                if (live.Step is not (RegistrationStep.Security or RegistrationStep.Terms))
                {
                    return (OutOfStep(), false);
                }

                live.Enrol(credential);

                (Error? failure, IReadOnlyList<string>? drawn) =
                    await SettleAsync(live, cancellationToken).ConfigureAwait(false);

                return failure is not null
                    ? (Result.Failure<RegistrationState>(failure), false)
                    : (Result.Success(State(live, drawn)), true);
            },
            cancellationToken);
    }

    /// <summary>
    /// Opens a WebAuthn creation ceremony against the session at its security step,
    /// in place of any it had open. The ceremony is a staged value of the session and
    /// nothing is written outside the session store (REG-SESS-001, REG-SESS-006).
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="kind">Which catalogue entry the ceremony creates.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the browser is asked for, or the refusal.</returns>
    /// <remarks>
    /// Implements REG-PM-001: the handle is the session's provisional subject
    /// identifier, which the account carries, and the name is the staged email.
    /// </remarks>
    public ValueTask<Result<CredentialCeremony>> BeginKeyAsync(
        RegistrationSessionId session,
        Factor kind,
        CancellationToken cancellationToken) =>
        HeldAsync(session, live => OpenedAsync(live, kind, cancellationToken), cancellationToken);

    /// <summary>
    /// Stages the credential a creation ceremony produced, spending the ceremony the
    /// session had open. No account row is written before the terms step.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="attestation">What the browser sent back.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential, with the recovery codes where it is what put a second step
    /// beside a password, or the refusal.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result<EnrolledCredential>> CompleteKeyAsync(
        RegistrationSessionId session,
        AuthenticatorAttestation attestation,
        string label,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        ArgumentNullException.ThrowIfNull(label);

        return CredentialLabel.TryParse(label, out CredentialLabel named)
            ? HeldAsync(session, live => CreatedAsync(live, attestation, named, cancellationToken), cancellationToken)
            : ValueTask.FromResult(
                Result.Failure<EnrolledCredential>(Error.From(ErrorCodes.CredentialLabelInvalid)));
    }

    /// <summary>
    /// Begins a code generator against the session at its security step, in place of
    /// any it had begun and not confirmed. The secret is a staged value of the session
    /// until a code confirms it (AUTH-FACT-006, AUTH-FACT-007).
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="label">What the person calls it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>What the authenticator app is given, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The label is absent.</exception>
    public ValueTask<Result<GeneratorEnrolment>> BeginGeneratorAsync(
        RegistrationSessionId session,
        string label,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);

        return CredentialLabel.TryParse(label, out CredentialLabel named)
            ? HeldAsync(session, live => BegunAsync(live, named, cancellationToken), cancellationToken)
            : ValueTask.FromResult(
                Result.Failure<GeneratorEnrolment>(Error.From(ErrorCodes.CredentialLabelInvalid)));
    }

    /// <summary>
    /// Confirms the generator the session began with one code of its secret, which
    /// spends it and stages the credential.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="credential">Which enrolment.</param>
    /// <param name="code">What was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential, with the recovery codes where it is what put a second step
    /// beside a password, or the refusal.
    /// </returns>
    /// <exception cref="ArgumentNullException">The code is absent.</exception>
    public ValueTask<Result<EnrolledCredential>> ConfirmGeneratorAsync(
        RegistrationSessionId session,
        AuthenticatorId credential,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        return HeldAsync(
            session,
            live => ConfirmedAsync(live, credential, code, cancellationToken),
            cancellationToken);
    }

    private async ValueTask<(Result<CredentialCeremony> Answer, bool Commits)> OpenedAsync(
        RegistrationSession? live,
        Factor kind,
        CancellationToken cancellationToken)
    {
        if ((Enrolling(live) ?? await AdmitsAsync(live!, kind, cancellationToken).ConfigureAwait(false))
            is Error refused)
        {
            return (Result.Failure<CredentialCeremony>(refused), false);
        }

        Error? failure = null;

        WebAuthnCeremony ceremony = (await keys
                .BeginAsync(
                    kind,
                    new CeremonyUser(WebAuthnService.Handle(live!.Provisional), Shown(live), string.Empty),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<WebAuthnCeremony>(error, ref failure));

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.CodeVerificationLifetime, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return (Result.Failure<CredentialCeremony>(failure), false);
        }

        // REG-SESS-001: one ceremony stands per session, replaced under the lock the
        // session is held by, so an abandoned challenge is never a second way in.
        live.Open(new StagedCeremony(kind, ceremony.Challenge, time.GetUtcNow() + lifetime));

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        return (
            Result.Success(new CredentialCeremony(
                ceremony.RelyingPartyId,
                ceremony.User,
                ceremony.Algorithms,
                ceremony.DiscoverableCredential,
                ceremony.Challenge)),
            true);
    }

    private async ValueTask<(Result<EnrolledCredential> Answer, bool Commits)> CreatedAsync(
        RegistrationSession? live,
        AuthenticatorAttestation attestation,
        CredentialLabel label,
        CancellationToken cancellationToken)
    {
        if (Enrolling(live) is Error refused)
        {
            return (Result.Failure<EnrolledCredential>(refused), false);
        }

        // AUTH-FACT-014: what the browser sends back is judged against the challenge
        // the session staged, which the request never carries.
        if (live!.Ceremony is not StagedCeremony open || open.HasExpired(time.GetUtcNow()))
        {
            return (Result.Failure<EnrolledCredential>(Error.From(ErrorCodes.FactorRejected)), false);
        }

        if (await AdmitsAsync(live, open.Kind, cancellationToken).ConfigureAwait(false) is Error unadmitted)
        {
            return (Result.Failure<EnrolledCredential>(unadmitted), false);
        }

        if (LabelHeld(live, open.Kind, label))
        {
            return (Result.Failure<EnrolledCredential>(Error.From(ErrorCodes.CredentialLabelInvalid)), false);
        }

        Error? failure = null;

        WebAuthnMaterial material = (await keys
                .ReadAsync(open.Kind, attestation, open.Challenge, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<WebAuthnMaterial>(error, ref failure));

        if (failure is not null)
        {
            return (Result.Failure<EnrolledCredential>(failure), false);
        }

        live.SpendCeremony();

        return await StagedAsync(
                live,
                new StagedCredential(AuthenticatorId.New(time), open.Kind, label, Totp: null, material),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<(Result<GeneratorEnrolment> Answer, bool Commits)> BegunAsync(
        RegistrationSession? live,
        CredentialLabel label,
        CancellationToken cancellationToken)
    {
        Factor generated = FactorCatalogue.Generated;

        if ((Enrolling(live) ?? await AdmitsAsync(live!, generated, cancellationToken).ConfigureAwait(false))
            is Error refused)
        {
            return (Result.Failure<GeneratorEnrolment>(refused), false);
        }

        if (LabelHeld(live!, generated, label))
        {
            return (Result.Failure<GeneratorEnrolment>(Error.From(ErrorCodes.CredentialLabelInvalid)), false);
        }

        var begun = new StagedGenerator(AuthenticatorId.New(time), label, TotpCodes.Draw(randomness));

        // REG-SESS-001: one unconfirmed generator stands per session, replaced under
        // the lock the session is held by.
        live!.Begin(begun);

        await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

        return (
            Result.Success(await generators
                .ShownAsync(begun.Id, begun.Secret, Shown(live), cancellationToken)
                .ConfigureAwait(false)),
            true);
    }

    private async ValueTask<(Result<EnrolledCredential> Answer, bool Commits)> ConfirmedAsync(
        RegistrationSession? live,
        AuthenticatorId credential,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        if (Enrolling(live) is Error refused)
        {
            return (Result.Failure<EnrolledCredential>(refused), false);
        }

        if (live!.Generator is not StagedGenerator begun || begun.Id != credential)
        {
            return (Result.Failure<EnrolledCredential>(Error.From(ErrorCodes.FactorRejected)), false);
        }

        Factor generated = FactorCatalogue.Generated;

        if (await AdmitsAsync(live, generated, cancellationToken).ConfigureAwait(false) is Error unadmitted)
        {
            return (Result.Failure<EnrolledCredential>(unadmitted), false);
        }

        // AUTH-FACT-007: one valid code of the secret is what makes it a credential, and
        // the step that code was accepted for is kept, so the same code is refused once
        // the account holds the generator (AUTH-FACT-005).
        if (await generators
                .AcceptsAsync(new TotpMaterial(begun.Secret, ConsumedStep: null), code, cancellationToken)
                .ConfigureAwait(false) is not long step)
        {
            return (Result.Failure<EnrolledCredential>(Error.From(ErrorCodes.CodeInvalid)), false);
        }

        live.SpendGenerator();

        return await StagedAsync(
                live,
                new StagedCredential(
                    begun.Id,
                    generated,
                    begun.Label,
                    new TotpMaterial(begun.Secret, step),
                    WebAuthn: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // What every enrolment at the security step ends with, under the lock its caller
    // holds: the credential staged, the recovery codes a second step beside a password
    // brings, and the step the session has reached.
    private async ValueTask<(Result<EnrolledCredential> Answer, bool Commits)> StagedAsync(
        RegistrationSession live,
        StagedCredential credential,
        CancellationToken cancellationToken)
    {
        live.Enrol(credential);

        Error? failure = null;

        Policy policy = (await PolicyAsync(live, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return (Result.Failure<EnrolledCredential>(failure), false);
        }

        (Error? unsettled, IReadOnlyList<string>? drawn) =
            await SettleAsync(live, cancellationToken).ConfigureAwait(false);

        if (unsettled is not null)
        {
            return (Result.Failure<EnrolledCredential>(unsettled), false);
        }

        DateTimeOffset now = time.GetUtcNow();

        // AUTH-RECOV-001: what the account would hold decides whether a second
        // credential is prompted for, exactly as it does once the account exists.
        List<Authenticator> enrolled =
            [.. live.Credentials
                .Where(staged => staged.ProviderSubject is null)
                .Select(staged => Enrolled(live.Provisional, staged, now))];

        return (
            Result.Success(new EnrolledCredential(
                credential.Id,
                Redundancy.Satisfied(enrolled) ? null : policy.CredentialRedundancy,
                drawn)),
            true);
    }

    // REG-SESS-006: the registration session stands in for an account's session at
    // the security step and at no other, so before that step an enrolment is answered
    // as one asked for under no session at all.
    private static Error? Enrolling(RegistrationSession? live) =>
        live?.Step is RegistrationStep.Security or RegistrationStep.Terms
            ? null
            : Error.From(ErrorCodes.SessionExpired);

    // What the session may enrol at all: the policy's own list, and, for a second
    // step, the password it is second to (AUTH-FACT-002b).
    private async ValueTask<Error?> AdmitsAsync(
        RegistrationSession live,
        Factor kind,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy policy = (await PolicyAsync(live, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        return !policy.LoginFactors.Contains(kind) || (SecondStep.Is(kind) && live.Password is null)
            ? Error.From(ErrorCodes.FactorNotPermitted)
            : null;
    }

    // AUTH-FACT-001 AC5: a label is held once per kind per account, so the session
    // stages no two of a kind under one label, compared in the canonical form the
    // account's own credentials are compared in.
    private static bool LabelHeld(RegistrationSession live, Factor kind, CredentialLabel label)
    {
        string canonical = CanonicalForm.Of(label.Value);

        foreach (StagedCredential staged in live.Credentials)
        {
            if (staged.Factor == kind
                && string.Equals(CanonicalForm.Of(staged.Label.Value), canonical, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // REG-PM-001: the name a ceremony and an authenticator app show is the staged
    // email, the first staged being the one the account takes as its primary
    // (REG-IDENT-002).
    private static string Shown(RegistrationSession live)
    {
        foreach (StagedIdentity staged in live.Identifiers)
        {
            if (staged.Kind is IdentifierKind.Email)
            {
                return staged.Canonical;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// The email step supplied by a social provider: the identity is staged as a
    /// credential the account is created with, and the address the provider supplied
    /// as the step's email.
    /// </summary>
    /// <param name="session">Which session.</param>
    /// <param name="provider">Which provider vouched.</param>
    /// <param name="providerSubject">The provider's own identifier for the person.</param>
    /// <param name="address">The address the provider supplied, where it supplied one.</param>
    /// <param name="label">What the credential is called until the person renames it.</param>
    /// <param name="source">The source of the request in hand, which a message it sends is counted against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// That the identity is linked to an account already, or the state with the
    /// identity staged, or the refusal.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <remarks>
    /// Implements REG-IDENT-008 and REG-IDENT-010. The provider's subject is the key and
    /// its address never is. An address the provider operates and no account holds is
    /// verified by the sign-in and locked; any other is staged as a typed one is, so a
    /// third-party address is sent one code and an address another account holds gets
    /// the ordinary answer while its holder is told.
    /// </remarks>
    public async ValueTask<Result<ProvidedRegistration>> ProvidedAsync(
        RegistrationSessionId session,
        Factor provider,
        [NeverLogged] string providerSubject,
        ProvidedAddress? address,
        CredentialLabel label,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(providerSubject);
        ArgumentNullException.ThrowIfNull(source);

        // REG-IDENT-008 AC3: an identity already linked makes the attempt a sign-in,
        // whatever the session holds.
        if (await authenticators.ByProviderAsync(provider, providerSubject, cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            return Result.Success(new ProvidedRegistration(Linked: true, State: null));
        }

        RegistrationSession? live =
            await LiveAsync(session, cancellationToken).ConfigureAwait(false);

        if (live is null)
        {
            return Result.Failure<ProvidedRegistration>(Error.From(ErrorCodes.SessionExpired));
        }

        if (!live.AgeAnswered)
        {
            return Result.Failure<ProvidedRegistration>(Error.From(ErrorCodes.AffirmationRequired));
        }

        if (live.Step is not RegistrationStep.Email)
        {
            return Result.Failure<ProvidedRegistration>(Error.From(ErrorCodes.RegistrationIncomplete));
        }

        var credential = new StagedCredential(
            AuthenticatorId.New(time),
            provider,
            label,
            Totp: null,
            WebAuthn: null,
            providerSubject);

        // REG-IDENT-010: the address an invitation bound stands, and the step goes on
        // with it; a provider that supplied no address leaves the step to a typed one.
        if (address is null || live.Bound(IdentifierKind.Email) is not null)
        {
            if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notBegun)
            {
                return Result.Failure<ProvidedRegistration>(notBegun);
            }

            live.Link(credential);

            await sessions.RecordAsync(live, cancellationToken).ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notCommitted)
            {
                return Result.Failure<ProvidedRegistration>(notCommitted);
            }

            return Result.Success(new ProvidedRegistration(Linked: false, State(live)));
        }

        return (await SuppliedAsync(live, credential, address, source, cancellationToken).ConfigureAwait(false))
            .Match(
                state => Result.Success(new ProvidedRegistration(Linked: false, state)),
                Result.Failure<ProvidedRegistration>);
    }

    private static Result<RegistrationState> Gone() =>
        Result.Failure<RegistrationState>(Error.From(ErrorCodes.SessionExpired));

    private static Result<RegistrationState> OutOfStep() =>
        Result.Failure<RegistrationState>(Error.From(ErrorCodes.RegistrationIncomplete));

    /// <summary>
    /// The band a date answers for, where the deployment takes no affirmation.
    /// </summary>
    /// <param name="adult">Whether the date made the person an adult.</param>
    /// <returns>The band.</returns>
    /// <remarks>
    /// Bootstrap derives the first administrator's answer by the same rule
    /// (PRIV-MINOR-001 AC3).
    /// </remarks>
    internal static AgeGroup Band(bool adult) => adult ? AgeGroup.Adult : AgeGroup.Minor;

    /// <summary>
    /// Whether a date of birth makes a person an adult on a given day (REG-PROF-002).
    /// </summary>
    /// <param name="dateOfBirth">The date entered.</param>
    /// <param name="today">The day the screen is answered on.</param>
    /// <returns>Whether the person is eighteen or older that day.</returns>
    /// <remarks>
    /// Bootstrap derives the first administrator's affirmation by the same rule
    /// (PRIV-MINOR-001 AC3).
    /// </remarks>
    internal static bool IsAdult(DateOnly dateOfBirth, DateOnly today) =>
        dateOfBirth <= today.AddYears(-Majority);

    private static IntegerSetting Maximum(IdentifierKind kind) =>
        kind is IdentifierKind.Email ? Settings.IdentifiersEmailMax : Settings.IdentifiersPhoneMax;

    // AUTH-ABUSE-001, REG-SESS-003 AC6: a registration's asks and tries are held to the
    // delay the source of the request in hand and the identifier's keyed hash have
    // earned, as a sign-in's are, never to a source stored when the session began, and
    // nothing about any account is part of it.
    private ThrottleAttempt Attempt(string source, string identifier) =>
        new(source, throttle.Identify(identifier, usernames: false));

    private async ValueTask<Error?> DelayedAsync(ThrottleAttempt attempt, CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan delay = (await throttle.DelayAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        return delay > TimeSpan.Zero ? Error.Throttled(time.GetUtcNow() + delay) : null;
    }

    // AUTH-ABUSE-001, CONV-DESIGN-003: the count of a pressed token that opens nothing
    // stands whatever the outcome, and the refusal writes nothing else; the throttle's
    // own unit of work is the outermost here, since nothing was begun for the press.
    private async ValueTask<Result<LinkLanding>> GoneAsync(string source, CancellationToken cancellationToken)
    {
        var attempt = new ThrottleAttempt(source, Identifier: null);

        if (await DelayedAsync(attempt, cancellationToken).ConfigureAwait(false) is Error delayed)
        {
            return Result.Failure<LinkLanding>(delayed);
        }

        return (await throttle.FailedAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(
                () => Result.Failure<LinkLanding>(Error.From(ErrorCodes.CodeExpired)),
                Result.Failure<LinkLanding>);
    }

    // The refusal is counted against the delay after it is decided, and answers as it
    // was decided unless the count itself failed. AUTH-ABUSE-001, CONV-DESIGN-003: the
    // count stands whatever the outcome, so the refusal that made it commits.
    private async ValueTask<(Result<RegistrationState> Answer, bool Commits)> CountedAsync(
        ThrottleAttempt attempt,
        Error refusal,
        CancellationToken cancellationToken) =>
        (await throttle.FailedAsync(attempt, cancellationToken).ConfigureAwait(false))
            .Match(
                () => (Result.Failure<RegistrationState>(refusal), true),
                error => (Result.Failure<RegistrationState>(error), false));

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
        string termsVersion,
        string noticeVersion,
        DateTimeOffset now,
        int emails,
        int phones,
        string? language)
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
            termsVersion,
            noticeVersion,
            emails,
            phones,
            language);
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

    // D-166 X3: an answer decided on the session's one document is decided on its row
    // under the lock, from the read to the write, so answers given at once are decided
    // one after another and none is written over another. CONV-DESIGN-003: the answer
    // says whether it commits, as a success and a refusal that keeps a count do; every
    // other refusal rolls back.
    private async ValueTask<Result<TAnswer>> HeldAsync<TAnswer>(
        RegistrationSessionId id,
        Func<RegistrationSession?, ValueTask<(Result<TAnswer> Answer, bool Commits)>> decide,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<TAnswer>(notBegun);
        }

        RegistrationSession? held = await sessions.FindForUpdateAsync(id, cancellationToken)
            .ConfigureAwait(false);

        (Result<TAnswer> decided, bool commits) = await decide(
                held is null || held.HasExpired(time.GetUtcNow()) ? null : held)
            .ConfigureAwait(false);

        if (!commits)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return decided;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => decided, Result.Failure<TAnswer>);
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
        (Error? failure, IReadOnlyList<string>? drawn) =
            await SettleAsync(session, cancellationToken).ConfigureAwait(false);

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<RegistrationState>(failure);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationState>(notCommitted);
        }

        return Result.Success(State(session, drawn));
    }

    // What settling writes, inside the unit of work its caller holds and ends: the
    // recovery codes a second step beside a password brings, drawn once, and the step
    // the session has reached.
    private async ValueTask<(Error? Failure, IReadOnlyList<string>? Drawn)> SettleAsync(
        RegistrationSession session,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy policy = (await PolicyAsync(session, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        if (failure is not null)
        {
            return (failure, null);
        }

        IReadOnlyList<string>? drawn = null;

        if (SecurityStep.PasswordSeconded(session) && session.RecoveryCodes is null)
        {
            PreparedRecoveryCodes set = (await recoveryCodes
                    .PrepareAsync(cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<PreparedRecoveryCodes>(error, ref failure));

            if (failure is not null)
            {
                return (failure, null);
            }

            session.StageRecoveryCodes(set.Hashes);
            drawn = set.Codes;
        }

        session.Reached(
            SecurityStep.Complete(session, policy.LoginFactors)
                ? RegistrationStep.Terms
                : RegistrationStep.Security);

        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);

        return (null, drawn);
    }

    // IDN-LIFE-009a: from the moment the token attaches, the invitation's organization
    // governs the registration as though the membership already stood.
    private async ValueTask<Result<Policy>> PolicyAsync(
        RegistrationSession session,
        CancellationToken cancellationToken) =>
        await policies
            .ForAsync(
                session.Provisional,
                (await InvitationAsync(session, cancellationToken).ConfigureAwait(false))?.Organization,
                cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<Invitation?> InvitationAsync(
        RegistrationSession session,
        CancellationToken cancellationToken) =>
        session.Invitation is InvitationId id
            ? await invitations.FindAsync(id, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The invitation a registration was opened by has no row.")
            : null;

    // REG-DOM-001 AC2: an email the person chooses at an invitation, rather than one
    // the invitation bound, is held to the inviting organization's lock.
    private async ValueTask<Error?> LockRefusedAsync(
        RegistrationSession session,
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken) =>
        kind is IdentifierKind.Email
            && await InvitationAsync(session, cancellationToken).ConfigureAwait(false) is { } invitation
            ? await locks.RefusedInAsync(invitation.Organization, Address(canonical), cancellationToken)
                .ConfigureAwait(false)
            : null;

    // REG-IDENT-010: a bound identifier is not changed, so the only value its step or
    // its Change takes is its own, and taking it sends a new code where none has
    // verified it yet. Nothing goes to it before its step is reached (REG-SESS-002).
    private async ValueTask<Result<RegistrationState>> ResentAsync(
        RegistrationSession session,
        StagedIdentity bound,
        string value,
        string source,
        CancellationToken cancellationToken)
    {
        RegistrationStep collecting =
            bound.Kind is IdentifierKind.Email ? RegistrationStep.Email : RegistrationStep.Phone;

        if (session.Step < collecting)
        {
            return OutOfStep();
        }

        if (bound.IsVerified
            || Canonical(bound.Kind, value) is not (_, string canonical)
            || !string.Equals(canonical, bound.Canonical, StringComparison.Ordinal))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierLocked));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationState>(notBegun);
        }

        if (await DispatchAsync(session, bound, source, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<RegistrationState>(refused);
        }

        if (session.Step is RegistrationStep.Phone && bound.Kind is IdentifierKind.Phone)
        {
            session.Reached(RegistrationStep.Confirm);
        }

        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationState>(notCommitted);
        }

        return Result.Success(State(session));
    }

    private async ValueTask<Result<RegistrationState>> CollectAsync(
        RegistrationSession session,
        IdentifierKind kind,
        string value,
        bool isExtra,
        string source,
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

        if (await LockRefusedAsync(session, kind, canonical, cancellationToken).ConfigureAwait(false)
            is Error outside)
        {
            return Result.Failure<RegistrationState>(outside);
        }

        var staged = StagedIdentity.Of(
            IdentifierId.New(time),
            kind,
            entered,
            canonical,
            isLocked: false,
            isExtra);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationState>(notBegun);
        }

        session.Stage(staged);

        if (await DispatchAsync(session, staged, source, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<RegistrationState>(refused);
        }

        if (!isExtra)
        {
            session.Reached(kind is IdentifierKind.Email
                ? RegistrationStep.Phone
                : RegistrationStep.Confirm);
        }

        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationState>(notCommitted);
        }

        return Result.Success(State(session));
    }

    // REG-IDENT-008: the address comes from the provider rather than the person, and
    // is held to every rule a typed one is. Only an address the provider operates and
    // no account holds is verified by the sign-in; one another account holds takes
    // the typed path, so nothing is sent here and its holder is told (AC4).
    private async ValueTask<Result<RegistrationState>> SuppliedAsync(
        RegistrationSession session,
        StagedCredential credential,
        ProvidedAddress address,
        string source,
        CancellationToken cancellationToken)
    {
        if (Canonical(IdentifierKind.Email, address.Entered) is not (string entered, string canonical))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierInvalid));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(canonical))
        {
            return Result.Failure<RegistrationState>(Error.From(ErrorCodes.IdentifierMixedScript));
        }

        if (await LockRefusedAsync(session, IdentifierKind.Email, canonical, cancellationToken)
                .ConfigureAwait(false) is Error outside)
        {
            return Result.Failure<RegistrationState>(outside);
        }

        bool vouched = address.Operated
            && !await TakenAsync(IdentifierKind.Email, canonical, cancellationToken)
                .ConfigureAwait(false);

        var staged = StagedIdentity.Of(
            IdentifierId.New(time),
            IdentifierKind.Email,
            entered,
            canonical,
            isLocked: vouched,
            isExtra: false);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<RegistrationState>(notBegun);
        }

        session.Link(credential);
        session.Stage(staged);

        if (vouched)
        {
            staged.Verify(time.GetUtcNow());
        }
        else if (await DispatchAsync(session, staged, source, cancellationToken).ConfigureAwait(false)
                 is Error refused)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<RegistrationState>(refused);
        }

        session.Reached(RegistrationStep.Phone);

        await sessions.RecordAsync(session, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<RegistrationState>(notCommitted);
        }

        return Result.Success(State(session));
    }

    // The three cases are one path: the lookup decides only whether the code goes to
    // the person registering, the holder is told instead, or, for a value held out of
    // reach for its owner's undo, nothing is sent and nobody is told; the caller cannot
    // tell which happened (REG-SESS-005, REG-IDENT-006, AUTH-ABUSE-003).
    private async ValueTask<Error?> DispatchAsync(
        RegistrationSession session,
        StagedIdentity staged,
        string source,
        CancellationToken cancellationToken)
    {
        SubjectId? owner = await directory
            .OwnerAsync(staged.Kind, staged.Canonical, cancellationToken)
            .ConfigureAwait(false);

        if (owner is SubjectId holder)
        {
            return await WithheldAsync(session, staged, source, cancellationToken).ConfigureAwait(false)
                ?? await TellHolderAsync(session, staged, holder, source, cancellationToken).ConfigureAwait(false);
        }

        if (await directory
                .IsReservedAsync(staged.Kind, staged.Canonical, time.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false))
        {
            return await WithheldAsync(session, staged, source, cancellationToken).ConfigureAwait(false);
        }

        return await SendCodeAsync(session, staged, source, cancellationToken).ConfigureAwait(false);
    }

    // AUTH-FACT-004, AUTH-ABUSE-004: an ask of a code for a held or reserved value is
    // judged and counted against the restrictions as its message would be, and refused
    // by them alike; admitted, the value is given a verification-code record that
    // lives and counts as a sent code's does and that no code matches. Nothing is
    // sent, and no link stands for a press to prove.
    private async ValueTask<Error?> WithheldAsync(
        RegistrationSession session,
        StagedIdentity staged,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        Result drawn = await restrictions
            .DrawAsync(
                new OutboundMessage(
                    Destination(staged),
                    MessageKind.VerificationLink,
                    RestrictionPurpose.Verification,
                    source,
                    RecipientLanguage.Found(session.Language, languages)),
                cancellationToken)
            .ConfigureAwait(false);

        if (drawn.Match(() => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        staged.Unlinked();

        return (await codes.WithholdAsync(Holder(session.Id, staged.Id), cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
    }

    // AUTH-FACT-004 (D-166, 115): the verification-code record of a staged identifier
    // is held against a fingerprint of the registration session and the identifier.
    private static byte[] Holder(RegistrationSessionId session, IdentifierId staged)
    {
        Span<byte> named = stackalloc byte[32];

        _ = session.Value.TryWriteBytes(named);
        _ = staged.Value.TryWriteBytes(named[16..]);

        return SHA256.HashData(named);
    }

    // The codes a session's staged identifiers had outstanding go with the session.
    private async ValueTask EndCodesAsync(RegistrationSession session, CancellationToken cancellationToken)
    {
        foreach (StagedIdentity staged in session.Identifiers)
        {
            await codes.EndAsync(Holder(session.Id, staged.Id), cancellationToken).ConfigureAwait(false);
        }
    }

    // Whether a value belongs to an account or is held out of reach for an undo, which
    // registration answers alike (REG-SESS-005, REG-IDENT-006).
    private async ValueTask<bool> TakenAsync(
        IdentifierKind kind,
        string canonical,
        CancellationToken cancellationToken) =>
        await directory.OwnerAsync(kind, canonical, cancellationToken).ConfigureAwait(false) is not null
        || await directory
            .IsReservedAsync(kind, canonical, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask<Error?> SendCodeAsync(
        RegistrationSession session,
        StagedIdentity staged,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        // AUTH-ABUSE-004: the code is issued and its message undertaken in the caller's
        // unit of work, so a send the restrictions refuse leaves no code behind it.
        string code = (await codes.IssueAsync(Holder(session.Id, staged.Id), cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<string>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        var link = OpaqueToken.Draw(randomness);

        Result<SendReference> sent = await sending
            .UndertakeAsync(
                new OutboundMessage(
                    Destination(staged),
                    MessageKind.VerificationLink,
                    RestrictionPurpose.Verification,
                    source,
                    RecipientLanguage.Found(session.Language, languages))
                {
                    Values = new Dictionary<string, string>(capacity: 2, StringComparer.Ordinal)
                    {
                        ["code"] = code,
                        ["link"] = landing.Of(LinkKind.Registration, link.Value),
                    },
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (sent.Match(_ => (Error?)null, error => error) is Error refused)
        {
            return refused;
        }

        staged.Linked(link.Fingerprint());

        return null;
    }

    private async ValueTask<Error?> TellHolderAsync(
        RegistrationSession session,
        StagedIdentity staged,
        SubjectId holder,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.AbuseNonexistentWindow, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        // D-166 X3: the holder is told once however many ask at once.
        await notices.HoldAsync(staged.Canonical, cancellationToken).ConfigureAwait(false);

        if (!await notices
            .FirstAsync(staged.Canonical, time.GetUtcNow(), window, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        string? settled = await directory.LanguageAsync(holder, cancellationToken).ConfigureAwait(false);

        // The holder is told and the person registering is told nothing: the message
        // names no requester and carries neither a code nor a link (REG-SESS-005 AC2).
        Result<SendReference> sent = await sending
            .UndertakeAsync(
                new OutboundMessage(
                    Destination(staged),
                    MessageKind.AccountExists,
                    RestrictionPurpose.Notification,
                    source,
                    RecipientLanguage.Of(settled, session.Language, languages))
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
            // REG-IDENT-008: a provider's identity is written with the subject the
            // provider knows the person by, which is what a later sign-in matches.
            if (staged.ProviderSubject is string providerSubject)
            {
                await authenticators
                    .LinkAsync(
                        Authenticator.Linked(staged.Id, session.Provisional, staged.Factor, staged.Label, now),
                        providerSubject,
                        cancellationToken)
                    .ConfigureAwait(false);

                continue;
            }

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
