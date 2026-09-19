using System;
using Janus.Core;

namespace Janus.Authentication.Passwords;

/// <summary>
/// The one password an account holds: the hash, whether that password stands on its
/// own, and when it was set.
/// </summary>
/// <remarks>
/// Implements AUTH-PASS-001a, AUTH-PASS-003 and AUTH-PASS-007. There is no expiry
/// field: rotation is required on evidence of compromise and never on a schedule.
/// </remarks>
internal sealed class Password
{
    private Password(
        SubjectId subject,
        PasswordHash hash,
        bool meetsSingleFactorFloor,
        DateTimeOffset setAt)
    {
        Subject = subject;
        Hash = hash;
        MeetsSingleFactorFloor = meetsSingleFactorFloor;
        SetAt = setAt;
    }

    /// <summary>
    /// Whose password it is.
    /// </summary>
    public SubjectId Subject { get; }

    /// <summary>
    /// The hash, with the parameters it was computed at.
    /// </summary>
    public PasswordHash Hash { get; private set; }

    /// <summary>
    /// Whether the password meets the floor that applies where it could complete a
    /// sign-in by itself. Recorded because it cannot be recomputed from the hash.
    /// </summary>
    public bool MeetsSingleFactorFloor { get; private set; }

    /// <summary>
    /// When the password was last set or changed.
    /// </summary>
    public DateTimeOffset SetAt { get; private set; }

    /// <summary>
    /// A password just set.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="meetsSingleFactorFloor">Whether it stands on its own.</param>
    /// <param name="at">When it was set.</param>
    /// <returns>The password.</returns>
    /// <exception cref="ArgumentNullException">The hash is absent.</exception>
    public static Password Set(
        SubjectId subject,
        PasswordHash hash,
        bool meetsSingleFactorFloor,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return new Password(subject, hash, meetsSingleFactorFloor, at);
    }

    /// <summary>
    /// The password as it already stands, which is the store's translation of a row
    /// and no change.
    /// </summary>
    /// <param name="subject">Whose it is.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="meetsSingleFactorFloor">Whether it stands on its own.</param>
    /// <param name="setAt">When it was set.</param>
    /// <returns>The password.</returns>
    /// <exception cref="ArgumentNullException">The hash is absent.</exception>
    public static Password Existing(
        SubjectId subject,
        PasswordHash hash,
        bool meetsSingleFactorFloor,
        DateTimeOffset setAt)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return new Password(subject, hash, meetsSingleFactorFloor, setAt);
    }

    /// <summary>
    /// The person set a different password.
    /// </summary>
    /// <param name="hash">The new hash.</param>
    /// <param name="meetsSingleFactorFloor">Whether the new password stands on its own.</param>
    /// <param name="at">When it was changed.</param>
    /// <exception cref="ArgumentNullException">The hash is absent.</exception>
    public void Change(PasswordHash hash, bool meetsSingleFactorFloor, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(hash);

        Hash = hash;
        MeetsSingleFactorFloor = meetsSingleFactorFloor;
        SetAt = at;
    }

    /// <summary>
    /// The same password, hashed again at raised parameters after it verified. The
    /// password did not change, so neither the floor flag nor the instant it was set
    /// moves.
    /// </summary>
    /// <param name="hash">The hash at the parameters now in force.</param>
    /// <exception cref="ArgumentNullException">The hash is absent.</exception>
    public void Rehash(PasswordHash hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        Hash = hash;
    }
}
