using System;

namespace Janus.Hosting.Records;

/// <summary>
/// The body of <c>POST /account/records/find</c>.
/// </summary>
/// <param name="Identifier">The identifier of the account's email or phone the code goes to.</param>
/// <remarks>Implements REC-ATT-001, chapter 09 section 7a.</remarks>
internal sealed record RecordFindCodeRequest(Guid? Identifier);
