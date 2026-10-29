using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>POST /records/collection/verify</c>.
/// </summary>
/// <param name="Kind">Which staged contact the code is for.</param>
/// <param name="Code">The code entered.</param>
/// <remarks>Implements REC-COL-001 and AUTH-FACT-004, chapter 09 section 7a.</remarks>
internal sealed record CollectionVerifyRequest(IdentifierKind? Kind, [property: NeverLogged] string? Code);
