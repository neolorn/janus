using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Authentication.Passwords;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// An account's set of recovery codes: issued together, single-use, hashed, and
/// replaced whole.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-008, AUTH-FACT-009 and AUTH-STEP-006. A set is not an
/// enrolled authenticator: it contributes nothing to what an account can reach, so no
/// rule that reads the credential list has to except it by name.
/// </remarks>
internal sealed class RecoveryCodeSet
{
    private readonly List<RecoveryCodeEntry> _codes;

    private RecoveryCodeSet(
        SubjectId subject,
        List<RecoveryCodeEntry> codes,
        DateTimeOffset at)
    {
        Subject = subject;
        _codes = codes;
        GeneratedAt = at;
    }

    /// <summary>Whose set it is.</summary>
    public SubjectId Subject { get; }

    /// <summary>When it was issued.</summary>
    public DateTimeOffset GeneratedAt { get; }

    /// <summary>When the codes were shown, and nothing where they never were.</summary>
    public DateTimeOffset? ViewedAt { get; private set; }

    /// <summary>
    /// When the codes were copied, downloaded or printed, and nothing where they were
    /// not.
    /// </summary>
    public DateTimeOffset? ExportedAt { get; private set; }

    /// <summary>When the reminder was sent, and nothing where it has not been.</summary>
    public DateTimeOffset? RemindedAt { get; private set; }

    /// <summary>The codes as they are stored, each hashed and each used at most once.</summary>
    public IReadOnlyList<RecoveryCodeEntry> Codes => _codes;

    /// <summary>How many of the set are still unused, which the account shows.</summary>
    public int Remaining => _codes.Count(code => code.UsedAt is null);

    /// <summary>
    /// A set of hashed codes, issued together.
    /// </summary>
    /// <param name="subject">Whose set it is.</param>
    /// <param name="codes">The hashes, in the order the codes were drawn.</param>
    /// <param name="at">When it was issued.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">The codes are absent.</exception>
    public static RecoveryCodeSet Of(
        SubjectId subject,
        IReadOnlyList<PasswordHash> codes,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(codes);

        return new RecoveryCodeSet(
            subject,
            [.. codes.Select(hash => new RecoveryCodeEntry(hash, UsedAt: null))],
            at);
    }

    /// <summary>
    /// The set as it already stands, which is the store's translation of its rows and
    /// no change to them.
    /// </summary>
    /// <param name="subject">Whose set it is.</param>
    /// <param name="codes">The codes, spent and unspent, in the order they were drawn.</param>
    /// <param name="generatedAt">When it was issued.</param>
    /// <param name="viewedAt">When the codes were shown.</param>
    /// <param name="exportedAt">When they were copied, downloaded or printed.</param>
    /// <param name="remindedAt">When the reminder was sent.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">The codes are absent.</exception>
    public static RecoveryCodeSet Existing(
        SubjectId subject,
        IReadOnlyList<RecoveryCodeEntry> codes,
        DateTimeOffset generatedAt,
        DateTimeOffset? viewedAt,
        DateTimeOffset? exportedAt,
        DateTimeOffset? remindedAt)
    {
        ArgumentNullException.ThrowIfNull(codes);

        return new RecoveryCodeSet(subject, [.. codes], generatedAt)
        {
            ViewedAt = viewedAt,
            ExportedAt = exportedAt,
            RemindedAt = remindedAt,
        };
    }

    /// <summary>
    /// The codes were shown to the person.
    /// </summary>
    /// <param name="at">When.</param>
    public void Viewed(DateTimeOffset at) => ViewedAt ??= at;

    /// <summary>
    /// The codes were copied, downloaded or printed.
    /// </summary>
    /// <param name="at">When.</param>
    public void Exported(DateTimeOffset at) => ExportedAt ??= at;

    /// <summary>
    /// The reminder was sent, which happens once per set.
    /// </summary>
    /// <param name="at">When.</param>
    public void Reminded(DateTimeOffset at) => RemindedAt ??= at;

    /// <summary>
    /// Whether the set is old enough to remind its owner about, and has not been
    /// reminded about already.
    /// </summary>
    /// <param name="now">The instant.</param>
    /// <param name="after">How old a set is reminded about.</param>
    /// <returns>Whether one reminder is due.</returns>
    public bool RemindsAt(DateTimeOffset now, TimeSpan after) =>
        RemindedAt is null && now >= GeneratedAt + after;

    /// <summary>
    /// Spends the code presented, where it is one of the set and has not been spent.
    /// </summary>
    /// <param name="entered">The code as it was typed.</param>
    /// <param name="at">When.</param>
    /// <returns>Whether a code of the set was spent.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public bool Spend(string entered, DateTimeOffset at)
    {
        byte[] presented = RecoveryCode.Presented(entered);

        try
        {
            for (int index = 0; index < _codes.Count; index++)
            {
                if (_codes[index].UsedAt is not null
                    || !Argon2idHasher.Verify(presented, _codes[index].Hash))
                {
                    continue;
                }

                _codes[index] = _codes[index] with { UsedAt = at };

                return true;
            }

            return false;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(presented);
        }
    }
}
