using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Callbacks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Passwords;
using Janus.Authentication.Sending;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Credentials;

/// <summary>
/// The security events the social providers send about an identity linked to an
/// account: Google's Cross-Account Protection and Sign in with Apple's server-to-server
/// notifications, each carried once and audited.
/// </summary>
/// <param name="admission">What claims an event so a repeated delivery is carried once.</param>
/// <param name="authenticators">Where the linked credential is found and changed.</param>
/// <param name="passwords">Where the account's password is read.</param>
/// <param name="sessions">Where the account's sessions are ended.</param>
/// <param name="accounts">Where the account's standing is read and changed.</param>
/// <param name="identifiers">Where the addresses the account holds are read and changed.</param>
/// <param name="audit">Where what an event did is recorded.</param>
/// <param name="sending">Where a notice goes out.</param>
/// <param name="configuration">Where the languages a notice may go out in are read.</param>
/// <param name="events">Where a change of the account's state is announced.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-LIFE-012a, IDN-LIFE-012 AC3, IDN-LIFE-013 and INT-GEN-003. The event
/// reaches this class verified against the provider's published keys; what is done here
/// runs in the caller's transaction, beside the counts the callback keeps, so an event
/// whose work fails is neither claimed nor half done.
/// </remarks>
internal sealed class ProviderEvents(
    CallbackAdmission admission,
    IAuthenticatorStore authenticators,
    IPasswordStore passwords,
    ISessionStore sessions,
    IAccountDirectory accounts,
    IIdentifierDirectory identifiers,
    ICredentialAudit audit,
    INotificationHandler sending,
    IConfigurationStore configuration,
    IEvents events,
    TimeProvider time)
{
    private const string Risc = "https://schemas.openid.net/secevent/risc/event-type/";

    private const string Oauth = "https://schemas.openid.net/secevent/oauth/event-type/";

    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(StringComparer.Ordinal);

    // The name each provider's events are claimed and recorded under, so an identifier
    // one provider issued never answers for the other's.
    private static readonly FrozenDictionary<Factor, string> Callbacks = new Dictionary<Factor, string>
    {
        [Factor.Google] = "providers/google",
        [Factor.Apple] = "providers/apple",
    }.ToFrozenDictionary();

    // What each event type says of the identity, as each provider spells it; a type not
    // here is recorded and changes nothing. Google's tokens-revoked is the revocation of
    // the sign-in's grant, and its credential-change-required is a suspected compromise,
    // so both end the sessions; Apple's deletion is spelled both ways its documentation
    // and chapter 01 spell it.
    private static readonly FrozenDictionary<(Factor Provider, string Type), ProviderEventKind> Kinds =
        new Dictionary<(Factor Provider, string Type), ProviderEventKind>
        {
            [(Factor.Google, Risc + "sessions-revoked")] = ProviderEventKind.Compromised,
            [(Factor.Google, Risc + "account-disabled")] = ProviderEventKind.Compromised,
            [(Factor.Google, Risc + "account-credential-change-required")] = ProviderEventKind.Compromised,
            [(Factor.Google, Oauth + "tokens-revoked")] = ProviderEventKind.Compromised,
            [(Factor.Apple, "consent-revoked")] = ProviderEventKind.Withdrawn,
            [(Factor.Apple, "account-delete")] = ProviderEventKind.Withdrawn,
            [(Factor.Apple, "account-deleted")] = ProviderEventKind.Withdrawn,
            [(Factor.Apple, "email-disabled")] = ProviderEventKind.AddressDisabled,
        }.ToFrozenDictionary();

    /// <summary>
    /// The name a social provider's events are claimed and recorded under.
    /// </summary>
    /// <param name="provider">Which social provider.</param>
    /// <returns>The callback's name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The factor is not a social provider.</exception>
    public static string CallbackOf(Factor provider) =>
        Callbacks.TryGetValue(provider, out string? callback)
            ? callback
            : throw new ArgumentOutOfRangeException(nameof(provider), provider, "Not a social provider.");

    /// <summary>
    /// Carries one verified event.
    /// </summary>
    /// <param name="notice">The event.</param>
    /// <param name="source">Where the callback came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the event was carried, which it is not where it had been carried before;
    /// or the failure that kept the change of state from being announced.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<bool>> TakeAsync(
        ProviderNotice notice,
        string source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(source);

        bool claimed = await admission
            .ClaimAsync(CallbackOf(notice.Provider), notice.EventId, cancellationToken)
            .ConfigureAwait(false);

        Authenticator? linked = notice.Subject is null
            ? null
            : await authenticators
                .ByProviderAsync(notice.Provider, notice.Subject, cancellationToken)
                .ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();

        if (!claimed)
        {
            // IDN-LIFE-012a AC1: a replayed event changes nothing and is audited as
            // rejected; the provider is still answered, so it stops delivering it.
            if (linked is not null)
            {
                await RecordedAsync(AuditActions.ProviderEventRejected, linked, notice, ProviderEventOutcome.Replayed, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            return Result.Success(false);
        }

        // An event about an identity no account links, or about no identity at all,
        // concerns nobody here.
        if (linked is null)
        {
            return Result.Success(true);
        }

        Result<ProviderEventOutcome> outcome = Kinds.GetValueOrDefault((notice.Provider, notice.Type)) switch
        {
            ProviderEventKind.Compromised =>
                await EndedAsync(linked, now, cancellationToken).ConfigureAwait(false),
            ProviderEventKind.Withdrawn =>
                await WithdrawnAsync(linked, source, now, cancellationToken).ConfigureAwait(false),
            ProviderEventKind.AddressDisabled =>
                await UnvouchedAsync(linked, notice.Address, cancellationToken).ConfigureAwait(false),
            _ => Result.Success(ProviderEventOutcome.Recorded),
        };

        if (outcome.Match(_ => (Error?)null, error => error) is Error unannounced)
        {
            return Result.Failure<bool>(unannounced);
        }

        await RecordedAsync(
                AuditActions.ProviderEventTaken,
                linked,
                notice,
                outcome.Match(done => done, _ => ProviderEventOutcome.Recorded),
                now,
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(true);
    }

    /// <summary>
    /// Records an event the provider's published keys did not verify against the
    /// account whose linked identity it names, where it names one.
    /// </summary>
    /// <param name="provider">Which social provider it claims to come from.</param>
    /// <param name="subject">The provider's subject identifier it names, where it names one.</param>
    /// <param name="type">The event type it names, where it names one.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    public async ValueTask RejectedAsync(
        Factor provider,
        string? subject,
        string? type,
        CancellationToken cancellationToken)
    {
        if (subject is null)
        {
            return;
        }

        Authenticator? linked = await authenticators
            .ByProviderAsync(provider, subject, cancellationToken)
            .ConfigureAwait(false);

        if (linked is not null)
        {
            await audit
                .ProviderEventAsync(
                    AuditActions.ProviderEventRejected,
                    linked.Subject,
                    linked.Id,
                    type ?? string.Empty,
                    ProviderEventOutcome.Unsigned,
                    time.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string Key(SubjectId subject, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{subject.Value}@{at.UtcTicks}");

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

    private ValueTask RecordedAsync(
        AuditAction action,
        Authenticator linked,
        ProviderNotice notice,
        ProviderEventOutcome outcome,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        audit.ProviderEventAsync(action, linked.Subject, linked.Id, notice.Type, outcome, at, cancellationToken);

    // IDN-LIFE-012a AC1: every session of the account ends, and the credential is held
    // until the person signs in by another factor.
    private async ValueTask<Result<ProviderEventOutcome>> EndedAsync(
        Authenticator linked,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await sessions.EndAccountAsync(linked.Subject, now, cancellationToken).ConfigureAwait(false);

        linked.Hold();

        await authenticators.RecordAsync(linked, cancellationToken).ConfigureAwait(false);

        return Result.Success(ProviderEventOutcome.SessionsEnded);
    }

    // IDN-LIFE-012a AC2: the credential is unlinked where the account keeps another way
    // in; where it is the last (IDN-LIFE-012 AC3), the account is suspended instead, and
    // told either way, as every removal of a credential is.
    private async ValueTask<Result<ProviderEventOutcome>> WithdrawnAsync(
        Authenticator linked,
        string source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Authenticator> enrolled = await authenticators
            .OfAsync(linked.Subject, cancellationToken)
            .ConfigureAwait(false);

        bool password = await SecondStep.AvailableAsync(passwords, linked.Subject, cancellationToken)
            .ConfigureAwait(false);

        if (HeldFactors.KeptWithout(enrolled, linked, password))
        {
            await authenticators.RemoveAsync(linked.Id, cancellationToken).ConfigureAwait(false);

            _ = await TellAsync(linked.Subject, source, cancellationToken).ConfigureAwait(false);

            return Result.Success(ProviderEventOutcome.CredentialUnlinked);
        }

        return await SuspendedAsync(linked.Subject, source, now, cancellationToken).ConfigureAwait(false);
    }

    // IDN-LIFE-013: the suspension is an administrator's, so only an administrator
    // stands the account back up; one its owner deactivated is taken over, and one an
    // administrator suspended already stays as it is. An account in its deletion window
    // or erased is left to that.
    private async ValueTask<Result<ProviderEventOutcome>> SuspendedAsync(
        SubjectId subject,
        string source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        AccountState? state = await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false);

        if (state is null or AccountState.Deleting or AccountState.Deleted)
        {
            return Result.Success(ProviderEventOutcome.Recorded);
        }

        if (state is not AccountState.Suspended
            || await accounts.SuspendedByAsync(subject, cancellationToken).ConfigureAwait(false)
                is not SuspensionOrigin.Administrator)
        {
            await accounts.SuspendAsync(subject, cancellationToken).ConfigureAwait(false);
        }

        // AUTH-SESS-010: the sessions end in the transaction that suspends.
        await sessions.EndAccountAsync(subject, now, cancellationToken).ConfigureAwait(false);

        if (state is not AccountState.Suspended)
        {
            Result published = await events
                .PublishAsync(
                    new AccountSuspended(now, Key(subject, now), SuspensionOrigin.Administrator)
                    {
                        Subject = subject,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (published.Match(() => (Error?)null, error => error) is Error unpublished)
            {
                return Result.Failure<ProviderEventOutcome>(unpublished);
            }
        }

        _ = await TellAsync(subject, source, cancellationToken).ConfigureAwait(false);

        return Result.Success(ProviderEventOutcome.AccountSuspended);
    }

    // IDN-LIFE-012a: the address the provider stopped forwarding to drops to
    // unverified. An event naming no address, or one the account holds unverified
    // already, changes nothing.
    private async ValueTask<Result<ProviderEventOutcome>> UnvouchedAsync(
        Authenticator linked,
        string? address,
        CancellationToken cancellationToken)
    {
        if (address is null || !EmailAddress.TryParse(address, out EmailAddress named))
        {
            return Result.Success(ProviderEventOutcome.Recorded);
        }

        IdentifierId? dropped = await identifiers
            .UnverifyAsync(linked.Subject, named.Value, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(dropped is null ? ProviderEventOutcome.Recorded : ProviderEventOutcome.AddressUnverified);
    }

    // The security-notice set as it stands; a notice one destination refuses still
    // reaches the rest.
    private async ValueTask<int> TellAsync(
        SubjectId subject,
        string source,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers channels = await identifiers.HeldAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        string? language = await LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        int told = 0;

        foreach (HeldIdentifier identifier in channels.NoticeSet)
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
                        Subject = subject,
                        Values = Nothing,
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
