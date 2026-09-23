using System;

namespace Janus.Hosting.Authorization;

/// <summary>
/// Whose credentials a request was made under, and whose identity it acted under.
/// </summary>
/// <param name="Acting">Whose credentials, or nothing where a system principal asked.</param>
/// <param name="Effective">Whose identity, or nothing where a system principal asked.</param>
/// <remarks>Implements AUTHZ-GATE-004 and AUTHZ-IMP-001.</remarks>
internal sealed record ExplainedPrincipalView(Guid? Acting, Guid? Effective);
