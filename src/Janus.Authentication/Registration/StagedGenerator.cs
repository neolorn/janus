using System;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// The code generator a registration session has begun and not confirmed.
/// </summary>
/// <param name="Id">The identifier the credential carries once a code confirms it.</param>
/// <param name="Label">What the person calls it.</param>
/// <param name="Secret">The shared secret, which left the library once, when it was begun.</param>
/// <remarks>
/// Implements REG-SESS-001, AUTH-FACT-006 and AUTH-FACT-007. Until a code of its secret
/// confirms it, it is no credential: it counts for nothing at the security step and
/// the terms step does not write it. One stands per session: beginning another
/// replaces it.
/// </remarks>
[NeverLogged]
internal sealed record StagedGenerator(
    AuthenticatorId Id,
    CredentialLabel Label,
    ReadOnlyMemory<byte> Secret);
