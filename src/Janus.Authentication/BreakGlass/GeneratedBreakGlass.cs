using System;
using Janus.Core;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// A break-glass credential as it is shown the one time it is shown.
/// </summary>
/// <param name="Credential">The code, as it is printed.</param>
/// <param name="Address">The page it is presented at, which the envelope names.</param>
/// <param name="IssuedAt">When it was generated.</param>
/// <remarks>
/// Implements OPS-BOOT-004. Nothing keeps this: the page it feeds is the only place the
/// code exists once the response is sent.
/// </remarks>
internal sealed record GeneratedBreakGlass(
    [property: NeverLogged] string Credential,
    Uri Address,
    DateTimeOffset IssuedAt);
