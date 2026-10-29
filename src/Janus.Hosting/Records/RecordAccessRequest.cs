using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>POST /records/access</c>.
/// </summary>
/// <param name="Kind">Whether the value is an email or a phone.</param>
/// <param name="Value">The value entered.</param>
/// <remarks>Implements REC-ACC-001, chapter 09 section 7a.</remarks>
internal sealed record RecordAccessRequest(IdentifierKind? Kind, string? Value);
