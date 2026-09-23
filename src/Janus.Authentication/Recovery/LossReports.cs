using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Policies;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Recovery;

/// <summary>
/// What becomes of a credential its holder says is gone: refused at once, notified
/// throughout, and invalidated only when the window has run and somebody has been
/// told.
/// </summary>
/// <param name="reports">Where the reports that are running are held.</param>
/// <param name="authenticators">Where the account's credentials are read.</param>
/// <param name="passwords">Where the account's password is read and marked.</param>
/// <param name="recoveryCodes">Where the account's set of single-use codes is held.</param>
/// <param name="identifiers">Where the channels a notice reaches are read.</param>
/// <param name="policies">What policy governs the account.</param>
/// <param name="sending">Where a message goes out.</param>
/// <param name="audit">Where what became of a credential is recorded.</param>
/// <param name="events">Where what became of a credential is announced.</param>
/// <param name="configuration">Where the window and the notice interval come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the cancel token is drawn from.</param>
/// <remarks>
/// Implements AUTH-RECOV-007, AUTH-RECOV-007a and AUTH-RECOV-008. Reporting asks no
/// step-up, because the person reporting has lost the very factor a gate would ask
/// for; what stands in its place is the window and the notices that run through it.
/// </remarks>
internal sealed class LossReports(
    ILossReportStore reports,
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
    IRecoveryCodeStore recoveryCodes,
    IIdentifierDirectory identifiers,
    PolicyResolution policies,
    INotificationHandler sending,
    ICredentialAudit audit,
    IEvents events,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    private static readonly AuditAction Reported = AuditActions.CredentialReportedLost;

    private static readonly AuditAction Cancelled = AuditActions.CredentialReportCancelled;

    private static readonly AuditAction Invalidated = AuditActions.CredentialInvalidated;

    private static readonly AuditAction Held = AuditActions.CredentialInvalidationHeld;

    // What a consumer recognises the repeat of one report by: the credential and
    // the instant, because one credential may be reported again after a cancel.
    private const string Opened = "credential-suspended";

    private const string Ended = "credential-restored";

    private const string Completed = "credential-invalidated";

    // The notices the window carries are asked for by no request, so they count
    // against the deployment itself and not against a person's address.
    private const string Origin = "recovery";

    /// <summary>
    /// Reports a credential lost.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>When the window ends, or what refused the report.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<LossReported>> ReportAsync(
        AccessContext context,
        AuthenticatorId credential,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<LossReported>(Error.From(ErrorCodes.Denied));
        }

        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<LossReported>(failure);
        }

        // AUTH-RECOV-008: an account that holds two credentials by policy reports a
        // loss through an approver, not through itself.
        if (!policy.SelfServiceRecovery)
        {
            return Result.Failure<LossReported>(Error.From(ErrorCodes.LossReportNotPermitted));
        }

        Authenticator? held = await authenticators.FindAsync(credential, cancellationToken)
            .ConfigureAwait(false);

        if (held is null || held.Subject != subject || !held.Confirmed)
        {
            return Result.Failure<LossReported>(Error.From(ErrorCodes.CredentialNotFound));
        }

        if (await reports.FindAsync(credential, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<LossReported>(Error.From(ErrorCodes.LossReportPending));
        }

        if (held.State is not AuthenticatorState.Active)
        {
            return Result.Failure<LossReported>(Error.From(ErrorCodes.CredentialSuspended));
        }

        return await SuspendAsync(held, source, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Suspends a credential for the window and opens the report that runs it, which
    /// a loss report and a removal that would lower reachable assurance both do
    /// (AUTH-RECOV-007).
    /// </summary>
    /// <param name="held">The credential.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>When the window ends, or what refused it.</returns>
    /// <exception cref="ArgumentNullException">The credential is absent.</exception>
    public async ValueTask<Result<LossReported>> SuspendAsync(
        Authenticator held,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        Error? failure = null;

        TimeSpan window = (await configuration
                .ReadAsync(Settings.RecoveryInvalidationWindow, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<LossReported>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        var cancel = OpaqueToken.Draw(randomness);
        var report = LossReport.Open(held.Id, held.Subject, cancel, now, window);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        held.Suspend(report.InvalidatesAt);

        await authenticators.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await reports.AddAsync(report, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        report.Notified(
            await TellAsync(report, source, cancellationToken).ConfigureAwait(false),
            now);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await reports.RecordAsync(report, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(Reported, held.Subject, held.Id, now, cancellationToken)
            .ConfigureAwait(false);

        if (await AnnouncedAsync(
                new CredentialSuspended(
                    now,
                    Key(Opened, held.Id, now),
                    held.Id,
                    held.Factor,
                    report.InvalidatesAt)
                {
                    Subject = held.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false) is Error unannounced)
        {
            return Result.Failure<LossReported>(unannounced);
        }

        return Result.Success(new LossReported(held.Id, report.InvalidatesAt));
    }

    /// <summary>
    /// Cancels a report, from a session of the account or from the link every notice
    /// carried, and returns the credential to active.
    /// </summary>
    /// <param name="context">Who is asking, which is nobody where a link is presented.</param>
    /// <param name="credential">Which credential.</param>
    /// <param name="cancelToken">The token the notice carried, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal that tells an outsider nothing.</returns>
    /// <exception cref="ArgumentNullException">The context is absent.</exception>
    public async ValueTask<Result> CancelAsync(
        AccessContext? context,
        AuthenticatorId credential,
        string? cancelToken,
        CancellationToken cancellationToken)
    {
        LossReport? report = await reports.FindAsync(credential, cancellationToken)
            .ConfigureAwait(false);

        // LIB-SEAM-002: the effective identity is read once and compared as a value,
        // which is the one way a feature asks whose account it is.
        bool holder = report is not null
            && context?.Effective is SubjectId subject
            && report.Subject == subject;

        // One refusal answers an unknown credential, a report that is not running and
        // a token that is not the one: none of them tells the caller which it was.
        if (report is null || !(holder || report.Matches(cancelToken)))
        {
            return Result.Failure(Error.From(ErrorCodes.CredentialNotFound));
        }

        Authenticator? held = await authenticators.FindAsync(credential, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (held is not null)
        {
            held.Restore();

            await authenticators.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        }

        await reports.RemoveAsync(credential, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(Cancelled, report.Subject, credential, now, cancellationToken)
            .ConfigureAwait(false);

        // AUTH-RECOV-007: a report is cancelled whether or not the credential is
        // still there to restore, and the event states what was restored.
        if (held is not null
            && await AnnouncedAsync(
                new CredentialRestored(
                    now,
                    Key(Ended, credential, now),
                    credential,
                    held.Factor)
                {
                    Subject = report.Subject,
                    Actor = context?.Effective,
                },
                cancellationToken)
            .ConfigureAwait(false) is Error unannounced)
        {
            return Result.Failure(unannounced);
        }

        return Result.Success();
    }

    /// <summary>
    /// Carries every running report as far as the clock allows: another notice where
    /// one is owed, invalidation where the window has run and a notice was delivered,
    /// and a hold where none was.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many reports were carried on.</returns>
    public async ValueTask<Result<int>> AdvanceAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan interval = (await configuration
                .ReadAsync(Settings.RecoveryInvalidationNoticeInterval, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();

        IReadOnlyList<LossReport> outstanding = await reports
            .OutstandingAsync(now - interval, now, cancellationToken)
            .ConfigureAwait(false);

        int carried = 0;

        foreach (LossReport report in outstanding)
        {
            (await CarryAsync(report, now, interval, cancellationToken).ConfigureAwait(false))
                .Switch(count => carried += count, error => failure = error);

            if (failure is not null)
            {
                return Result.Failure<int>(failure);
            }
        }

        return Result.Success(carried);
    }

    private async ValueTask<Result<int>> CarryAsync(
        LossReport report,
        DateTimeOffset now,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        if (now < report.InvalidatesAt)
        {
            return Result.Success(report.NoticeDue(now, interval)
                ? await RepeatAsync(report, now, cancellationToken).ConfigureAwait(false)
                : 0);
        }

        // AUTH-RECOV-007: a window nobody was told of completes nothing. The report
        // carries that it was held, and the credential stays suspended.
        if (!report.AnyDelivered)
        {
            report.Hold(now);

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);
            await reports.RecordAsync(report, cancellationToken).ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            await audit
                .RecordedAsync(Held, report.Subject, report.Credential, now, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(1);
        }

        return await InvalidateAsync(report, now, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> RepeatAsync(
        LossReport report,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Every notice carries the same link: a fresh token would strand the one
        // already in somebody's inbox, which is the one they are most likely to open.
        report.Notified(
            await TellAsync(report, Origin, cancellationToken).ConfigureAwait(false),
            now);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await reports.RecordAsync(report, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return 1;
    }

    private async ValueTask<Result<int>> InvalidateAsync(
        LossReport report,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Authenticator? held = await authenticators.FindAsync(report.Credential, cancellationToken)
            .ConfigureAwait(false);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (held is not null)
        {
            held.Invalidate();

            await authenticators.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(report.Subject, cancellationToken)
            .ConfigureAwait(false);

        Password? password = await passwords.FindAsync(report.Subject, cancellationToken)
            .ConfigureAwait(false);

        var standing = HeldFactors.Of(enrolled, password is not null);

        // AUTH-RECOV-007a: a password set under the shorter floor is below it once the
        // account no longer reaches two factors, so the next sign-in collects a new one.
        if (password is not null
            && !password.MeetsSingleFactorFloor
            && StepUp.Reachable(standing.Standing).Level < AssuranceLevel.Aal2)
        {
            password.RequireChange();

            await passwords.SetAsync(password, cancellationToken).ConfigureAwait(false);
        }

        // AUTH-RECOV-007: the codes stood in for a second step, and there is none left
        // for them to stand in for.
        if (!standing.Standing.Any(SecondStep.Is))
        {
            await recoveryCodes.RemoveAsync(report.Subject, cancellationToken).ConfigureAwait(false);
        }

        await reports.RemoveAsync(report.Credential, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(Invalidated, report.Subject, report.Credential, now, cancellationToken)
            .ConfigureAwait(false);

        // AUTH-RECOV-007: invalidation is the one point at which the account's
        // reachable assurance is recomputed, so it is the one a consumer hears about.
        if (held is not null
            && await AnnouncedAsync(
                new CredentialInvalidated(
                    now,
                    Key(Completed, report.Credential, now),
                    report.Credential,
                    held.Factor)
                {
                    Subject = report.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false) is Error unannounced)
        {
            return Result.Failure<int>(unannounced);
        }

        return Result.Success(1);
    }

    // Every recorded channel hears of the report, and each notice carries the link
    // that ends it (AUTH-RECOV-007).
    private async ValueTask<bool> TellAsync(
        LossReport report,
        string source,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(report.Subject, cancellationToken)
            .ConfigureAwait(false);

        string language = await LanguageAsync(report.Subject, cancellationToken).ConfigureAwait(false);
        var values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
        {
            ["token"] = Encoding.UTF8.GetString(report.Cancel),
        };

        bool delivered = false;

        foreach (HeldIdentifier identifier in held.NoticeSet)
        {
            if (Destination(identifier) is not SendDestination destination)
            {
                continue;
            }

            Result<SendReference> sent = await sending
                .SendAsync(
                    new SendRequest(
                        destination,
                        MessageKind.SecurityNotice,
                        RestrictionPurpose.Notification,
                        source,
                        language)
                    {
                        Subject = report.Subject,
                        Values = values,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            delivered = sent.Match(_ => true, _ => false) || delivered;
        }

        return delivered;
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

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static string Key(string what, AuthenticatorId credential, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{what}:{credential.Value}@{at.UtcTicks}");

    private async ValueTask<Error?> AnnouncedAsync<TEvent>(
        TEvent raised,
        CancellationToken cancellationToken)
        where TEvent : JanusEvent =>
        (await events.PublishAsync(raised, cancellationToken).ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);

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
}
