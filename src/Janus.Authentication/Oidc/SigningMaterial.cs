using System;

namespace Janus.Authentication.Oidc;

/// <summary>
/// The key a token is being signed with right now, with the private material the
/// caller signs with and clears.
/// </summary>
/// <param name="KeyId">What the token's header names.</param>
/// <param name="Algorithm">What it signs with.</param>
/// <param name="PrivateKey">The private key in unencrypted private key information format.</param>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The material is handed to the one caller
/// that signs and is never written anywhere unwrapped.
/// </remarks>
internal sealed record SigningMaterial(string KeyId, string Algorithm, byte[] PrivateKey);
