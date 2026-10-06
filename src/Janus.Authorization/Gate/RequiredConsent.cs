using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// The consent a permission bound to a consent-based purpose asks of a record's data
/// subject: for which purpose, recorded against which document, and of which kind.
/// </summary>
/// <param name="Purpose">The purpose the permission is exercised for.</param>
/// <param name="Document">The document the purpose now names.</param>
/// <param name="Kind">The kind the purpose requires.</param>
/// <remarks>
/// Implements AUTHZ-GATE-002, PRIV-SENS-002 and PRIV-CONS-007. The check and both
/// renderings of the rule are given the same one, so a list admits no record a check
/// would refuse.
/// </remarks>
internal sealed record RequiredConsent(string Purpose, string Document, ConsentKind Kind);
