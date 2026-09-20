using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Passwords;

/// <summary>
/// Setting a password and presenting one: the length rule, the screening, the hashing
/// and the silent rehash, in the one order every path through them takes.
/// </summary>
/// <param name="passwords">Where the account's password is read and written.</param>
/// <param name="screening">What every password is screened against.</param>
/// <param name="hasher">What a password is hashed with.</param>
/// <param name="configuration">Where the floors and the parameters are read from.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-PASS-001, AUTH-PASS-001a, AUTH-PASS-002, AUTH-PASS-004,
/// AUTH-PASS-005 and AUTH-PASS-007. Nothing here imposes a composition rule: a
/// password is refused for its length or for a source that rejected it, and for
/// nothing else.
/// </remarks>
internal sealed class PasswordService(
    IPasswordStore passwords,
    PasswordScreening screening,
    Argon2idHasher hasher,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Puts a password through the floor, the screening and the hashing, and stops
    /// short of storing it. Registration prepares one because no account exists to
    /// store it against until the terms step.
    /// </summary>
    /// <param name="password">The password, in UTF-8. The caller clears it.</param>
    /// <param name="ownWords">
    /// The person's own identifiers and profile fields, and the service name.
    /// </param>
    /// <param name="reachable">
    /// What the account reaches with the credentials it holds, which is what decides
    /// whether the shorter floor applies (AUTH-PASS-001a).
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the password hashes to and what to show beside it, or the failure that
    /// refused it.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<PreparedPassword>> PrepareAsync(
        byte[] password,
        IReadOnlyCollection<string> ownWords,
        AssuranceLevel reachable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(ownWords);

        Error? failure = null;

        int singleFactor = (await configuration
                .ReadAsync(Settings.PasswordFloorSingleFactor, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        int withMfa = (await configuration
                .ReadAsync(Settings.PasswordFloorWithMfa, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        int maximum = (await configuration
                .ReadAsync(Settings.PasswordMaximum, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        Argon2StrengthClass parameters =
            (await ParametersAsync(cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<Argon2StrengthClass>(error, ref failure));

        int parallelism = (await configuration
                .ReadAsync(Settings.PasswordArgon2Parallelism, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<PreparedPassword>(failure);
        }

        int characters = PasswordFloor.Characters(password);

        PasswordFloor.Admits(characters, singleFactor, withMfa, maximum, reachable)
            .Switch(() => { }, error => failure = error);

        if (failure is not null)
        {
            return Result.Failure<PreparedPassword>(failure);
        }

        (await screening.ScreenAsync(password, ownWords, cancellationToken).ConfigureAwait(false))
            .Switch(() => { }, error => failure = error);

        if (failure is not null)
        {
            return Result.Failure<PreparedPassword>(failure);
        }

        return Result.Success(new PreparedPassword(
            hasher.Hash(password, parameters, parallelism),
            PasswordFloor.MeetsSingleFactorFloor(characters, singleFactor),
            PasswordAdvice.On(password, ownWords)));
    }

    /// <summary>
    /// Sets the account's password, replacing whatever it held.
    /// </summary>
    /// <param name="subject">Whose password.</param>
    /// <param name="password">The password, in UTF-8. The caller clears it.</param>
    /// <param name="ownWords">
    /// The person's own identifiers and profile fields, and the service name.
    /// </param>
    /// <param name="reachable">
    /// What the account reaches with the credentials it holds, which is what decides
    /// whether the shorter floor applies (AUTH-PASS-001a).
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The advice to show beside the password that was accepted, or the failure that
    /// refused it.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<PasswordFeedback>> SetAsync(
        SubjectId subject,
        byte[] password,
        IReadOnlyCollection<string> ownWords,
        AssuranceLevel reachable,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        PreparedPassword prepared =
            (await PrepareAsync(password, ownWords, reachable, cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<PreparedPassword>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<PasswordFeedback>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        Password? held = await passwords.FindAsync(subject, cancellationToken).ConfigureAwait(false);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if (held is null)
        {
            await passwords
                .SetAsync(Password.Set(subject, prepared.Hash, prepared.StandsAlone, now), cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            held.Change(prepared.Hash, prepared.StandsAlone, now);
            await passwords.SetAsync(held, cancellationToken).ConfigureAwait(false);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(prepared.Feedback);
    }

    /// <summary>
    /// Checks a password against the one the account holds, and carries it onto the
    /// parameters now in force where they have been raised since it was set.
    /// </summary>
    /// <param name="subject">Whose password.</param>
    /// <param name="password">The password, in UTF-8. The caller clears it.</param>
    /// <param name="ownWords">
    /// The person's own identifiers and profile fields, and the service name.
    /// </param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// What the sign-in is told beyond the password having verified, or the failure
    /// where the account holds no password or this is not it.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public async ValueTask<Result<PasswordVerification>> VerifyAsync(
        SubjectId subject,
        byte[] password,
        IReadOnlyCollection<string> ownWords,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(ownWords);

        Password? held = await passwords.FindAsync(subject, cancellationToken).ConfigureAwait(false);

        if (held is null || !Argon2idHasher.Verify(password, held.Hash))
        {
            return Result.Failure<PasswordVerification>(Error.From(ErrorCodes.FactorRejected));
        }

        Error? failure = null;

        Argon2StrengthClass parameters =
            (await ParametersAsync(cancellationToken).ConfigureAwait(false))
                .Match(value => value, error => Held<Argon2StrengthClass>(error, ref failure));

        int parallelism = (await configuration
                .ReadAsync(Settings.PasswordArgon2Parallelism, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<int>(error, ref failure));

        bool matches = (await screening
                .ContextMatchesAsync(password, ownWords, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<bool>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<PasswordVerification>(failure);
        }

        if (Raised(held.Hash, parameters, parallelism))
        {
            await work.BeginAsync(cancellationToken).ConfigureAwait(false);

            held.Rehash(hasher.Hash(password, parameters, parallelism));
            await passwords.RehashAsync(held, cancellationToken).ConfigureAwait(false);

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        // AUTH-RECOV-007a: an invalidation that left the account on this password
        // alone marked it below the floor it now has to meet, and the mark stands
        // until a new password clears it.
        return Result.Success(new PasswordVerification(matches || held.ChangeRequired));
    }

    // Argon2id costs memory by iterations; a hash computed for less work than the
    // deployment now asks is carried onto the new parameters, and one computed for
    // more is left where it stands (AUTH-PASS-007).
    private static bool Raised(PasswordHash hash, Argon2StrengthClass parameters, int parallelism) =>
        ((long)parameters.Memory * parameters.Iterations)
            > ((long)hash.Parameters.Memory * hash.Parameters.Iterations)
        || (parameters == hash.Parameters && parallelism > hash.Parallelism);

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
