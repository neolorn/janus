using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Passwords;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Factors;

/// <summary>
/// Issuing a set of recovery codes, spending one, and replacing the set.
/// </summary>
/// <param name="sets">Where the account's set is read and written.</param>
/// <param name="hasher">What a code is hashed with, which is what a password is.</param>
/// <param name="configuration">Where the count and the hashing parameters come from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a code is drawn from.</param>
/// <remarks>
/// Implements AUTH-FACT-008 and AUTH-FACT-009. The codes leave the library once, at
/// generation; what is kept is verifiable and never recoverable.
/// </remarks>
internal sealed class RecoveryCodeService(
    IRecoveryCodeStore sets,
    Argon2idHasher hasher,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Issues a set, replacing whatever the account held: no code of the previous set
    /// validates afterwards.
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The codes, returned once and never read back.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> GenerateAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        int count = (await configuration.ReadAsync(Settings.FactorRecoveryCodesCount, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        Argon2StrengthClass parameters =
            (await ParametersAsync(cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<Argon2StrengthClass>(error, ref failure));

        int parallelism = (await configuration.ReadAsync(Settings.PasswordArgon2Parallelism, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<string>>(failure);
        }

        List<string> drawn = [];
        List<PasswordHash> hashes = [];

        for (int code = 0; code < count; code++)
        {
            string issued = RecoveryCode.Draw(randomness);
            byte[] presented = Encoding.UTF8.GetBytes(RecoveryCode.Canonical(issued));

            try
            {
                hashes.Add(hasher.Hash(presented, parameters, parallelism));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(presented);
            }

            drawn.Add(issued);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sets.ReplaceAsync(
                RecoveryCodeSet.Of(subject, hashes, time.GetUtcNow()),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success<IReadOnlyList<string>>(drawn);
    }

    /// <summary>
    /// Records that the codes were shown, which is what lets the account say whether
    /// the person ever saw them.
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="exported">Whether they were copied, downloaded or printed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure where the account holds no set.</returns>
    public async ValueTask<Result> ShownAsync(
        SubjectId subject,
        bool exported,
        CancellationToken cancellationToken)
    {
        RecoveryCodeSet? held = await sets.FindAsync(subject, cancellationToken).ConfigureAwait(false);

        if (held is null)
        {
            return Result.Failure(Error.From(ErrorCodes.FactorRejected));
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        held.Viewed(now);

        if (exported)
        {
            held.Exported(now);
        }

        await sets.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// Spends one code of the account's set.
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="code">The code as it was typed.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing, or the failure where the account holds no set, the code is not one of
    /// it, or it has been spent.
    /// </returns>
    public async ValueTask<Result> SpendAsync(
        SubjectId subject,
        string code,
        CancellationToken cancellationToken)
    {
        RecoveryCodeSet? held = await sets.FindAsync(subject, cancellationToken).ConfigureAwait(false);

        if (held is null || !held.Spend(code, time.GetUtcNow()))
        {
            return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await sets.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// How many of the account's codes are still unused, which the account shows.
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The count, nought where the account holds no set.</returns>
    public async ValueTask<Result<int>> RemainingAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        Result.Success(
            (await sets.FindAsync(subject, cancellationToken).ConfigureAwait(false))?.Remaining ?? 0);

    /// <summary>
    /// Whether the account's set is old enough for the one reminder it gets, and
    /// records that the reminder was sent where it is.
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether a reminder is due now.</returns>
    public async ValueTask<Result<bool>> RemindAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        RecoveryCodeSet? held = await sets.FindAsync(subject, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = time.GetUtcNow();

        TimeSpan after = (await configuration.ReadAsync(Settings.RecoveryCodesReminder, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => Settings.RecoveryCodesReminder.Default);

        if (held is null || !held.RemindsAt(now, after))
        {
            return Result.Success(false);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        held.Reminded(now);
        await sets.RecordAsync(held, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(true);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<Result<Argon2StrengthClass>> ParametersAsync(
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        int memory = (await configuration.ReadAsync(Settings.PasswordArgon2Memory, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        int iterations = (await configuration.ReadAsync(Settings.PasswordArgon2Iterations, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        return failure is not null
            ? Result.Failure<Argon2StrengthClass>(failure)
            : Result.Success(new Argon2StrengthClass(memory, iterations));
    }
}
