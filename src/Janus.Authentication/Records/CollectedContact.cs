using System;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// A contact a collection session staged: its kind, its canonical value, and when its
/// code verified it.
/// </summary>
/// <param name="Kind">An email or a phone.</param>
/// <param name="Value">The value, canonicalised as an account's identifier is (IDN-ACCT-004).</param>
/// <param name="VerifiedAt">When its code verified it, or nothing while it waits.</param>
/// <remarks>
/// Implements REC-COL-001.
/// </remarks>
internal sealed record CollectedContact(
    IdentifierKind Kind,
    string Value,
    DateTimeOffset? VerifiedAt)
{
    /// <summary>
    /// Whether its code verified it.
    /// </summary>
    public bool IsVerified => VerifiedAt is not null;
}
