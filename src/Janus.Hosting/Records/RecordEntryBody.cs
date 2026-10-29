using System;
using System.Collections.Generic;

namespace Janus.Hosting.Records;

/// <summary>
/// What entering a person record carries, as the request reads.
/// </summary>
/// <param name="Organization">The organization the host keeps the data in.</param>
/// <param name="Language">The language every message to the record is sent in.</param>
/// <param name="Name">The name the person gave, where they gave one.</param>
/// <param name="Email">The email the person gave, where they gave one.</param>
/// <param name="Phone">The phone the person gave, where they gave one.</param>
/// <param name="Consents">The consent-based purposes the person gave consent for, none where absent.</param>
/// <param name="DateOfBirth">The person's own answer to the age question, where they gave one.</param>
/// <param name="Reference">What the person will hold of this entry, where there is one.</param>
/// <remarks>Implements chapter 09 section 8a and REC-COL-003.</remarks>
internal sealed record RecordEntryBody(
    Guid? Organization,
    string? Language,
    string? Name,
    string? Email,
    string? Phone,
    IReadOnlyList<string>? Consents,
    DateOnly? DateOfBirth,
    string? Reference);
