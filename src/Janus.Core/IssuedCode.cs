using System;

namespace Janus.Core;

/// <summary>
/// The one-time authorization code a live session was issued, which is exchanged
/// back-channel and never travels anywhere else.
/// </summary>
/// <param name="Code">The code.</param>
/// <param name="ExpiresAt">When it stops being exchangeable.</param>
/// <remarks>
/// Implements AUTH-SESS-012. The code is single-use, bound to the client it was
/// issued to and to the verifier of the challenge it was issued against.
/// </remarks>
public sealed record IssuedCode(string Code, DateTimeOffset ExpiresAt);
