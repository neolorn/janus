namespace Janus.Hosting.Accounts;

/// <summary>
/// A request carried by the link a message sent, for the paths an account has no
/// session to reach on its own.
/// </summary>
/// <param name="LinkToken">The token of the link.</param>
/// <remarks>Implements REG-IDENT-006 and REG-SESS-003.</remarks>
internal sealed record LinkTokenRequest(string? LinkToken);
