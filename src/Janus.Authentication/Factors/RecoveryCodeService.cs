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
    /// validates afterwards. The set is written as viewed, since the response of the
    /// operation that calls this returns the codes (AUTH-FACT-008).
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The codes, returned once and never read back.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> GenerateAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        PreparedRecoveryCodes drawn = (await PrepareAsync(cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<PreparedRecoveryCodes>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<string>>(failure);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<IReadOnlyList<string>>(notBegun);
        }

        DateTimeOffset now = time.GetUtcNow();
        var issued = RecoveryCodeSet.Of(subject, drawn.Hashes, now);

        // AUTH-FACT-008: the response of the unit of work this write joins returns the
        // codes, the one time they are shown, so the set is viewed where it is written.
        issued.Viewed(now);

        await sets.ReplaceAsync(issued, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<IReadOnlyList<string>>(notCommitted);
        }

        return Result.Success(drawn.Codes);
    }

    /// <summary>
    /// Draws a set without writing it, which the security step of a registration
    /// needs because no account holds it yet.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The codes and their hashes, or the failure a setting produced.</returns>
    public async ValueTask<Result<PreparedRecoveryCodes>> PrepareAsync(
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
            return Result.Failure<PreparedRecoveryCodes>(failure);
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

        return Result.Success(new PreparedRecoveryCodes(drawn, hashes));
    }

    /// <summary>
    /// Whether the account holds a set, which a report of an export is refused without
    /// before any unit of work begins.
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>Whether a set stands.</returns>
    public async ValueTask<bool> IssuedAsync(SubjectId subject, CancellationToken cancellationToken) =>
        await sets.FindAsync(subject, cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>
    /// Records that the codes were copied, downloaded or printed, which is what lets
    /// the account say whether the person ever saved them. The first report stands.
    /// It begins no unit of work: it writes in its caller's, which decides on its answer
    /// whether anything is kept (CONV-DESIGN-003).
    /// </summary>
    /// <param name="subject">Whose set.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Whether the report was written, which it is not where an earlier one stands, or
    /// the failure where the account holds no set.
    /// </returns>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<Result<bool>> ExportedAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        // CONV-DESIGN-003: recording the set writes each code's spend as the set read
        // holds it, so the set is read under its lock and a code spent meanwhile stays
        // spent.
        RecoveryCodeSet? held = await sets.FindForUpdateAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (held is null)
        {
            return Result.Failure<bool>(Error.From(ErrorCodes.FactorNotEnrolled));
        }

        if (held.ExportedAt is not null)
        {
            return Result.Success(false);
        }

        held.Exported(time.GetUtcNow());

        await sets.RecordAsync(held, cancellationToken).ConfigureAwait(false);

        return Result.Success(true);
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
        [NeverLogged] string code,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // D-166 X3: the set is read under its lock, so a second presentation of the
        // same code waits for this one to commit and finds it spent.
        RecoveryCodeSet? held = await sets.FindForUpdateAsync(subject, cancellationToken)
            .ConfigureAwait(false);

        if (held is null || !held.Spend(code, time.GetUtcNow()))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.CodeInvalid));
        }

        await sets.RecordAsync(held, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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
