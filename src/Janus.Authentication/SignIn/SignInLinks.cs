using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Authentication.Registration;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.SignIn;

/// <summary>
/// The sign-in links and codes a channel carries: asking for one, spending one, and
/// ending one.
/// </summary>
/// <param name="pending">Where the ones that have gone out are held.</param>
/// <param name="identifiers">Where an identifier is resolved to an account.</param>
/// <param name="accounts">Where the account's state is read.</param>
/// <param name="policies">What policy governs the account.</param>
/// <param name="domainLock">Whether an email address is one a member may sign in with.</param>
/// <param name="sending">Where a message goes out.</param>
/// <param name="nonExistence">What answers an address no account holds.</param>
/// <param name="signals">What is known about a number before a text leans on it.</param>
/// <param name="throttle">The progressive delay.</param>
/// <param name="configuration">Where the lifetimes and the limits come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a token and a code are drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-003, AUTH-ABUSE-003, REG-SESS-003 and REG-DOM-001. Asking always
/// succeeds: an identifier no account holds, an identifier whose channel the policy has
/// not enabled, an account that cannot be signed into and an address a domain lock
/// refuses each produce the same answer as one that can, and differ only in what
/// arrives at the channel.
/// </remarks>
internal sealed class SignInLinks(
    IPendingSignInStore pending,
    IIdentifierDirectory identifiers,
    IAccountDirectory accounts,
    PolicyResolution policies,
    DomainLock domainLock,
    INotificationHandler sending,
    NonExistenceNotice nonExistence,
    PhoneSignals signals,
    ThrottleService throttle,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Asks for a sign-in link, by email or by text according to the identifier's
    /// kind.
    /// </summary>
    /// <param name="identifier">The email or phone as it was entered.</param>
    /// <param name="language">
    /// The locale of the request, which the message goes out in where the account holds
    /// no language of its own (IDN-ATTR-001).
    /// </param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="browser">What the asking browser carries, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the delay the source has earned.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result> SendLinkAsync(
        string identifier,
        string language,
        string source,
        string? browser,
        CancellationToken cancellationToken) =>
        AskAsync(identifier, language, source, browser, Links, cancellationToken);

    /// <summary>
    /// Asks for a one-time code by email.
    /// </summary>
    /// <param name="identifier">The email as it was entered.</param>
    /// <param name="language">
    /// The locale of the request, which the message goes out in where the account holds
    /// no language of its own (IDN-ATTR-001).
    /// </param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the delay the source has earned.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public ValueTask<Result> SendCodeAsync(
        string identifier,
        string language,
        string source,
        CancellationToken cancellationToken) =>
        AskAsync(identifier, language, source, browser: null, Codes, cancellationToken);

    /// <summary>
    /// The pending sign-in a link token answers to, where it has not lapsed.
    /// </summary>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The pending sign-in, or nothing.</returns>
    public async ValueTask<PendingSignIn?> FindAsync(
        string linkToken,
        CancellationToken cancellationToken)
    {
        if (linkToken is not { Length: > 0 })
        {
            return null;
        }

        PendingSignIn? held = await pending
            .FindAsync(OpaqueToken.Of(linkToken).Fingerprint(), cancellationToken)
            .ConfigureAwait(false);

        return held is null || held.HasExpired(time.GetUtcNow()) ? null : held;
    }

    /// <summary>
    /// The account's outstanding link or code of one kind, where it has not lapsed.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="factor">Which kind.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The pending sign-in, or nothing.</returns>
    public async ValueTask<PendingSignIn?> FindAsync(
        SubjectId subject,
        Factor factor,
        CancellationToken cancellationToken)
    {
        PendingSignIn? held = await pending
            .FindAsync(subject, factor, cancellationToken)
            .ConfigureAwait(false);

        return held is null || held.HasExpired(time.GetUtcNow()) ? null : held;
    }

    /// <summary>
    /// What the asking browser's token hashes to, or nothing where it carried none.
    /// </summary>
    /// <param name="browser">What the browser carried.</param>
    /// <returns>The fingerprint, or nothing.</returns>
    public static byte[]? Fingerprint(string? browser) =>
        browser is { Length: > 0 } carried ? OpaqueToken.Of(carried).Fingerprint() : null;

    /// <summary>
    /// Spends the code a message carried, where it is the one that was sent.
    /// </summary>
    /// <param name="held">The pending sign-in.</param>
    /// <param name="entered">What was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or what the code produced.</returns>
    /// <exception cref="ArgumentNullException">The pending sign-in is absent.</exception>
    public async ValueTask<Result> SpendCodeAsync(
        PendingSignIn held,
        string entered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        Error? failure = null;

        int attempts = (await configuration
                .ReadAsync(Settings.CodeVerificationAttempts, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (!held.Matches(entered))
        {
            held.Missed();

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            // Enough wrong codes end the link, which is what stops a six-digit code
            // being guessed at leisure (AUTH-FACT-004).
            if (held.WrongAttempts >= attempts)
            {
                await pending.RemoveAsync(held.Fingerprint, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await pending.RecordAsync(held, cancellationToken).ConfigureAwait(false);
            }

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Failure(
                Error.From(held.WrongAttempts >= attempts ? ErrorCodes.CodeExpired : ErrorCodes.CodeInvalid));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await pending.RemoveAsync(held.Fingerprint, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Whether the address a link or code went to is one the account's domain locks
    /// still admit, which is judged again when it is used since a lock may have changed
    /// while it was out (REG-DOM-001).
    /// </summary>
    /// <param name="held">The pending sign-in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing where it is admitted, or the refusal.</returns>
    /// <exception cref="ArgumentNullException">The pending sign-in is absent.</exception>
    public async ValueTask<Error?> LockedAsync(PendingSignIn held, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        if (held.Email is not IdentifierId email)
        {
            return null;
        }

        HeldIdentifiers standing = await identifiers.HeldAsync(held.Subject, cancellationToken)
            .ConfigureAwait(false);

        // An address the account has given up since is judged on nothing.
        return standing.Find(email) is HeldIdentifier sent
            && EmailAddress.TryParse(sent.Canonical, out EmailAddress address)
                ? await domainLock.RefusedAsync(held.Subject, address, cancellationToken).ConfigureAwait(false)
                : null;
    }

    /// <summary>
    /// Ends one that has been used, inside the transaction the caller opened.
    /// </summary>
    /// <param name="held">The pending sign-in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    /// <exception cref="ArgumentNullException">The pending sign-in is absent.</exception>
    public ValueTask SpendAsync(PendingSignIn held, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        return pending.RemoveAsync(held.Fingerprint, cancellationToken);
    }

    /// <summary>
    /// Ends a pending link, whether or not the token resolves to one.
    /// </summary>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, always.</returns>
    public async ValueTask<Result> AbandonAsync(
        string linkToken,
        CancellationToken cancellationToken)
    {
        if (linkToken is { Length: > 0 })
        {
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);
            await pending
                .RemoveAsync(OpaqueToken.Of(linkToken).Fingerprint(), cancellationToken)
                .ConfigureAwait(false);
            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    // What each kind of ask sends, and on which channels. The email code is a code to
    // type and carries no link; a link carries both, because the browser that opened
    // it elsewhere shows the code (REG-SESS-003).
    private static readonly Ask Links = new(
        [IdentifierKind.Email, IdentifierKind.Phone],
        MessageKind.SignInLink,
        Settings.LinkMagicLifetime,
        CarriesLink: true);

    private static readonly Ask Codes = new(
        [IdentifierKind.Email],
        MessageKind.VerificationCode,
        Settings.CodeVerificationLifetime,
        CarriesLink: false);

    private sealed record Ask(
        IReadOnlyList<IdentifierKind> Kinds,
        MessageKind Message,
        DurationSetting Lifetime,
        bool CarriesLink);

    private async ValueTask<Result> AskAsync(
        string identifier,
        string language,
        string source,
        string? browser,
        Ask ask,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(language);

        Error? failure = null;

        bool usernames = (await configuration
                .ReadAsync(Settings.IdentifiersUsernameEnabled, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        Channel? channel = Read(identifier, usernames, ask);
        (SubjectId Subject, IdentifierId Identifier)? holder = channel is null
            ? null
            : await identifiers
                .HolderAsync(channel.Kind, channel.Canonical, cancellationToken)
                .ConfigureAwait(false);
        SubjectId? owner = holder?.Subject;

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

        // AUTH-FACT-002b: a sign-in link by text is the whole of the sign-in, so a
        // number the carrier reports a recent change of SIM or of network for is
        // refused rather than carrying it. The question is asked of the number and
        // never of the account, so a number no account holds is answered the same way
        // and nothing about existence is told either way (AUTH-ABUSE-003 AC1).
        if (channel.Kind is IdentifierKind.Phone
            && !await signals
                .AllowsAsync(channel.Factor, channel.Canonical, owner, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure(Error.From(ErrorCodes.FactorRejected));
        }

        // Everything from here answers the caller the same way. What differs is what
        // reaches the channel: the message, the non-existence notice, or nothing at
        // all (AUTH-ABUSE-003).
        return holder is { } held
            ? await IssueAsync(held.Subject, held.Identifier, channel, language, source, browser, ask, cancellationToken)
                .ConfigureAwait(false)
            : await TellAsync(channel, language, source, cancellationToken).ConfigureAwait(false);
    }

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

    private async ValueTask<Result> IssueAsync(
        SubjectId subject,
        IdentifierId identifier,
        Channel channel,
        string language,
        string source,
        string? browser,
        Ask ask,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy policy = (await policies.ForAsync(subject, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<Policy>(error, ref failure));

        TimeSpan lifetime = (await configuration.ReadAsync(ask.Lifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // A factor the policy has not enabled is refused exactly as an address no
        // account holds: the caller is told nothing either way, and nothing goes out
        // (AUTH-FACT-003 AC5, AUTH-ABUSE-003).
        if (!policy.LoginFactors.Contains(channel.Factor)
            || await accounts.StateAsync(subject, cancellationToken).ConfigureAwait(false)
                is not AccountState.Active)
        {
            return Result.Success();
        }

        IdentifierId? email = channel.Kind is IdentifierKind.Email ? identifier : null;

        // An address a domain lock refuses is sent nothing, as a factor the policy has
        // not enabled is (REG-DOM-001, AUTH-ABUSE-003).
        if (email is not null
            && EmailAddress.TryParse(channel.Canonical, out EmailAddress address)
            && await domainLock.RefusedAsync(subject, address, cancellationToken).ConfigureAwait(false)
                is Error locked)
        {
            return locked.Code == ErrorCodes.IdentifierDomainNotAllowed
                ? Result.Success()
                : Result.Failure(locked);
        }

        string? settled = await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => (IReadOnlyList<string>)[]);

        string code = VerificationCode.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

        var values = new Dictionary<string, string>(capacity: 2, StringComparer.Ordinal)
        {
            ["code"] = code,
        };

        if (ask.CarriesLink)
        {
            values["token"] = token.Value;
        }

        _ = (await sending
                .SendAsync(
                    new SendRequest(
                        channel.Destination,
                        ask.Message,
                        RestrictionPurpose.SignIn,
                        source,
                        RecipientLanguage.Of(settled, language, languages))
                    {
                        Subject = subject,
                        Values = values,
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(_ => true, error => Withheld<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await pending
            .ReplaceAsync(
                PendingSignIn.Issue(
                    token,
                    subject,
                    channel.Factor,
                    email,
                    code,
                    Fingerprint(browser),
                    time.GetUtcNow(),
                    lifetime),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private sealed record Channel(
        IdentifierKind Kind,
        string Canonical,
        Factor Factor,
        SendDestination Destination);

    // An identifier of a kind this ask has no channel for, a username among them, is
    // read as nothing at all: there is nowhere for the message to go.
    private static Channel? Read(string identifier, bool usernames, Ask ask)
    {
        if (IdentifierKinds.Detect(identifier.Trim(), usernames) is not { } kind
            || !ask.Kinds.Contains(kind))
        {
            return null;
        }

        if (kind is IdentifierKind.Email)
        {
            return EmailAddress.TryParse(identifier.Trim(), out EmailAddress address)
                ? new Channel(kind, address.Value, Of(ask, kind), SendDestination.Of(address))
                : null;
        }

        return PhoneNumber.TryParse(identifier.Trim(), out PhoneNumber number)
            ? new Channel(kind, number.Value, Of(ask, kind), SendDestination.Of(number))
            : null;
    }

    private static Factor Of(Ask ask, IdentifierKind kind) =>
        FactorCatalogue.Sent[(kind, ask.CarriesLink)];

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
