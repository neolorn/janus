using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Recovery;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Credentials;

/// <summary>
/// What an account does to the credentials it holds: sets a password, creates a key,
/// enrols a generator, takes a set of codes, and gives one up.
/// </summary>
/// <param name="keys">What creates and records a WebAuthn credential.</param>
/// <param name="generators">What enrols a code generator.</param>
/// <param name="codes">What issues a set of single-use codes.</param>
/// <param name="passwords">What sets a password.</param>
/// <param name="losses">What suspends a credential for a notified window.</param>
/// <param name="enrolments">Where an enrolment session is read and ended.</param>
/// <param name="stepUp">What an operation asks of the session it arrived on.</param>
/// <param name="policies">What policy governs the account.</param>
/// <param name="ceremonies">Where the creation ceremony an account has open is held.</param>
/// <param name="authenticators">Where the account's credentials are read and written.</param>
/// <param name="held">Where the account's password is read.</param>
/// <param name="identifiers">Where the channels a notice reaches are read.</param>
/// <param name="sessions">Where the account's other sessions are ended.</param>
/// <param name="sending">Where a message goes out.</param>
/// <param name="audit">Where what became of a credential is recorded.</param>
/// <param name="configuration">Where the lifetimes and the service name come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, AUTH-FACT-001, AUTH-FACT-002b, AUTH-FACT-007,
/// AUTH-FACT-008, AUTH-STEP-007, AUTH-RECOV-001 and AUTH-RECOV-006. Two authorities
/// reach the same operations: a session, which every gate applies to, and the
/// enrolment session an approved recovery opened, which stands in for the gates
/// somebody locked out could never pass (D-148) and ends the moment the enrolment
/// completes.
/// </remarks>
internal sealed class CredentialService(
    WebAuthnService keys,
    TotpService generators,
    RecoveryCodeService codes,
    PasswordService passwords,
    LossReports losses,
    EnrolmentSessions enrolments,
    StepUpGuard stepUp,
    PolicyResolution policies,
    IKeyCeremonyStore ceremonies,
    IAuthenticatorStore authenticators,
    IPasswordStore held,
    IIdentifierDirectory identifiers,
    ISessionStore sessions,
    SendingService sending,
    ICredentialAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time) : ICredentials
{
    private static readonly AuditAction Enrolled = AuditAction.Parse("auth.credential.enrolled");

    private static readonly AuditAction Removed = AuditAction.Parse("auth.credential.removed");

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async ValueTask<Result> SetPasswordAsync(
        CredentialAuthority authority,
        string password,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        Acting acting = (await ActingAsync(authority, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Acting>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (await GateAsync(
                acting,
                StepUpAction.PasswordSet,
                FactorCatalogue.Password,
                cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure(gate);
        }

        _ = (await SetAsync(acting.Subject, password, cancellationToken).ConfigureAwait(false))
            .Match(() => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // IDN-LIFE-008: a changed password ends what was held under the old one.
        await EndOthersAsync(acting, cancellationToken).ConfigureAwait(false);

        _ = await TellAsync(acting.Subject, MessageKind.SecurityNotice, source, cancellationToken)
            .ConfigureAwait(false);

        await CompletedAsync(acting, cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<CredentialCeremony>> BeginKeyAsync(
        CredentialAuthority authority,
        Factor kind,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Acting acting = (await ActingAsync(authority, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Acting>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<CredentialCeremony>(failure);
        }

        if (await AdmitsAsync(acting.Subject, kind, cancellationToken).ConfigureAwait(false)
            is Error refusal)
        {
            return Result.Failure<CredentialCeremony>(refusal);
        }

        if (await GateAsync(acting, StepUpAction.FactorEnrol, kind, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure<CredentialCeremony>(gate);
        }

        return await OpenAsync(acting.Subject, kind, upgrading: null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<CredentialCeremony>> UpgradeKeyAsync(
        CredentialAuthority authority,
        AuthenticatorId credential,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Acting acting = (await ActingAsync(authority, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Acting>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<CredentialCeremony>(failure);
        }

        Authenticator? upgrading = await authenticators.FindAsync(credential, cancellationToken)
            .ConfigureAwait(false);

        if (upgrading is null || upgrading.Subject != acting.Subject)
        {
            return Result.Failure<CredentialCeremony>(Error.From(ErrorCodes.CredentialNotFound));
        }

        // AUTH-FACT-002b: the upgrade replaces a key the authenticator does not keep
        // with one it does, so an entry that is not such a key has nothing to upgrade.
        if (FactorCatalogue.Of(upgrading.Factor) is not { IsWebAuthn: true, IsDiscoverable: false })
        {
            return Result.Failure<CredentialCeremony>(Error.From(ErrorCodes.FactorRejected));
        }

        Factor discoverable = FactorCatalogue.Discoverable;

        if (await GateAsync(acting, StepUpAction.FactorEnrol, discoverable, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure<CredentialCeremony>(gate);
        }

        return await OpenAsync(acting.Subject, discoverable, credential, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<EnrolledCredential>> CompleteKeyAsync(
        CredentialAuthority authority,
        AuthenticatorAttestation attestation,
        string label,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        Acting acting = (await ActingAsync(authority, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Acting>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<EnrolledCredential>(failure);
        }

        if (!CredentialLabel.TryParse(label, out CredentialLabel named))
        {
            return Result.Failure<EnrolledCredential>(Error.From(ErrorCodes.CredentialLabelInvalid));
        }

        KeyCeremony? ceremony = await ceremonies.FindAsync(acting.Subject, cancellationToken)
            .ConfigureAwait(false);

        if (ceremony is null || ceremony.HasExpired(time.GetUtcNow()))
        {
            return Result.Failure<EnrolledCredential>(Error.From(ErrorCodes.FactorRejected));
        }

        if (await GateAsync(acting, StepUpAction.FactorEnrol, ceremony.Kind, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure<EnrolledCredential>(gate);
        }

        AuthenticatorId enrolled = (await keys
                .EnrolAsync(
                    acting.Subject,
                    ceremony.Kind,
                    named,
                    attestation,
                    ceremony.Challenge,
                    ceremony.Upgrading,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<AuthenticatorId>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<EnrolledCredential>(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await ceremonies.RemoveAsync(acting.Subject, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return await SettledAsync(acting, enrolled, ceremony.Kind, source, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<GeneratorEnrolment>> BeginGeneratorAsync(
        CredentialAuthority authority,
        string label,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(label);

        Error? failure = null;

        Acting acting = (await ActingAsync(authority, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Acting>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<GeneratorEnrolment>(failure);
        }

        if (!CredentialLabel.TryParse(label, out CredentialLabel named))
        {
            return Result.Failure<GeneratorEnrolment>(Error.From(ErrorCodes.CredentialLabelInvalid));
        }

        Factor generated = FactorCatalogue.Generated;

        if (await AdmitsAsync(acting.Subject, generated, cancellationToken).ConfigureAwait(false)
            is Error refusal)
        {
            return Result.Failure<GeneratorEnrolment>(refusal);
        }

        if (await GateAsync(acting, StepUpAction.FactorEnrol, generated, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure<GeneratorEnrolment>(gate);
        }

        TotpEnrolment begun = (await generators
                .BeginAsync(acting.Subject, named, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TotpEnrolment>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<GeneratorEnrolment>(failure);
        }

        byte[] secret = begun.Secret.ToArray();

        try
        {
            string text = TotpCodes.Text(secret);

            return Result.Success(new GeneratorEnrolment(
                begun.Id,
                text,
                TotpCodes.Address(
                    await IssuerAsync(cancellationToken).ConfigureAwait(false),
                    await AccountAsync(acting.Subject, cancellationToken).ConfigureAwait(false),
                    text)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result<EnrolledCredential>> ConfirmGeneratorAsync(
        CredentialAuthority authority,
        AuthenticatorId credential,
        string code,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        Acting acting = (await ActingAsync(authority, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Acting>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<EnrolledCredential>(failure);
        }

        _ = (await generators
                .ConfirmAsync(acting.Subject, credential, code, cancellationToken)
                .ConfigureAwait(false))
            .Match(() => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<EnrolledCredential>(failure);
        }

        return await SettledAsync(
                acting,
                credential,
                FactorCatalogue.Generated,
                source,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<GeneratedRecoveryCodes>> GenerateRecoveryCodesAsync(
        CredentialAuthority authority,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Acting acting = (await ActingAsync(authority, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Acting>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<GeneratedRecoveryCodes>(failure);
        }

        if (await GateAsync(acting, StepUpAction.RecoveryCodesGenerate, null, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure<GeneratedRecoveryCodes>(gate);
        }

        // AUTH-RECOV-006: the codes stand in for a second step, and a passkey-only
        // account has no second step for them to stand in for.
        if (!await SecondStep.AvailableAsync(held, acting.Subject, cancellationToken)
            .ConfigureAwait(false))
        {
            return Result.Failure<GeneratedRecoveryCodes>(Error.From(ErrorCodes.FactorNotPermitted));
        }

        IReadOnlyList<string> generated = (await codes
                .GenerateAsync(acting.Subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<string>>(error, ref failure));

        return failure is not null
            ? Result.Failure<GeneratedRecoveryCodes>(failure)
            : Result.Success(new GeneratedRecoveryCodes(generated, time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<LossReported?>> RemoveAsync(
        CredentialAuthority authority,
        AuthenticatorId credential,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        Acting acting = (await ActingAsync(authority, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Acting>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<LossReported?>(failure);
        }

        if (await GateAsync(acting, StepUpAction.FactorRemove, null, cancellationToken)
                .ConfigureAwait(false)
            is Error gate)
        {
            return Result.Failure<LossReported?>(gate);
        }

        Authenticator? going = await authenticators.FindAsync(credential, cancellationToken)
            .ConfigureAwait(false);

        if (going is null || going.Subject != acting.Subject)
        {
            return Result.Failure<LossReported?>(Error.From(ErrorCodes.CredentialNotFound));
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(acting.Subject, cancellationToken)
            .ConfigureAwait(false);

        bool password = await SecondStep.AvailableAsync(held, acting.Subject, cancellationToken)
            .ConfigureAwait(false);

        // AUTH-STEP-006, AUTH-RECOV-007: a removal that would leave the account
        // reaching less than it does now runs the notified window instead, so the
        // credential is refused at once and gone only once somebody has been told.
        if (Lowers(enrolled, going, password))
        {
            return (await losses.SuspendAsync(going, source, cancellationToken).ConfigureAwait(false))
                .Match(
                    reported => Result.Success<LossReported?>(reported),
                    Result.Failure<LossReported?>);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await authenticators.RemoveAsync(credential, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(Removed, acting.Subject, credential, now, cancellationToken)
            .ConfigureAwait(false);

        _ = await TellAsync(acting.Subject, MessageKind.SecurityNotice, source, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<LossReported?>(null);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // What the account would reach without this credential, against what it reaches
    // with it: the comparison AUTH-STEP-006 turns on.
    private static bool Lowers(
        IReadOnlyList<Authenticator> enrolled,
        Authenticator going,
        bool password)
    {
        AssuranceLevel standing = StepUp.Reachable(HeldFactors.Of(enrolled, password).Standing).Level;
        AssuranceLevel without = StepUp
            .Reachable(HeldFactors
                .Of([.. enrolled.Where(credential => credential.Id != going.Id)], password)
                .Standing)
            .Level;

        return without < standing;
    }

    private static SendDestination? Destination(HeldIdentifier identifier)
    {
        if (identifier.Kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? SendDestination.Of(address)
                : null;
        }

        return PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
            ? SendDestination.Of(number)
            : null;
    }

    // Who is acting, and under what: a session the gates apply to, or the enrolment
    // session an approved recovery opened, which is read afresh so that one that has
    // run out reaches nothing (D-147).
    private async ValueTask<Result<Acting>> ActingAsync(
        CredentialAuthority authority,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authority);

        if (authority.Enrolment is EnrolmentSessionId opened)
        {
            return await enrolments.FindAsync(opened, cancellationToken).ConfigureAwait(false)
                is EnrolmentSession enrolment
                ? Result.Success(new Acting(enrolment.Subject, Session: null, opened))
                : Result.Failure<Acting>(Error.From(ErrorCodes.EnrolmentTokenInvalid));
        }

        return authority.Context?.Effective is SubjectId subject && authority.Session is SessionId live
            ? Result.Success(new Acting(subject, live, Enrolment: null))
            : Result.Failure<Acting>(Error.From(ErrorCodes.Denied));
    }

    // AUTH-STEP-007: the gate applies to a session and is stated as the lower of what
    // the account reaches and what the credential contributes. An enrolment session
    // passes none of them, because the approver stood in for them before it existed.
    private async ValueTask<Error?> GateAsync(
        Acting acting,
        StepUpAction action,
        Factor? enrolling,
        CancellationToken cancellationToken)
    {
        if (acting.Session is not SessionId live)
        {
            return null;
        }

        return enrolling is Factor creating
            ? await stepUp
                .PassedToEnrolAsync(acting.Subject, live, action, creating, cancellationToken)
                .ConfigureAwait(false)
            : await stepUp
                .PassedAsync(acting.Subject, live, action, cancellationToken)
                .ConfigureAwait(false);
    }

    // What the account may enrol at all: the policy's own list, and, for a second
    // step, the password it is second to (AUTH-FACT-002b).
    private async ValueTask<Error?> AdmitsAsync(
        SubjectId subject,
        Factor kind,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        if (!policy.LoginFactors.Contains(kind))
        {
            return Error.From(ErrorCodes.FactorNotPermitted);
        }

        return SecondStep.Is(kind)
            && !await SecondStep.AvailableAsync(held, subject, cancellationToken).ConfigureAwait(false)
                ? Error.From(ErrorCodes.FactorNotPermitted)
                : null;
    }

    // One ceremony stands per account: opening another replaces it, so an abandoned
    // challenge is never a second way in (AUTH-FACT-014).
    private async ValueTask<Result<CredentialCeremony>> OpenAsync(
        SubjectId subject,
        Factor kind,
        AuthenticatorId? upgrading,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        WebAuthnCeremony ceremony = (await keys.BeginAsync(kind, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<WebAuthnCeremony>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<CredentialCeremony>(failure);
        }

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.CodeVerificationLifetime, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<CredentialCeremony>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await ceremonies
            .ReplaceAsync(
                KeyCeremony.Existing(
                    subject,
                    kind,
                    ceremony.Challenge,
                    upgrading,
                    now,
                    now + lifetime),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new CredentialCeremony(
            ceremony.RelyingPartyId,
            ceremony.Algorithms,
            ceremony.DiscoverableCredential,
            ceremony.Challenge));
    }

    // What every completed enrolment does: the codes a second step beside a password
    // brings with it, the prompt for a credential that would survive the device, the
    // notice on every channel, and the end of an enrolment session.
    private async ValueTask<Result<EnrolledCredential>> SettledAsync(
        Acting acting,
        AuthenticatorId credential,
        Factor kind,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;
        IReadOnlyList<string>? generated = null;

        // AUTH-RECOV-006: always generated when a second step is enrolled beside a
        // password, and never on an account that holds none.
        if (SecondStep.Is(kind)
            && await SecondStep.AvailableAsync(held, acting.Subject, cancellationToken)
                .ConfigureAwait(false))
        {
            generated = (await codes.GenerateAsync(acting.Subject, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Withheld<IReadOnlyList<string>>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<EnrolledCredential>(failure);
            }
        }

        Policy policy = (await policies.ForAsync(acting.Subject, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<EnrolledCredential>(failure);
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(acting.Subject, cancellationToken)
            .ConfigureAwait(false);

        await audit
            .RecordedAsync(Enrolled, acting.Subject, credential, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        // AUTH-STEP-007 AC1: every recorded channel hears of it, and the enrolling
        // session is not one of them.
        _ = await TellAsync(acting.Subject, MessageKind.CredentialEnrolled, source, cancellationToken)
            .ConfigureAwait(false);

        await CompletedAsync(acting, cancellationToken).ConfigureAwait(false);

        return Result.Success(new EnrolledCredential(
            credential,
            Redundancy.Satisfied(enrolled) ? null : policy.CredentialRedundancy,
            generated));
    }

    // D-148: completing the enrolment ends the enrolment session, and what was set is
    // used by signing in with it.
    private async ValueTask CompletedAsync(Acting acting, CancellationToken cancellationToken)
    {
        if (acting.Enrolment is EnrolmentSessionId opened)
        {
            await enrolments.EndAsync(opened, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result> SetAsync(
        SubjectId subject,
        string password,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers channels = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var words = new List<string>(channels.All.Count);

        foreach (HeldIdentifier identifier in channels.All)
        {
            words.Add(identifier.Canonical);
        }

        byte[] presented = Encoding.UTF8.GetBytes(password);

        try
        {
            // AUTH-PASS-001a: the floor follows what the account reaches once this
            // password stands beside what it already holds.
            return (await passwords
                    .SetAsync(
                        subject,
                        presented,
                        words,
                        StepUp.Reachable(HeldFactors.Of(enrolled, password: true).Standing).Level,
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match(_ => Result.Success(), Result.Failure);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presented);
        }
    }

    // The account's other sessions end; the one asking is left alone, and an
    // enrolment session leaves none standing because nobody is holding one.
    private async ValueTask EndOthersAsync(Acting acting, CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        if (acting.Session is not SessionId keeping)
        {
            await sessions.EndAccountAsync(acting.Subject, now, cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        IReadOnlyList<Session> live = await sessions
            .LiveOfAsync(acting.Subject, now, cancellationToken)
            .ConfigureAwait(false);

        SessionId? kept = null;

        foreach (Session session in live)
        {
            if (session.Id == keeping)
            {
                kept = session.Spine;
            }
        }

        var ended = new HashSet<SessionId>();

        foreach (Session session in live)
        {
            if (session.Spine != kept && ended.Add(session.Spine))
            {
                await sessions.EndSpineAsync(session.Spine, now, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<int> TellAsync(
        SubjectId subject,
        MessageKind message,
        string source,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers channels = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        string language = await LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        int told = 0;

        foreach (HeldIdentifier identifier in channels.NoticeSet)
        {
            if (Destination(identifier) is not SendDestination destination)
            {
                continue;
            }

            // A notice one destination refuses still reaches the rest: the set exists
            // so that no one channel can silence it.
            Result<SendReference> sent = await sending
                .SendAsync(
                    new SendRequest(
                        destination,
                        message,
                        RestrictionPurpose.Notification,
                        source,
                        language)
                    {
                        Subject = subject,
                        Values = Nothing,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
    }

    private async ValueTask<string> LanguageAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        if (await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false)
            is string settled)
        {
            return settled;
        }

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => (IReadOnlyList<string>)[]);

        return languages.Count > 0 ? languages[0] : string.Empty;
    }

    private async ValueTask<string> IssuerAsync(CancellationToken cancellationToken) =>
        (await configuration.ReadAsync(Settings.ServiceName, cancellationToken).ConfigureAwait(false))
            .Match(value => value, _ => string.Empty);

    // What the authenticator app shows beside the code: the account's primary
    // identifier, and the first it holds where it has settled no primary.
    private async ValueTask<string> AccountAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers channels = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        foreach (HeldIdentifier identifier in channels.All)
        {
            if (identifier.IsPrimary)
            {
                return identifier.Canonical;
            }
        }

        return channels.All.Count > 0 ? channels.All[0].Canonical : string.Empty;
    }

    private sealed record Acting(
        SubjectId Subject,
        SessionId? Session,
        EnrolmentSessionId? Enrolment);
}
