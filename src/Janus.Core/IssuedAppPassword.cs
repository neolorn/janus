namespace Janus.Core;

/// <summary>
/// A mail app password the server has just generated: what it is called and the secret
/// itself, which leaves the library once and is kept nowhere on its side.
/// </summary>
/// <param name="Id">What the server calls it, which is what a revocation names.</param>
/// <param name="Secret">The secret, returned once and never read back.</param>
/// <remarks>Implements REG-MAIL-002 and INT-MAIL-010.</remarks>
public sealed record IssuedAppPassword(string Id, string Secret);
