using System;
using Janus.Core;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// The secrets a signed callback is verified under, as the secrets manager holds them.
/// </summary>
/// <param name="Current">The secret the provider signs with now.</param>
/// <param name="CurrentSince">When the current secret replaced the previous one.</param>
/// <param name="Previous">
/// The secret it replaced, which still verifies for 24 hours from
/// <paramref name="CurrentSince"/> and never after; nothing where there was none.
/// </param>
/// <remarks>
/// Implements BFF-MACH-002 and INT-GEN-002. They are read on every callback, so a
/// rotation takes effect with no restart and no change to code.
/// </remarks>
[NeverLogged]
public sealed record CallbackSecrets(
    ReadOnlyMemory<byte> Current,
    DateTimeOffset CurrentSince,
    ReadOnlyMemory<byte>? Previous);
