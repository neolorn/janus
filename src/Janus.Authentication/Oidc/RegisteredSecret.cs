using System;
using Janus.Core;

namespace Janus.Authentication.Oidc;

/// <summary>
/// A registered client's secrets as the registry holds them, unwrapped for the one
/// caller that presents or judges them and clears them after.
/// </summary>
/// <param name="Current">The secret the client presents now, as its UTF-8 bytes.</param>
/// <param name="IssuedAt">When the current secret was drawn, which its rotation is read from.</param>
/// <param name="Previous">The secret the current one replaced, where one was.</param>
/// <param name="PreviousUntil">Until when the replaced secret is taken.</param>
/// <remarks>
/// Implements OPS-SEC-001 and OPS-SEC-002. Both secrets are the library's own, drawn by
/// it, held wrapped under the deployment's data key and never written anywhere
/// unwrapped.
/// </remarks>
[NeverLogged]
internal sealed record RegisteredSecret(
    byte[] Current,
    DateTimeOffset IssuedAt,
    byte[]? Previous,
    DateTimeOffset? PreviousUntil);
