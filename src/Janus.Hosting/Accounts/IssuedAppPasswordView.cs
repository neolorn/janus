namespace Janus.Hosting.Accounts;

/// <summary>
/// A mail app password the server has just generated, answered once.
/// </summary>
/// <param name="Id">What the revocation endpoint names it by.</param>
/// <param name="Secret">The secret, shown once and never read back.</param>
/// <remarks>Implements REG-MAIL-002 and INT-MAIL-010.</remarks>
internal sealed record IssuedAppPasswordView(string Id, string Secret);
