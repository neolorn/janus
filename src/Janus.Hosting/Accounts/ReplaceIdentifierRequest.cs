namespace Janus.Hosting.Accounts;

/// <summary>
/// The new value of an identifier being changed in one operation.
/// </summary>
/// <param name="Value">The address or number, as the person entered it.</param>
/// <remarks>Implements REG-IDENT-007.</remarks>
internal sealed record ReplaceIdentifierRequest(string? Value);
