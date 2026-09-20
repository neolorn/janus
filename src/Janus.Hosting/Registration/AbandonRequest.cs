namespace Janus.Hosting.Registration;

/// <summary>
/// Ending a registration, from the session itself or from the link a message carried.
/// </summary>
/// <param name="LinkToken">
/// The token of the link, where the ending control of another browser is pressing it.
/// </param>
/// <remarks>Implements REG-SESS-001 and REG-SESS-003.</remarks>
internal sealed record AbandonRequest(string? LinkToken);
