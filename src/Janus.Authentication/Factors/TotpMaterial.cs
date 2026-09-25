using System;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// What a code generator proves itself with: the shared secret, and the last time
/// step a code was accepted for.
/// </summary>
/// <param name="Secret">
/// The shared secret, held encrypted at rest under the deployment's key-encryption
/// key (AUTH-FACT-006).
/// </param>
/// <param name="ConsumedStep">
/// The last step a code was accepted for, and nothing where none has been. A code
/// from that step or earlier is refused, so an observer has no window to reuse one
/// in.
/// </param>
/// <remarks>Implements AUTH-FACT-005 and AUTH-FACT-006.</remarks>
[NeverLogged]
internal sealed record TotpMaterial(ReadOnlyMemory<byte> Secret, long? ConsumedStep);
