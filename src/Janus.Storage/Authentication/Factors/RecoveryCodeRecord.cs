using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// The <c>recovery_codes</c> row.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-008 and CONV-DESIGN-003. The code is held as a password is:
/// verifiable and never recoverable.
/// </remarks>
internal sealed class RecoveryCodeRecord
{
    /// <summary>The <c>subject</c> column, half of this table key.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>ordinal</c> column, the other half: which code of the set.</summary>
    public int Ordinal { get; set; }

    /// <summary>The <c>hash</c> column.</summary>
    [NeverLogged]
    public string Hash { get; set; } = string.Empty;

    /// <summary>The <c>used_at</c> column, unset while the code is unspent.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
