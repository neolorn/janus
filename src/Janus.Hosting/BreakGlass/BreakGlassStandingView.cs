using System;

namespace Janus.Hosting.BreakGlass;

/// <summary>
/// Whether a break-glass credential stands, as the management application reads it,
/// with nothing of the credential.
/// </summary>
/// <param name="Standing">Whether one was generated and is neither used nor replaced.</param>
/// <param name="IssuedAt">When the standing one was generated, or nothing where none stands.</param>
/// <remarks>Implements OPS-BOOT-001 AC3.</remarks>
internal sealed record BreakGlassStandingView(bool Standing, DateTimeOffset? IssuedAt);
