using System.Collections.Generic;

namespace Janus.Authentication.Factors;

/// <summary>
/// The related-origins document: the origins a browser admits under one relying party
/// identifier, and nothing else.
/// </summary>
/// <param name="Origins">The configured related origins, each in its serialization.</param>
/// <remarks>Implements AUTH-FACT-012.</remarks>
internal sealed record RelatedOrigins(IReadOnlyList<string> Origins);
