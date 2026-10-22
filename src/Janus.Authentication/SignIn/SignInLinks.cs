using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
/// <param name="authenticators">Where the credential a code is issued for is read under its lock.</param>
/// <param name="accounts">Where the account's state is read.</param>
/// <param name="policies">What policy governs the account.</param>
/// <param name="domainLock">Whether an email address is one a member may sign in with.</param>
/// <param name="sending">Where a message goes out.</param>
/// <param name="restrictions">
/// What a send is named to by the code or the link it carries, and what releases its
/// one count once that is answered.
/// </param>
/// <param name="landing">Where a link the message carries lands.</param>
/// <param name="nonExistence">What answers an ask no message of its own answers.</param>
/// <param name="signals">What is known about a number before a text leans on it.</param>
/// <param name="throttle">The progressive delay.</param>
/// <param name="configuration">Where the lifetimes and the limits come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a token and a code are drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-003, AUTH-ABUSE-002, AUTH-ABUSE-003, AUTH-ABUSE-004,
/// REG-SESS-003 and REG-DOM-001. Asking always succeeds: an identifier no account holds,
/// an identifier whose channel the policy has not enabled, an account that cannot be
/// signed into and an address a domain lock refuses each produce the same answer as one
/// that can, and differ only in what arrives at the channel. Each counts against the
/// sending restrictions as the message would, so where a restriction refuses the ask it
/// refuses all of them alike.
/// </remarks>
internal sealed class SignInLinks(
    IPendingSignInStore pending,
    IIdentifierDirectory identifiers,
    IAuthenticatorStore authenticators,
    IAccountDirectory accounts,
    PolicyResolution policies,
    DomainLock domainLock,
    IGovernedSend sending,
    ISendingRestrictions restrictions,
    LandingLinks landing,
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
        [NeverLogged] string linkToken,
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
    /// <returns>
    /// Success, which spends it; <c>auth.code.invalid</c> where the code was wrong, the
    /// try that reaches <c>code.signin.attempts</c> included, which ends it;
    /// <c>auth.code.expired</c> where it is gone or has lapsed.
    /// </returns>
    /// <exception cref="ArgumentNullException">The pending sign-in is absent.</exception>
    /// <remarks>
    /// Implements AUTH-FACT-004. The code is an authentication code, held to its own
    /// cap, and every try is decided under a lock on the pending sign-in's row, so
    /// concurrent tries count as the same number of sequential ones and the right code
    /// answers once. The try is decided in its caller's unit of work and begins none of
    /// its own, so the caller commits the wrong try's count with the refusal's other
    /// kept writes (CONV-DESIGN-003).
    /// </remarks>
    public async ValueTask<Result> SpendCodeAsync(
        PendingSignIn held,
        [NeverLogged] string entered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        Error? failure = null;

        int attempts = (await configuration
                .ReadAsync(Settings.CodeSigninAttempts, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        PendingSignIn? locked = await pending
            .FindForUpdateAsync(held.Fingerprint, cancellationToken)
            .ConfigureAwait(false);

        // AUTH-FACT-004 AC3: whatever is presented once the code is gone or has lapsed is
        // refused as expired, the right code included, and that refusal writes nothing,
        // the lapsed record being the sweep's.
        if (locked is null || locked.HasExpired(time.GetUtcNow()))
        {
            return Result.Failure(Error.From(ErrorCodes.CodeExpired));
        }

        Result answer = Spent(locked, entered);

        // The right code is spent by the try it answered, and enough wrong codes end
        // the link, which is what stops a six-digit code being guessed at leisure
        // (AUTH-FACT-004 AC3). A wrong try's count stands whatever the outcome, so the
        // caller commits the refusal that keeps it (CONV-DESIGN-003).
        if (answer.Match(() => true, _ => false) || locked.WrongAttempts >= attempts)
        {
            await pending.RemoveAsync(locked.Fingerprint, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await pending.RecordAsync(locked, cancellationToken).ConfigureAwait(false);
        }

        return answer;
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

        // REG-IDENT-006 AC6: what went to an address the account has given up since
        // signs nothing in.
        return standing.Find(email) is HeldIdentifier sent
            ? await domainLock.RefusedAsync(held.Subject, sent.Canonical, cancellationToken).ConfigureAwait(false)
            : Error.From(ErrorCodes.FactorRejected);
    }

    /// <summary>
    /// Texts the code of a second step to the account's number, issued for the one
    /// sign-in or step-up it was asked for and for the one credential it names.
    /// </summary>
    /// <param name="challenge">What the handle of that sign-in or step-up hashes to.</param>
    /// <param name="credential">
    /// The credential the code is issued for, of the account texted and of the second
    /// step the code is.
    /// </param>
    /// <param name="source">The address the ask came from.</param>
    /// <param name="language">The language the ask was made in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the ask is answered as one that sent its code, which it is not where the
    /// number's signal withheld it, or where the credential was taken out of use or its
    /// number left the account before the code was issued (AUTH-FACT-002 AC11); or the
    /// refusal: of the send, or <c>auth.credential.suspended</c> where the credential is
    /// by then suspended.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <remarks>
    /// Implements AUTH-FACT-002 AC6, AUTH-FACT-002b AC6 and AUTH-FACT-004. The code is an
    /// authentication code, living <c>code.signin.lifetime</c> and capped by
    /// <c>code.signin.attempts</c>, and it goes out as <c>secondstep-code</c> under the
    /// purpose <c>secondfactor</c>. Where the carrier reports a recent change of SIM or
    /// of network for the number, nothing is issued and nothing goes out, the
    /// consideration is recorded, and the caller is told so, since what the ask is
    /// answered with then depends on where it was made (AUTH-FACT-002 AC7). The code's
    /// record names the credential it is issued for, which is what it is judged against
    /// when it is presented (AUTH-FACT-004 AC7).
    /// </remarks>
    public async ValueTask<Result<bool>> SendSecondStepAsync(
        [NeverLogged] byte[] challenge,
        Authenticator credential,
        string source,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(language);

        SubjectId subject = credential.Subject;
        Factor factor = credential.Factor;
        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.CodeSigninLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        // AUTH-FACT-002: the code goes to the number the credential is bound to and to
        // no other. One bound to none is sent nothing and answered as an ask that sent
        // its code; only the carrier's signal is told apart.
        if (credential.Number is null || !PhoneNumber.TryParse(credential.Number, out PhoneNumber texted))
        {
            return Result.Success(true);
        }

        if (!await signals.AllowsAsync(factor, texted.Value, subject, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Success(false);
        }

        string? settled = await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        string code = VerificationCode.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

        // The identifier that holds the credential's number, as it was read before the
        // unit of work began, which is the row the unit of work locks first.
        IdentifierId? holding = Holding(
            await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false),
            texted.Value)?.Id;

        if ((await work.BeginOutermostAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<bool>(notBegun);
        }

        // CONV-DESIGN-003: every writer that takes a number from the account holds the
        // account's row before the identifier's, and the record of the code refers to
        // that row, so it is held here first: taken after the identifier's, the two
        // would wait on each other.
        await accounts.HoldAsync(subject, cancellationToken).ConfigureAwait(false);

        // AUTH-FACT-002 AC11, CONV-DESIGN-003 (D-197): the row of the identifier holding
        // the number is locked next, where one still does, and the credential is read
        // under its own lock after it, so a writer that takes the number from the
        // account is either seen here or waits for this code to be issued. Where the
        // credential is no longer active, or the number no longer the account's,
        // nothing is issued, sent or counted, and the consideration is the one write
        // kept: as the signal answered, or unavailable where none is declared.
        HeldIdentifier? locked = holding is IdentifierId row
            ? await identifiers.LockAsync(subject, row, cancellationToken).ConfigureAwait(false)
            : null;
        Authenticator? standing = await authenticators.FindForUpdateAsync(credential.Id, cancellationToken)
            .ConfigureAwait(false);

        if (standing is not { IsUsable: true }
            || !string.Equals(standing.Number, texted.Value, StringComparison.Ordinal)
            || locked is not { IsPending: false }
            || !string.Equals(locked.Canonical, texted.Value, StringComparison.Ordinal))
        {
            await signals.UnsentAsync(factor, texted.Value, subject, cancellationToken).ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error unkept)
            {
                return Result.Failure<bool>(unkept);
            }

            // A credential now suspended is answered as AUTH-RECOV-007 states; any other
            // is withheld from the challenge, as at a risk.
            return standing is { IsAwaitingInvalidation: true }
                ? Result.Failure<bool>(Error.From(ErrorCodes.CredentialSuspended))
                : Result.Success(false);
        }

        SendReference sent = (await sending
                .UndertakeAsync(
                    new OutboundMessage(
                        SendDestination.Of(texted),
                        MessageKind.SecondStepCode,
                        RestrictionPurpose.SecondFactor,
                        source,
                        RecipientLanguage.Of(settled, language, languages))
                    {
                        Subject = subject,
                        Values = new Dictionary<string, string>(capacity: 1, StringComparer.Ordinal)
                        {
                            ["code"] = code,
                        },
                    },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(reference => reference, error => Withheld<SendReference>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<bool>(failure);
        }

        // AUTH-ABUSE-004: the send is named by the record its code is held on, which is
        // what releases its one count once the code is presented right.
        await restrictions.CarriesAsync(sent, token.Fingerprint(), cancellationToken).ConfigureAwait(false);

        await pending
            .ReplaceAsync(
                PendingSignIn.Issue(
                    token,
                    subject,
                    factor,
                    email: null,
                    credential.Id,
                    code,
                    browser: null,
                    challenge,
                    time.GetUtcNow(),
                    lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<bool>(notCommitted);
        }

        return Result.Success(true);
    }

    /// <summary>
    /// Ends one whose link was pressed, inside the transaction the caller opened, and
    /// releases what the send that carried the link counted once (AUTH-ABUSE-004).
    /// </summary>
    /// <param name="held">The pending sign-in.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    /// <exception cref="ArgumentNullException">The pending sign-in is absent.</exception>
    public async ValueTask SpendAsync(PendingSignIn held, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        await pending.RemoveAsync(held.Fingerprint, cancellationToken).ConfigureAwait(false);
        await restrictions.AnsweredAsync(held.Fingerprint, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases what the send that carried a code counted once, inside the transaction
    /// in which the code was presented right, as a write of its spend.
    /// </summary>
    /// <param name="held">The pending sign-in the code was held on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    /// <exception cref="ArgumentNullException">The pending sign-in is absent.</exception>
    /// <remarks>
    /// Implements AUTH-ABUSE-004. A wrong code releases nothing. A right one releases
    /// also where a refusal follows it: the release is a kept write, committed with the
    /// spend (CONV-DESIGN-003).
    /// </remarks>
    public ValueTask AnsweredAsync(PendingSignIn held, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        return restrictions.AnsweredAsync(held.Fingerprint, cancellationToken);
    }

    /// <summary>
    /// Names the send that carried the code of the new-device check by that code and
    /// what holds it, inside the transaction that undertook it.
    /// </summary>
    /// <param name="sent">The reference the send was admitted under.</param>
    /// <param name="holder">What the code is held under.</param>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    public ValueTask CheckSentAsync(
        SendReference sent,
        [NeverLogged] byte[] holder,
        [NeverLogged] string code,
        CancellationToken cancellationToken) =>
        restrictions.CarriesAsync(sent, SendReferences.Naming(holder, code), cancellationToken);

    /// <summary>
    /// Releases what the send that carried the code of the new-device check counted
    /// once, inside the transaction in which the code was presented right.
    /// </summary>
    /// <param name="holder">What the code is held under.</param>
    /// <param name="code">The code as it was presented.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    public ValueTask CheckAnsweredAsync(
        [NeverLogged] byte[] holder,
        [NeverLogged] string code,
        CancellationToken cancellationToken) =>
        restrictions.AnsweredAsync(SendReferences.Naming(holder, code), cancellationToken);

    /// <summary>
    /// Ends a pending link, whether or not the token resolves to one.
    /// </summary>
    /// <param name="linkToken">The token the message carried.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, always.</returns>
    public async ValueTask<Result> AbandonAsync(
        [NeverLogged] string linkToken,
        CancellationToken cancellationToken)
    {
        if (linkToken is { Length: > 0 })
        {
            if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(_ => null, error => error) is Error notBegun)
            {
                return Result.Failure(notBegun);
            }

            await pending
                .RemoveAsync(OpaqueToken.Of(linkToken).Fingerprint(), cancellationToken)
                .ConfigureAwait(false);

            if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error) is Error notCommitted)
            {
                return Result.Failure(notCommitted);
            }
        }

        return Result.Success();
    }

    // What each kind of ask sends, and on which channels. The email code is a code to
    // type and carries no link; a link carries both, because the browser that opened
    // it elsewhere shows the code (REG-SESS-003). Both codes are authentication codes:
    // the email code lives code.signin.lifetime and the code a link shows lives as long
    // as its link (AUTH-FACT-004).
    private static readonly Ask Links = new(
        [IdentifierKind.Email, IdentifierKind.Phone],
        MessageKind.SignInLink,
        Settings.LinkMagicLifetime,
        CarriesLink: true);

    private static readonly Ask Codes = new(
        [IdentifierKind.Email],
        MessageKind.SignInCode,
        Settings.CodeSigninLifetime,
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
                    new ThrottleAttempt(source, throttle.Identify(identifier, usernames)) { Account = owner },
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (delay > TimeSpan.Zero)
        {
            return Result.Failure(Error.Throttled(time.GetUtcNow() + delay));
        }

        if (channel is null)
        {
            return Result.Success();
        }

        // AUTH-FACT-002b AC6: a sign-in link by text is the whole of the sign-in, so
        // nothing goes to a number the carrier reports a recent change of SIM or of
        // network for, and the ask is answered as every ask is. The question is asked
        // of the number and never of the account, so a number no account holds is
        // answered the same way and nothing about existence is told either way
        // (AUTH-ABUSE-003 AC1).
        if (channel.Kind is IdentifierKind.Phone
            && !await signals
                .AllowsAsync(channel.Factor, channel.Canonical, owner, cancellationToken)
                .ConfigureAwait(false))
        {
            return await WithheldAsync(channel, language, source, ask, unheld: false, cancellationToken)
                .ConfigureAwait(false);
        }

        // Everything from here answers the caller the same way. What differs is what
        // reaches the channel: the message, the non-existence notice, or nothing at
        // all (AUTH-ABUSE-003). Each of them counts against the sending restrictions as
        // the message would, so a restriction refuses alike too (AUTH-ABUSE-002 AC3).
        return holder is { } held
            ? await IssueAsync(held.Subject, held.Identifier, channel, language, source, browser, ask, cancellationToken)
                .ConfigureAwait(false)
            : await WithheldAsync(channel, language, source, ask, unheld: true, cancellationToken)
                .ConfigureAwait(false);
    }

    // An ask no message answers, because no account holds the address or the account
    // cannot be reached by it, is answered as the one and counted as the other.
    private ValueTask<Result> WithheldAsync(
        Channel channel,
        string language,
        string source,
        Ask ask,
        bool unheld,
        CancellationToken cancellationToken) =>
        nonExistence.AnswerAsync(
            channel.Destination,
            ask.Message,
            RestrictionPurpose.SignIn,
            source,
            language,
            unheld,
            cancellationToken);

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
                is not (AccountState.Active or AccountState.Restricted))
        {
            return await WithheldAsync(channel, language, source, ask, unheld: false, cancellationToken)
                .ConfigureAwait(false);
        }

        IdentifierId? email = channel.Kind is IdentifierKind.Email ? identifier : null;

        // An address a domain lock refuses is sent nothing, as a factor the policy has
        // not enabled is (REG-DOM-001, AUTH-ABUSE-003).
        if (email is not null
            && await domainLock.RefusedAsync(subject, channel.Canonical, cancellationToken).ConfigureAwait(false)
                is Error locked)
        {
            return locked.Code == ErrorCodes.IdentifierDomainNotAllowed
                ? await WithheldAsync(channel, language, source, ask, unheld: false, cancellationToken)
                    .ConfigureAwait(false)
                : Result.Failure(locked);
        }

        string? settled = await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        string code = VerificationCode.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

        var values = new Dictionary<string, string>(capacity: 2, StringComparer.Ordinal)
        {
            ["code"] = code,
        };

        if (ask.CarriesLink)
        {
            values["link"] = landing.Of(LinkKind.SignIn, token.Value);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        SendReference sent = (await sending
                .UndertakeAsync(
                    new OutboundMessage(
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
            .Match(reference => reference, error => Withheld<SendReference>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            // AUTH-ABUSE-006: the gateway floor's refusal answers the ask as it would
            // have been answered, as it does for a number no account holds, so the
            // floor tells nothing of an account. Nothing else was written.
            return failure.Code == ErrorCodes.SmsBalanceFloor ? Result.Success() : Result.Failure(failure);
        }

        // AUTH-ABUSE-004: the send is named by the record its code and its link are
        // held on, which is what releases its one count once either is answered.
        await restrictions.CarriesAsync(sent, token.Fingerprint(), cancellationToken).ConfigureAwait(false);

        await pending
            .ReplaceAsync(
                PendingSignIn.Issue(
                    token,
                    subject,
                    channel.Factor,
                    email,
                    credential: null,
                    code,
                    Fingerprint(browser),
                    challenge: null,
                    time.GetUtcNow(),
                    lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // AUTH-FACT-004 AC3: each wrong try is refused as wrong, the one that reaches the
    // cap included.
    private static Result Spent(PendingSignIn locked, [NeverLogged] string entered)
    {
        if (locked.Matches(entered))
        {
            return Result.Success();
        }

        locked.Missed();

        return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
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

    // The identifier of the account that holds a number: one the account lists as its
    // own, which a pending add is not yet.
    private static HeldIdentifier? Holding(HeldIdentifiers held, string number) =>
        held.All.FirstOrDefault(identifier =>
            identifier is { Kind: IdentifierKind.Phone, IsPending: false }
            && string.Equals(identifier.Canonical, number, StringComparison.Ordinal));
}
