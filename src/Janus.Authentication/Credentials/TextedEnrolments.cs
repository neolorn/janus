using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Identifiers;
using Janus.Authentication.Recovery;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Credentials;

/// <summary>
/// The enrolment of a second step the library texts: a code texted to one of the
/// account's verified numbers, which, presented, makes a credential bound to that
/// number.
/// </summary>
/// <param name="pending">Where an enrolment not yet confirmed is held.</param>
/// <param name="authenticators">Where the credential is written.</param>
/// <param name="reports">Where the report against a credential it retires is closed.</param>
/// <param name="identifiers">Where the number is read and its identifier held.</param>
/// <param name="signals">What is known about the number.</param>
/// <param name="sending">Where the code goes out.</param>
/// <param name="restrictions">What names the send, and releases its count.</param>
/// <param name="configuration">Where the lifetime and the tries come from.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a code is drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-002, AUTH-FACT-002b, AUTH-FACT-004 and CONV-DESIGN-003. The
/// signal of the number is asked before the unit of work, the host's callback never
/// running inside one. Inside it, the row of the identifier that holds the number is
/// locked before the pending enrolment, the credentials and the send are read or
/// written, and what the lock shows is what is judged: an identifier that no longer
/// holds, verified, the number stages nothing and confirms nothing. Each method here
/// writes inside the unit of work its caller opened, and the caller ends it.
/// </remarks>
internal sealed class TextedEnrolments(
    IPendingEnrolmentStore pending,
    IAuthenticatorStore authenticators,
    ILossReportStore reports,
    IIdentifierDirectory identifiers,
    PhoneSignals signals,
    IGovernedSend sending,
    ISendingRestrictions restrictions,
    IConfigurationStore configuration,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// The entry this enrols: the code the library texts as a second step.
    /// </summary>
    public static Factor Entry { get; } = FactorCatalogue.Sent[(IdentifierKind.Phone, false)];

    /// <summary>
    /// Reads the number an enrolment names and asks its signal, before the unit of
    /// work begins.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="identifier">The identifier the enrolment names.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The number, canonically; or <c>identity.identifier.invalid</c> where the
    /// identifier is no verified number of the account, and
    /// <c>auth.factor.notpermitted</c> where the signal of the number answers a risk,
    /// the consideration recorded.
    /// </returns>
    public async ValueTask<Result<string>> ConsideredAsync(
        SubjectId subject,
        IdentifierId identifier,
        CancellationToken cancellationToken)
    {
        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false);

        if (!Holds(held.Find(identifier), out string number))
        {
            return Result.Failure<string>(Error.From(ErrorCodes.IdentifierInvalid));
        }

        return await signals.AllowsAsync(Entry, number, subject, cancellationToken).ConfigureAwait(false)
            ? Result.Success(number)
            : Result.Failure<string>(Error.From(ErrorCodes.FactorNotPermitted));
    }

    /// <summary>
    /// Stages the enrolment and texts its code, inside the unit of work the caller
    /// opened.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="identifier">The identifier the enrolment names.</param>
    /// <param name="number">The number read before the unit of work began.</param>
    /// <param name="label">What the person would call the credential.</param>
    /// <param name="source">The address the request came from.</param>
    /// <param name="language">The language the request asked for.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where it is staged and its code undertaken; or the refusal, after which
    /// the caller writes nothing.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> StageAsync(
        SubjectId subject,
        IdentifierId identifier,
        [NeverLogged] string number,
        CredentialLabel label,
        string source,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(number);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(language);

        // AUTH-FACT-002 AC9: the identifier's row is locked before anything bound to
        // the number is read or written, and what is judged is the identifier as it
        // stands under that lock.
        if (!Holds(await identifiers.LockAsync(subject, identifier, cancellationToken).ConfigureAwait(false), out string held)
            || !string.Equals(held, number, StringComparison.Ordinal)
            || !PhoneNumber.TryParse(number, out PhoneNumber texted))
        {
            return Result.Failure(Error.From(ErrorCodes.IdentifierInvalid));
        }

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
            return Result.Failure(failure);
        }

        // The enrolment this one replaces is held, so a confirmation of it at once
        // waits and then finds the one that stands.
        _ = await pending.FindForUpdateAsync(subject, Entry, cancellationToken).ConfigureAwait(false);

        string? settled = await identifiers.LanguageAsync(subject, cancellationToken).ConfigureAwait(false);
        string code = VerificationCode.Draw(randomness);
        var token = OpaqueToken.Draw(randomness);

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
            return Result.Failure(failure);
        }

        // AUTH-ABUSE-004: the send is named by the record its code is held on, which is
        // what releases its one count once the code is presented right.
        await restrictions.CarriesAsync(sent, token.Fingerprint(), cancellationToken).ConfigureAwait(false);

        await pending
            .ReplaceAsync(
                PendingEnrolment.Stage(token, subject, Entry, label, number, code, time.GetUtcNow(), lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Judges the code presented against the pending enrolment, inside the unit of
    /// work the caller opened, and on a right one writes the credential bound to the
    /// number and retires every other credential of the entry.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="code">What was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The credential enrolled and those retired; or the refusal, with whether it
    /// keeps a write the caller commits (a wrong try's count, CONV-DESIGN-003).
    /// </returns>
    /// <exception cref="ArgumentNullException">The code is absent.</exception>
    public async ValueTask<TextedConfirmation> ConfirmAsync(
        SubjectId subject,
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(code);

        Error? failure = null;

        int attempts = (await configuration
                .ReadAsync(Settings.CodeSigninAttempts, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return TextedConfirmation.Refused(failure, kept: false);
        }

        PendingEnrolment? staged = await pending.FindAsync(subject, Entry, cancellationToken).ConfigureAwait(false);

        if (staged is null)
        {
            return Expired;
        }

        HeldIdentifiers held = await identifiers.HeldAsync(subject, cancellationToken).ConfigureAwait(false);
        HeldIdentifier? holding = held.All.FirstOrDefault(identifier =>
            Holds(identifier, out string number) && string.Equals(number, staged.Number, StringComparison.Ordinal));

        // AUTH-FACT-002 AC10: the identifier's row is locked before the enrolment, its
        // code and the credentials, and a number that has left the account, or leaves
        // it before the lock is taken, confirms nothing.
        if (holding is null
            || !Holds(await identifiers.LockAsync(subject, holding.Id, cancellationToken).ConfigureAwait(false), out string standing)
            || !string.Equals(standing, staged.Number, StringComparison.Ordinal))
        {
            return Expired;
        }

        PendingEnrolment? locked = await pending
            .FindForUpdateAsync(subject, Entry, cancellationToken)
            .ConfigureAwait(false);

        // AUTH-FACT-004 AC3: whatever is presented once the code is gone or has lapsed
        // is refused as expired, the right code included, and that refusal writes
        // nothing, the lapsed record being the sweep's. An enrolment staged meanwhile
        // for another number is not the one whose identifier is held.
        if (locked is null
            || locked.HasExpired(time.GetUtcNow())
            || !string.Equals(locked.Number, staged.Number, StringComparison.Ordinal))
        {
            return Expired;
        }

        if (!locked.Matches(code))
        {
            locked.Missed();

            // Enough wrong codes end the enrolment, which is what stops a six-digit
            // code being guessed at leisure; the count is the code's own and nothing
            // else is counted or recorded (AUTH-FACT-002).
            if (locked.WrongAttempts >= attempts)
            {
                await pending.RemoveAsync(subject, Entry, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await pending.RecordAsync(locked, cancellationToken).ConfigureAwait(false);
            }

            return TextedConfirmation.Refused(Error.From(ErrorCodes.CodeInvalid), kept: true);
        }

        await pending.RemoveAsync(subject, Entry, cancellationToken).ConfigureAwait(false);
        await restrictions.AnsweredAsync(locked.Send, cancellationToken).ConfigureAwait(false);

        // AUTH-FACT-002 AC9: an account holds one credential of the entry, so every
        // other is retired in the unit of work that makes this one active, read under
        // their locks, with the report that was running against any of them.
        var retired = new List<AuthenticatorId>();

        foreach (Authenticator other in (await authenticators
                     .OfForUpdateAsync(subject, cancellationToken)
                     .ConfigureAwait(false))
                 .Where(credential => credential.Factor == Entry))
        {
            await reports.RemoveAsync(other.Id, cancellationToken).ConfigureAwait(false);
            await authenticators.RemoveAsync(other.Id, cancellationToken).ConfigureAwait(false);

            retired.Add(other.Id);
        }

        var enrolled = Authenticator.Bound(
            AuthenticatorId.New(time),
            subject,
            Entry,
            locked.Label,
            locked.Number,
            time.GetUtcNow());

        await authenticators.AddAsync(enrolled, cancellationToken).ConfigureAwait(false);

        return new TextedConfirmation(enrolled.Id, retired, Refusal: null, Kept: true);
    }

    private static TextedConfirmation Expired { get; } =
        TextedConfirmation.Refused(Error.From(ErrorCodes.CodeExpired), kept: false);

    // Whether an identifier is one an enrolment may name: a number of the account,
    // verified.
    private static bool Holds(HeldIdentifier? identifier, out string number)
    {
        number = identifier?.Canonical ?? string.Empty;

        return identifier is { Kind: IdentifierKind.Phone, IsVerified: true };
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
