using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Recovery;

/// <summary>
/// The way back into an account: the link a person asks for themselves, the
/// re-enrolment an approver opens for them, and the session that link stands for.
/// </summary>
/// <param name="links">Where the links that have gone out are held.</param>
/// <param name="approvals">Where the approvals standing against an account are held.</param>
/// <param name="losses">What runs a report that a credential is gone.</param>
/// <param name="audit">Where an approval is recorded.</param>
/// <param name="identifiers">Where an identifier is resolved to an account.</param>
/// <param name="accounts">Where the account's standing is read.</param>
/// <param name="authenticators">Where the account's credentials are read.</param>
/// <param name="passwords">What screens and sets a password.</param>
/// <param name="policies">What policy governs the account.</param>
/// <param name="memberships">Where the approver's own organizations are read.</param>
/// <param name="sessions">What ends the sessions a changed credential invalidates.</param>
/// <param name="stepUp">What the approver's session has to have proved.</param>
/// <param name="gate">What decides whether the approver may approve at all.</param>
/// <param name="sending">Where a message goes out.</param>
/// <param name="nonExistence">What answers an address no account holds.</param>
/// <param name="throttle">The progressive delay.</param>
/// <param name="events">Where the anomaly alerts go.</param>
/// <param name="configuration">Where the lifetimes and the limits come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a token is drawn from.</param>
/// <remarks>
/// Implements AUTH-RECOV-002, AUTH-RECOV-002a, AUTH-RECOV-003, AUTH-RECOV-004,
/// AUTH-RECOV-005 and AUTH-ABUSE-003. Asking always succeeds: an identifier no
/// account holds, an account whose policy closes the route and an account that has
/// asked too often today each produce the answer one that can produce, and differ
/// only in what reaches the channel.
/// </remarks>
internal sealed class RecoveryService(
    IRecoveryLinkStore links,
    IRecoveryApprovalStore approvals,
    LossReports losses,
    IRecoveryAudit audit,
    IIdentifierDirectory identifiers,
    IAccountDirectory accounts,
    IAuthenticatorStore authenticators,
    PasswordService passwords,
    PolicyResolution policies,
    IMembershipLookup memberships,
    SessionService sessions,
    StepUpGuard stepUp,
    IAccessGate gate,
    SendingService sending,
    NonExistenceNotice nonExistence,
    ThrottleService throttle,
    IEvents events,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : IRecovery
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0, StringComparer.Ordinal);

    // Both rate limits of AUTH-RECOV-002 are stated per day, which is the one window
    // they are counted over (chapter 10 section 4.4).
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    /// <inheritdoc/>
    public async ValueTask<Result> BeginAsync(
        string identifier,
        string language,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(source);

        Error? failure = null;

        bool usernames = (await configuration
                .ReadAsync(Settings.IdentifiersUsernameEnabled, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        Channel? channel = Read(identifier, usernames);
        SubjectId? owner = channel is null
            ? null
            : await identifiers
                .OwnerAsync(channel.Kind, channel.Canonical, cancellationToken)
                .ConfigureAwait(false);

        TimeSpan delay = (await throttle
                .DelayAsync(
                    new ThrottleAttempt(source, identifier) { Account = owner },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (delay > TimeSpan.Zero)
        {
            return Result.Failure(Error.From(
                ErrorCodes.Throttled,
                "retryAt",
                JsonSerializer.SerializeToElement(time.GetUtcNow() + delay)));
        }

        if (channel is null)
        {
            return Result.Success();
        }

        return owner is SubjectId subject
            ? await IssueAsync(subject, channel, language, source, cancellationToken)
                .ConfigureAwait(false)
            : await TellAsync(channel, language, source, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CompleteAsync(
        string token,
        string password,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(source);

        DateTimeOffset now = time.GetUtcNow();
        RecoveryLink? link = await FindAsync(token, cancellationToken).ConfigureAwait(false);

        if (link is null || link.Purpose is not RecoveryPurpose.SelfService || link.SpentAt is not null)
        {
            return Result.Failure(Error.From(ErrorCodes.RecoveryTokenInvalid));
        }

        if (link.HasExpired(now))
        {
            return Result.Failure(Error.From(ErrorCodes.RecoveryTokenExpired));
        }

        Error? failure = null;

        _ = (await SetAsync(link.Subject, password, cancellationToken).ConfigureAwait(false))
            .Match(() => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        link.Spend(session: null, now);

        await links.RecordAsync(link, cancellationToken).ConfigureAwait(false);

        // D-140: recovery is the way back for an account its own holder deactivated,
        // and finishing it is what stands it up again.
        if (await accounts.SuspendedByAsync(link.Subject, cancellationToken).ConfigureAwait(false)
            is SuspensionOrigin.Self)
        {
            await accounts.ReinstateAsync(link.Subject, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        // IDN-LIFE-008: a changed password ends every session that was held under the
        // old one, wherever it is held.
        _ = (await sessions.EndAccountAsync(link.Subject, cancellationToken).ConfigureAwait(false))
            .Match(() => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        _ = await NotifyAsync(
                link.Subject,
                MessageKind.SecurityNotice,
                Nothing,
                excluded: null,
                source,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<ApprovedRecovery>> ApproveAsync(
        AccessContext context,
        SessionId session,
        SubjectId subject,
        string reason,
        string channelUsed,
        string language,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(channelUsed);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId approver)
        {
            return Result.Failure<ApprovedRecovery>(Error.From(ErrorCodes.Denied));
        }

        // AUTH-RECOV-002a AC2: the refusal is here and not at the endpoint, so no
        // caller of the library reaches the approval by another road.
        if (approver == subject)
        {
            return Result.Failure<ApprovedRecovery>(Error.From(ErrorCodes.RecoverySelfApproval));
        }

        if (await RefusedAsync(context, approver, cancellationToken).ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<ApprovedRecovery>(denied);
        }

        if (await stepUp
                .PassedAsync(approver, session, StepUpAction.RecoveryApprove, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure<ApprovedRecovery>(closed);
        }

        if (reason.Trim().Length is 0)
        {
            return Result.Failure<ApprovedRecovery>(Error.From(ErrorCodes.RecoveryReasonRequired));
        }

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        // AUTH-RECOV-003: the channel is one the account already holds, proved
        // against what is recorded rather than against what was typed.
        if (Recorded(held, channelUsed) is not Channel channel)
        {
            return Result.Failure<ApprovedRecovery>(
                Error.From(ErrorCodes.RecoveryChannelNotOnAccount));
        }

        return await StandAsync(
                approver,
                subject,
                reason.Trim(),
                channel,
                language,
                source,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<EnrolmentSession>> BeginEnrolmentAsync(
        string token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        DateTimeOffset now = time.GetUtcNow();
        RecoveryLink? link = await FindAsync(token, cancellationToken).ConfigureAwait(false);

        if (link is null || link.Purpose is not RecoveryPurpose.Enrolment || link.SpentAt is not null)
        {
            return Result.Failure<EnrolmentSession>(Error.From(ErrorCodes.EnrolmentTokenInvalid));
        }

        if (link.HasExpired(now))
        {
            return Result.Failure<EnrolmentSession>(Error.From(ErrorCodes.RecoveryTokenExpired));
        }

        var opened = EnrolmentSessionId.New(time);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        link.Spend(opened, now);

        await links.RecordAsync(link, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        // D-147: the session the link opens is capped by the link's own lifetime and
        // never given one of its own.
        return Result.Success(
            new EnrolmentSession(opened, link.Subject, link.ExpiresAt, link.MailboxLost));
    }

    /// <inheritdoc/>
    public ValueTask<Result<LossReported>> ReportLossAsync(
        AccessContext context,
        AuthenticatorId credential,
        string source,
        CancellationToken cancellationToken) =>
        losses.ReportAsync(context, credential, source, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result> CancelLossAsync(
        AccessContext? context,
        AuthenticatorId credential,
        string? cancelToken,
        CancellationToken cancellationToken) =>
        losses.CancelAsync(context, credential, cancelToken, cancellationToken);

    private sealed record Channel(IdentifierKind Kind, string Canonical, SendDestination Destination);

    // A username reaches nobody and an identifier of no kind at all is read as
    // nothing: there is nowhere for the message to go.
    private static Channel? Read(string identifier, bool usernames)
    {
        string entered = identifier.Trim();

        if (IdentifierKinds.Detect(entered, usernames) is not { } kind
            || kind is IdentifierKind.Username)
        {
            return null;
        }

        if (kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(entered, out EmailAddress address)
                ? new Channel(kind, address.Value, SendDestination.Of(address))
                : null;
        }

        return PhoneNumber.TryParse(entered, out PhoneNumber number)
            ? new Channel(kind, number.Value, SendDestination.Of(number))
            : null;
    }

    // AUTH-RECOV-003: what the approver names is matched against the account's
    // verified identifiers, and a channel that is not one of them is no channel.
    private static Channel? Recorded(HeldIdentifiers held, string channelUsed)
    {
        string entered = channelUsed.Trim();

        foreach (HeldIdentifier identifier in held.All)
        {
            if (!identifier.IsVerified || identifier.Kind is IdentifierKind.Username)
            {
                continue;
            }

            if (Matches(identifier, entered) is Channel channel)
            {
                return channel;
            }
        }

        return null;
    }

    private static Channel? Matches(HeldIdentifier identifier, string entered)
    {
        if (identifier.Kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(entered, out EmailAddress address)
                && string.Equals(address.Value, identifier.Canonical, StringComparison.Ordinal)
                ? new Channel(identifier.Kind, identifier.Canonical, SendDestination.Of(address))
                : null;
        }

        return PhoneNumber.TryParse(entered, out PhoneNumber number)
            && string.Equals(number.Value, identifier.Canonical, StringComparison.Ordinal)
            ? new Channel(identifier.Kind, identifier.Canonical, SendDestination.Of(number))
            : null;
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // AUTHZ-SCOPE-001: the permission is held in an organization, and an approver
    // approves for any account with the permission one of their own organizations
    // grants them. The account being recovered need belong to none.
    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SubjectId approver,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationId> organizations = await memberships
            .OfAsync(approver, cancellationToken)
            .ConfigureAwait(false);

        var refused = Error.From(ErrorCodes.Denied);

        foreach (OrganizationId organization in organizations)
        {
            refused = (await gate
                    .RequireAsync(context, Permissions.RecoveryApprove, organization, cancellationToken)
                    .ConfigureAwait(false))
                .Match<Error?>(() => null, error => error);

            if (refused is null)
            {
                return null;
            }
        }

        return refused;
    }

    private async ValueTask<RecoveryLink?> FindAsync(string token, CancellationToken cancellationToken) =>
        token is { Length: > 0 }
            ? await links
                .FindAsync(OpaqueToken.Of(token).Fingerprint(), cancellationToken)
                .ConfigureAwait(false)
            : null;

    private async ValueTask<Result> TellAsync(
        Channel channel,
        string language,
        string source,
        CancellationToken cancellationToken)
    {
        if (channel.Kind is not IdentifierKind.Email
            || !EmailAddress.TryParse(channel.Canonical, out EmailAddress address))
        {
            return Result.Success();
        }

        Error? failure = null;

        _ = (await nonExistence.TellAsync(address, source, language, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        return failure is null ? Result.Success() : Result.Failure(failure);
    }

    // AUTH-RECOV-004 and AUTH-ABUSE-003: a policy that closes the route and an
    // account that cannot be recovered both end here, and both answer exactly as an
    // identifier no account holds answers.
    private async ValueTask<Result> IssueAsync(
        SubjectId subject,
        Channel channel,
        string language,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.RecoveryLinkLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        if (!policy.SelfServiceRecovery
            || !await RecoverableAsync(subject, cancellationToken).ConfigureAwait(false))
        {
            return Result.Success();
        }

        var token = OpaqueToken.Draw(randomness);

        _ = (await sending
                .SendAsync(
                    new SendRequest(
                        channel.Destination,
                        MessageKind.RecoveryLink,
                        RestrictionPurpose.Notification,
                        source,
                        language)
                    {
                        Subject = subject,
                        Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                        {
                            ["token"] = token.Value,
                        },
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(_ => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await links
            .ReplaceAsync(
                RecoveryLink.Issue(token, subject, RecoveryPurpose.SelfService, now, lifetime),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    // D-140: an account its own holder deactivated recovers; one an administrator
    // suspended, or one on its way out, does not.
    private async ValueTask<bool> RecoverableAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        AccountState? state = await accounts.StateAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        return state is AccountState.Active
            || (state is AccountState.Suspended
                && await accounts.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
                    is SuspensionOrigin.Self);
    }

    private async ValueTask<Result> SetAsync(
        SubjectId subject,
        string password,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        var words = new List<string>(held.All.Count);

        foreach (HeldIdentifier identifier in held.All)
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

    // A notice reaches every recorded channel but the one the flow itself used, so
    // that the person hears of a recovery somewhere the recovery did not touch
    // (AUTH-RECOV-002).
    private async ValueTask<int> NotifyAsync(
        SubjectId subject,
        MessageKind message,
        IReadOnlyDictionary<string, string> values,
        string? excluded,
        string source,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        string language = await LanguageAsync(subject, cancellationToken).ConfigureAwait(false);

        int told = 0;

        foreach (HeldIdentifier identifier in held.NoticeSet)
        {
            if (string.Equals(identifier.Canonical, excluded, StringComparison.Ordinal)
                || Destination(identifier) is not SendDestination destination)
            {
                continue;
            }

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
                        Values = values,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            told += sent.Match(_ => 1, _ => 0);
        }

        return told;
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

    // AUTH-RECOV-002: one approval is recorded, counted and alerted on; the link goes
    // out only once as many approvers as the deployment requires have stood behind it.
    private async ValueTask<Result<ApprovedRecovery>> StandAsync(
        SubjectId approver,
        SubjectId subject,
        string reason,
        Channel channel,
        string language,
        string source,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        int perAccount = (await configuration
                .ReadAsync(Settings.RecoveryRateLimitAccount, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        int perApprover = (await configuration
                .ReadAsync(Settings.RecoveryRateLimitApprover, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        int required = (await configuration
                .ReadAsync(Settings.RecoveryApproversRequired, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.RecoveryLinkLifetime, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<ApprovedRecovery>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset since = now - Day;

        int forAccount = await approvals.ForAsync(subject, since, cancellationToken)
            .ConfigureAwait(false);

        int byApprover = await approvals.ByAsync(approver, since, cancellationToken)
            .ConfigureAwait(false);

        if (forAccount >= perAccount || byApprover >= perApprover)
        {
            return Result.Failure<ApprovedRecovery>(Error.From(ErrorCodes.Throttled));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await approvals
            .AddAsync(new RecoveryApproval(subject, approver, channel.Canonical, now), cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit
            .ApprovedAsync(approver, subject, reason, channel.Kind, now, cancellationToken)
            .ConfigureAwait(false);

        await RaiseAsync(subject, approver, forAccount + 1, byApprover + 1, now, cancellationToken)
            .ConfigureAwait(false);

        if (await StandingAsync(subject, now - lifetime, cancellationToken).ConfigureAwait(false)
            < required)
        {
            return Result.Success(new ApprovedRecovery(EnrolmentLinkExpiresAt: null));
        }

        return await SendAsync(
                approver,
                subject,
                channel,
                language,
                source,
                now,
                lifetime,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<Result<ApprovedRecovery>> SendAsync(
        SubjectId approver,
        SubjectId subject,
        Channel channel,
        string language,
        string source,
        DateTimeOffset now,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        Error? failure = null;
        var token = OpaqueToken.Draw(randomness);

        _ = (await sending
                .SendAsync(
                    new SendRequest(
                        channel.Destination,
                        MessageKind.EnrolmentLink,
                        RestrictionPurpose.Notification,
                        source,
                        language)
                    {
                        Subject = subject,
                        Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                        {
                            ["token"] = token.Value,
                        },
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(_ => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<ApprovedRecovery>(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await links
            .ReplaceAsync(
                RecoveryLink.Issue(
                    token,
                    subject,
                    RecoveryPurpose.Enrolment,
                    now,
                    lifetime,
                    approver,

                    // D-111: the approver reached the person somewhere other than the
                    // mailbox, which is what lets the session it opens settle a new
                    // address on the new address alone.
                    channel.Kind is not IdentifierKind.Email),
                cancellationToken)
            .ConfigureAwait(false);
        await approvals.SpendAsync(subject, now, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        _ = await NotifyAsync(
                subject,
                MessageKind.SecurityNotice,
                Nothing,
                channel.Canonical,
                source,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new ApprovedRecovery(now + lifetime));
    }

    private async ValueTask<int> StandingAsync(
        SubjectId subject,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RecoveryApproval> standing = await approvals
            .StandingForAsync(subject, from, cancellationToken)
            .ConfigureAwait(false);

        var approvers = new HashSet<SubjectId>();

        foreach (RecoveryApproval approval in standing)
        {
            _ = approvers.Add(approval.Approver);
        }

        return approvers.Count;
    }

    // OPS-ALERT-001: the two thresholds of chapter 10 section 4.9 are what makes an
    // unusual frequency visible without anyone watching for it.
    private async ValueTask RaiseAsync(
        SubjectId subject,
        SubjectId approver,
        int forAccount,
        int byApprover,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        int account = (await configuration
                .ReadAsync(Settings.AlertingRecoveryAccountThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        int raised = (await configuration
                .ReadAsync(Settings.AlertingRecoveryApproverThreshold, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return;
        }

        if (forAccount >= account)
        {
            await events
                .PublishAsync(
                    Alerts.Of(AlertCondition.RecoveryClustering, subject.ToString(), now),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (byApprover >= raised)
        {
            await events
                .PublishAsync(
                    Alerts.Of(AlertCondition.ApproverVolume, approver.ToString(), now),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
