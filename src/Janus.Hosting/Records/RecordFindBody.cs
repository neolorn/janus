using System;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// What finding person records carries, as the request reads: a contact or a reference.
/// </summary>
/// <param name="Organization">The organization the finding is asked in.</param>
/// <param name="Kind">Whether the value is an email or a phone.</param>
/// <param name="Value">The email or phone, as entered.</param>
/// <param name="Reference">The reference, as entered.</param>
/// <remarks>Implements chapter 09 section 8a and REC-ADM-001.</remarks>
internal sealed record RecordFindBody(
    Guid? Organization,
    IdentifierKind? Kind,
    string? Value,
    string? Reference);
