using Janus.Core;

namespace Janus.Hosting.Registration;

/// <summary>
/// A further identifier added during registration.
/// </summary>
/// <param name="Kind">Which kind it is.</param>
/// <param name="Value">The address or number, as the person entered it.</param>
/// <remarks>Implements REG-IDENT-004.</remarks>
internal sealed record AddIdentifierRequest(IdentifierKind Kind, string? Value);
