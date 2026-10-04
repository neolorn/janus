using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Factors;

/// <summary>
/// Issues and answers verification codes, which live and die on their own rules
/// wherever they are issued from.
/// </summary>
/// <param name="codes">Where the codes are held.</param>
/// <param name="configuration">Where the lifetime and the attempt cap come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where the digits are drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-004. A code lives <c>code.verification.lifetime</c> whatever
/// issued it, dies after <c>code.verification.attempts</c> wrong tries, and is spent by
/// the first right one, so the same digits never answer twice. Every try is decided
/// under a lock on the code's row, so concurrent tries count as the same number of
/// sequential ones and the right code answers once.
/// </remarks>
internal sealed class VerificationCodes(
    IVerificationCodeStore codes,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Draws a code and holds it against something, replacing whatever that thing had
    /// outstanding.
    /// </summary>
    /// <param name="holder">What the code is issued against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The code as the person reads it, for the message to carry, or the failure the
    /// configuration produced.
    /// </returns>
    /// <exception cref="ArgumentNullException">The holder is absent.</exception>
    public async ValueTask<Result<string>> IssueAsync(
        byte[] holder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.CodeVerificationLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<string>(failure);
        }

        string code = VerificationCode.Draw(randomness);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<string>(notBegun);
        }

        await codes.RemoveAsync(holder, cancellationToken).ConfigureAwait(false);
        await codes
            .AddAsync(
                VerificationCode.Issue(holder, code, time.GetUtcNow(), lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<string>(notCommitted);
        }

        return Result.Success(code);
    }

    /// <summary>
    /// Holds against something the record of a held or reserved value, replacing
    /// whatever that thing had outstanding: it lives and is counted as an issued code
    /// is, and no code presented against it is the right one (AUTH-FACT-004).
    /// </summary>
    /// <param name="holder">What the record stands against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure the configuration produced.</returns>
    /// <exception cref="ArgumentNullException">The holder is absent.</exception>
    public async ValueTask<Result> WithholdAsync(
        byte[] holder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.CodeVerificationLifetime, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        await codes.RemoveAsync(holder, cancellationToken).ConfigureAwait(false);
        await codes
            .AddAsync(
                VerificationCode.Unanswerable(holder, time.GetUtcNow(), lifetime),
                cancellationToken)
            .ConfigureAwait(false);

        return await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers a code.
    /// </summary>
    /// <param name="holder">What the code was issued against.</param>
    /// <param name="entered">The code as it was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the code was the one outstanding, which spends it;
    /// <c>auth.code.invalid</c> where it was wrong, the try that reaches the cap
    /// included, which ends the code; <c>auth.code.expired</c> where none is
    /// outstanding or it has run out of life.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    /// <remarks>
    /// Implements AUTH-FACT-004 and CONV-DESIGN-003. The try is decided in its caller's
    /// unit of work and begins none of its own, so the caller commits the wrong try's
    /// count with the refusal's other kept writes; a try at a code out of life, or with
    /// none outstanding, writes nothing.
    /// </remarks>
    public async ValueTask<Result> PresentAsync(
        byte[] holder,
        [NeverLogged] string entered,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(entered);

        Error? failure = null;

        int cap = (await configuration
                .ReadAsync(Settings.CodeVerificationAttempts, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // AUTH-FACT-004 AC4: the row is read under its lock, so a second try waits for
        // this one to commit and decides on what it left.
        VerificationCode? outstanding = await codes.FindForUpdateAsync(holder, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = time.GetUtcNow();
        Result answer = Judged(outstanding, entered, now);

        // A code out of life is left as it stands, the lapsed record being the sweep's,
        // so that try writes nothing (CONV-DESIGN-003).
        if (outstanding is not null && outstanding.IsLive(now))
        {
            // The right code is spent by the try it answered, and a code out of tries
            // is ended with the try that found it so (AUTH-FACT-004 AC3).
            if (answer.Match(() => true, _ => false) || outstanding.Exhausted(cap))
            {
                await codes.RemoveAsync(holder, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await codes.RecordAsync(outstanding, cancellationToken).ConfigureAwait(false);
            }
        }

        return answer;
    }

    /// <summary>
    /// The digits outstanding against something, which the landing page opened away
    /// from the browser that asked for them shows (REG-SESS-003).
    /// </summary>
    /// <param name="holder">What the code was issued against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The code as the person reads it, or nothing where none is live.</returns>
    /// <exception cref="ArgumentNullException">The holder is absent.</exception>
    public async ValueTask<string?> ShownAsync(
        byte[] holder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);

        VerificationCode? outstanding = await codes.FindAsync(holder, cancellationToken)
            .ConfigureAwait(false);

        return outstanding is not null && outstanding.IsLive(time.GetUtcNow()) && outstanding.IsAnswerable()
            ? VerificationCode.Read(outstanding.Code)
            : null;
    }

    /// <summary>
    /// Ends whatever code is outstanding against something, inside the transaction the
    /// caller opened: what it was issued against was proved another way, changed or
    /// given up.
    /// </summary>
    /// <param name="holder">What the code was issued against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing.</returns>
    /// <exception cref="ArgumentNullException">The holder is absent.</exception>
    public ValueTask EndAsync(byte[] holder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);

        return codes.RemoveAsync(holder, cancellationToken);
    }

    /// <summary>
    /// Whether a code is outstanding against something, which is what says a sign-in
    /// is still held.
    /// </summary>
    /// <param name="holder">What the code was issued against.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether one is outstanding and still live.</returns>
    /// <exception cref="ArgumentNullException">The holder is absent.</exception>
    public async ValueTask<bool> OutstandingAsync(
        byte[] holder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holder);

        VerificationCode? outstanding = await codes.FindAsync(holder, cancellationToken)
            .ConfigureAwait(false);

        return outstanding is not null && outstanding.IsLive(time.GetUtcNow());
    }

    private static Result Expired() => Result.Failure(Error.From(ErrorCodes.CodeExpired));

    // AUTH-FACT-004 AC3: each wrong try is refused as wrong, the one that reaches the
    // cap included; whatever is presented once the code is gone or out of life is
    // refused as expired, the right code included.
    private static Result Judged(
        VerificationCode? outstanding,
        [NeverLogged] string entered,
        DateTimeOffset now)
    {
        if (outstanding is null || !outstanding.IsLive(now))
        {
            return Expired();
        }

        if (outstanding.Is(entered))
        {
            return Result.Success();
        }

        _ = outstanding.Missed();

        return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
