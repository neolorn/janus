using System;
using System.Collections.Generic;

namespace Janus.Hosting.Callbacks;

/// <summary>
/// What a signed callback presents, read from it by the provider's published scheme
/// before anything parses its body.
/// </summary>
/// <param name="Presented">
/// Every signature the request carries, decoded to bytes; a provider that signs under
/// two secrets while it rotates sends one for each.
/// </param>
/// <param name="Message">
/// The exact bytes the scheme signs: the raw body, with whatever the scheme puts
/// before it, such as the instant it was signed at.
/// </param>
/// <param name="SignedAt">
/// When the provider signed it, where the scheme carries that; a scheme that does not
/// is verified without a window.
/// </param>
/// <remarks>Implements BFF-MACH-002 and INT-GEN-003.</remarks>
public sealed record CallbackSignature(
    IReadOnlyList<ReadOnlyMemory<byte>> Presented,
    ReadOnlyMemory<byte> Message,
    DateTimeOffset? SignedAt);
