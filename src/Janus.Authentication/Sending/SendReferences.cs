using System;
using System.Security.Cryptography;
using System.Text;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// What a correlation reference is kept as: its hash and never itself, so that a dump
/// of the ledger yields no reference a callback could be forged with.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-007, INT-GEN-003 and INT-SMS-005. A callback is hostile
/// input; what arrives is hashed and looked up, and what is looked up is a row that
/// names no destination.
/// </remarks>
internal static class SendReferences
{
    /// <summary>
    /// What a value that arrived from outside is looked up as.
    /// </summary>
    /// <param name="presented">What the callback carried.</param>
    /// <returns>Its hash.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static byte[] Of(string presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        return SHA256.HashData(Encoding.UTF8.GetBytes(presented));
    }

    /// <summary>
    /// What one drawn reference is kept as.
    /// </summary>
    /// <param name="reference">The reference.</param>
    /// <returns>Its hash.</returns>
    public static byte[] Of(SendReference reference) => Of(reference.Value);
}
