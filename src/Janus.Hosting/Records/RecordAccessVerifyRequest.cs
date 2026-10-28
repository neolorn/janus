using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>POST /records/access/verify</c>.
/// </summary>
/// <param name="Code">The code entered.</param>
/// <remarks>Implements REC-ACC-001 and AUTH-FACT-004, chapter 09 section 7a.</remarks>
internal sealed record RecordAccessVerifyRequest([property: NeverLogged] string? Code);
