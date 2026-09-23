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
/// the first right one, so the same digits never answer twice.
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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await codes.RemoveAsync(holder, cancellationToken).ConfigureAwait(false);
        await codes
            .AddAsync(
                VerificationCode.Issue(holder, code, time.GetUtcNow(), lifetime),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(code);
    }

    /// <summary>
    /// Answers a code.
    /// </summary>
    /// <param name="holder">What the code was issued against.</param>
    /// <param name="entered">The code as it was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where the code was the one outstanding, which spends it;
    /// <c>auth.code.invalid</c> where it was wrong and tries remain;
    /// <c>auth.code.expired</c> where none is outstanding, where it has run out of
    /// life or where this wrong try was the last one it had.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result> PresentAsync(
        byte[] holder,
        string entered,
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

        VerificationCode? outstanding = await codes.FindAsync(holder, cancellationToken)
            .ConfigureAwait(false);

        if (outstanding is null)
        {
            return Expired();
        }

        if (!outstanding.IsLive(time.GetUtcNow()))
        {
            await EndAsync(holder, cancellationToken).ConfigureAwait(false);

            return Expired();
        }

        if (outstanding.Is(entered))
        {
            // AUTH-FACT-004 AC3: the code is spent by the try that answered it, so the
            // same digits never answer a second time.
            await EndAsync(holder, cancellationToken).ConfigureAwait(false);

            return Result.Success();
        }

        _ = outstanding.Missed();

        // Enough wrong tries end the code, and a correct one afterwards is refused
        // with it: what the person does next is ask for another (AUTH-FACT-004).
        if (outstanding.Exhausted(cap))
        {
            await EndAsync(holder, cancellationToken).ConfigureAwait(false);

            return Expired();
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await codes.RecordAsync(outstanding, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
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

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask EndAsync(byte[] holder, CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await codes.RemoveAsync(holder, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
