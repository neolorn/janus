using System;
using Janus.Core;

namespace Janus.Hosting.BreakGlass;

/// <summary>
/// A break-glass credential as the printable page receives it, answered once.
/// </summary>
/// <param name="Credential">The code, in its check-charactered groups of four.</param>
/// <param name="Address">The <c>/break-glass</c> address the envelope names.</param>
/// <param name="IssuedAt">When it was generated.</param>
/// <remarks>Implements OPS-BOOT-004.</remarks>
[NeverLogged]
internal sealed record GeneratedBreakGlassView(
    [property: NeverLogged] string Credential,
    string Address,
    DateTimeOffset IssuedAt);
