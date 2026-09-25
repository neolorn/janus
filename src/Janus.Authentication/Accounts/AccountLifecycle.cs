using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Accounts;

/// <summary>
/// What an account does to its own standing: takes itself down, stands itself back
/// up, asks for its own erasure and changes its mind inside the window.
/// </summary>
/// <param name="directory">Where the standing is read and the transition carried.</param>
/// <param name="identifiers">Where the addresses a notice reaches are read.</param>
/// <param name="links">Where the link a notice carries is held.</param>
/// <param name="sessions">What every transition out of active ends.</param>
/// <param name="sending">Where a notice goes out.</param>
/// <param name="audit">Where what the account did to itself is recorded.</param>
/// <param name="stepUp">What the two gated operations ask of the session.</param>
/// <param name="events">Where the lifecycle event goes.</param>
/// <param name="configuration">Where the grace window is read.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the link token is drawn from.</param>
/// <remarks>
/// Implements IDN-LIFE-013, IDN-LIFE-014, AUTH-SESS-010 and chapter 09 section 6.
/// Neither state the account can put itself in can sign in, so what ends each of
/// them is a link in the notice and never a session.
/// </remarks>
internal sealed class AccountLifecycle(
    IAccountDirectory directory,
    IIdentifierDirectory identifiers,
    ILifecycleLinkStore links,
    ISessionStore sessions,
    INotificationHandler sending,
    IAccountAudit audit,
    StepUpGuard stepUp,
    IEvents events,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    private static readonly AuditAction Deactivated =
        AuditActions.AccountDeactivated;

    private static readonly AuditAction Reactivated =
        AuditActions.AccountReactivated;

    private static readonly AuditAction DeletionRequested =
        AuditActions.DeletionRequested;

    private static readonly AuditAction DeletionCancelled =
        AuditActions.DeletionCancelled;

    /// <summary>
    /// Takes the account down at its own request.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> DeactivateAsync(
        AccessContext context,
        SessionId session,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.AccountDeactivate, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure(closed);
        }

        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not AccountState.Active)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        DateTimeOffset now = time.GetUtcNow();
        var token = OpaqueToken.Draw(randomness);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await directory.DeactivateAsync(subject, cancellationToken).ConfigureAwait(false);

        await links
            .ReplaceAsync(
                LifecycleLink.Issued(subject, LifecycleLinkKind.Reactivation, token, now),
                cancellationToken)
            .ConfigureAwait(false);

        // AUTH-SESS-010: the transition out of active ends every session in the
        // operation that makes it, so nothing of the account's is still live after.
        await sessions.EndAccountAsync(subject, now, cancellationToken).ConfigureAwait(false);

        _ = await TellAsync(
                subject,
                MessageKind.DeactivationNotice,
                source,
                token.Value,
                cancellationToken)
            .ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountSuspended(now, Key(subject, now), SuspensionOrigin.Self)
                {
                    Subject = subject,
                    Actor = subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit.RecordedAsync(Deactivated, subject, subject, now, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Stands a self-deactivated account back up from the link its notice carried.
    /// </summary>
    /// <param name="linkToken">The token the notice carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    /// <exception cref="ArgumentNullException">The token is absent.</exception>
    public async ValueTask<Result> ReactivateAsync(
        [NeverLogged] string linkToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);

        if (await PresentedAsync(linkToken, LifecycleLinkKind.Reactivation, cancellationToken)
                .ConfigureAwait(false)
            is not LifecycleLink link)
        {
            return Result.Failure(Error.From(ErrorCodes.ReactivationTokenInvalid));
        }

        // IDN-LIFE-013: the two suspensions are reversed differently, and a link
        // cannot stand up an account an administrator took down.
        switch (await directory.SuspendedByAsync(link.Subject, cancellationToken).ConfigureAwait(false))
        {
            case SuspensionOrigin.Administrator:
                return Result.Failure(Error.From(ErrorCodes.AccountAdministrativelySuspended));
            case null:
                return Result.Failure(Error.From(ErrorCodes.ReactivationTokenInvalid));
            default:
                break;
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await directory.ReinstateAsync(link.Subject, cancellationToken).ConfigureAwait(false);
        await links.RemoveAsync(link.Subject, cancellationToken).ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountReactivated(now, Key(link.Subject, now))
                {
                    Subject = link.Subject,
                    Actor = link.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit.RecordedAsync(Reactivated, link.Subject, link.Subject, now, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Begins the account's own deletion grace window.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>When the erasure runs if nothing cancels it, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<DateTimeOffset>> DeleteAsync(
        AccessContext context,
        SessionId session,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);

        if (context.Effective is not SubjectId subject)
        {
            return Result.Failure<DateTimeOffset>(Error.From(ErrorCodes.Denied));
        }

        if (await stepUp
                .PassedAsync(subject, session, StepUpAction.AccountDelete, cancellationToken)
                .ConfigureAwait(false)
            is Error closed)
        {
            return Result.Failure<DateTimeOffset>(closed);
        }

        if (await directory.StateAsync(subject, cancellationToken).ConfigureAwait(false)
            is not (AccountState.Active or AccountState.Restricted))
        {
            return Result.Failure<DateTimeOffset>(Error.From(ErrorCodes.Denied));
        }

        Error? refused = null;

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.AccountDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref refused));

        if (refused is not null)
        {
            return Result.Failure<DateTimeOffset>(refused);
        }

        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset erasesAt = now + grace;
        var token = OpaqueToken.Draw(randomness);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await directory.BeginDeletionAsync(subject, now, cancellationToken).ConfigureAwait(false);

        await links
            .ReplaceAsync(
                LifecycleLink.Issued(subject, LifecycleLinkKind.DeletionCancellation, token, now),
                cancellationToken)
            .ConfigureAwait(false);

        // AUTH-SESS-010 AC3: the sessions end before anything of the person's is
        // removed, and the window runs with nothing of theirs still live.
        await sessions.EndAccountAsync(subject, now, cancellationToken).ConfigureAwait(false);

        _ = await TellAsync(subject, MessageKind.DeletionNotice, source, token.Value, cancellationToken)
            .ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountDeletionRequested(now, Key(subject, now), DeletionOrigin.Self, erasesAt)
                {
                    Subject = subject,
                    Actor = subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure<DateTimeOffset>(unpublished);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit.RecordedAsync(DeletionRequested, subject, subject, now, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(erasesAt);
    }

    /// <summary>
    /// Ends a grace window from the link the deletion notice carried.
    /// </summary>
    /// <param name="linkToken">The token the notice carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    /// <exception cref="ArgumentNullException">The token is absent.</exception>
    public async ValueTask<Result> CancelDeletionAsync(
        [NeverLogged] string linkToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(linkToken);

        LifecycleLink? link = await PresentedAsync(
                linkToken,
                LifecycleLinkKind.DeletionCancellation,
                cancellationToken)
            .ConfigureAwait(false);

        HeldDeletion? deleting = link is null
            ? null
            : await directory.DeletingAsync(link.Subject, cancellationToken).ConfigureAwait(false);

        // IDN-LIFE-003: a takedown is not the subject's to cancel, and says so rather
        // than answering as a window that has run out.
        if (deleting?.By is DeletionOrigin.Takedown)
        {
            return Result.Failure(Error.From(ErrorCodes.TakedownActive));
        }

        if (link is null
            || deleting is null
            || await ElapsedAsync(deleting, cancellationToken).ConfigureAwait(false))
        {
            // A token that answers to nothing and a window that has run out are the
            // same answer: neither says whether a deletion was ever asked for.
            return Result.Failure(Error.From(ErrorCodes.DeletionWindowElapsed));
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await directory.CancelDeletionAsync(link.Subject, cancellationToken).ConfigureAwait(false);
        await links.RemoveAsync(link.Subject, cancellationToken).ConfigureAwait(false);

        Result published = await events
            .PublishAsync(
                new AccountDeletionCancelled(now, Key(link.Subject, now))
                {
                    Subject = link.Subject,
                    Actor = link.Subject,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (published.Match(() => (Error?)null, error => error) is Error unpublished)
        {
            return Result.Failure(unpublished);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        await audit
            .RecordedAsync(DeletionCancelled, link.Subject, link.Subject, now, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    private static string Key(SubjectId subject, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{subject.Value}@{at.UtcTicks}");

    private static SendDestination? Destination(HeldIdentifier identifier) =>
        identifier.Kind switch
        {
            IdentifierKind.Email => EmailAddress.TryParse(identifier.Canonical, out EmailAddress address)
                ? SendDestination.Of(address)
                : null,
            IdentifierKind.Phone => PhoneNumber.TryParse(identifier.Canonical, out PhoneNumber number)
                ? SendDestination.Of(number)
                : null,
            _ => null,
        };

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<LifecycleLink?> PresentedAsync(
        [NeverLogged] string linkToken,
        LifecycleLinkKind kind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(linkToken))
        {
            return null;
        }

        LifecycleLink? held = await links
            .FindAsync(OpaqueToken.Of(linkToken).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        return held?.Kind == kind ? held : null;
    }

    private async ValueTask<bool> ElapsedAsync(
        HeldDeletion deleting,
        CancellationToken cancellationToken)
    {
        TimeSpan grace = (await configuration
                .ReadAsync(Settings.AccountDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => TimeSpan.Zero);

        return time.GetUtcNow() >= deleting.Since + grace;
    }

    private async ValueTask<int> TellAsync(
        SubjectId subject,
        MessageKind message,
        string source,
        [NeverLogged] string token,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        string? language = await LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        var values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
        {
            ["token"] = token,
        };

        int told = 0;

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

    private async ValueTask<string?> LanguageAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        string? settled = await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => (IReadOnlyList<string>)[]);

        return RecipientLanguage.Of(settled, requested: null, languages);
    }
}
