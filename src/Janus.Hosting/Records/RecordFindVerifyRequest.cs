using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>POST /account/records/find/verify</c>.
/// </summary>
/// <param name="Code">The code the account was sent.</param>
/// <remarks>Implements REC-ATT-001, chapter 09 section 7a.</remarks>
internal sealed record RecordFindVerifyRequest([property: NeverLogged] string? Code);
