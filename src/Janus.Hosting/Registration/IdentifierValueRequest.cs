namespace Janus.Hosting.Registration;

/// <summary>
/// One identifier, entered or changed.
/// </summary>
/// <param name="Value">The address or number, as the person entered it.</param>
/// <remarks>Implements REG-IDENT-004 and REG-IDENT-010.</remarks>
internal sealed record IdentifierValueRequest(string? Value);
