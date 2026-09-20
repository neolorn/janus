using System;

namespace Janus.Core;

/// <summary>
/// One key as the published key set carries it: enough to validate a token offline,
/// and nothing that could sign one.
/// </summary>
/// <param name="KeyId">What a token's header names.</param>
/// <param name="Algorithm">What it signs with.</param>
/// <param name="PublicKey">The public key in subject public key information format.</param>
/// <param name="RetiresAt">
/// When it leaves the set, and nothing for the key that is signing now.
/// </param>
/// <remarks>
/// Implements AUTH-KEY-001. The previous key stays published for the overlap so that
/// tokens it signed validate until the last of them has expired.
/// </remarks>
public sealed record PublishedSigningKey(
    string KeyId,
    string Algorithm,
    ReadOnlyMemory<byte> PublicKey,
    DateTimeOffset? RetiresAt);
