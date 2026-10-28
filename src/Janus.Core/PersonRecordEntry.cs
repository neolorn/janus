using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What a person gave staff otherwise than on a host's screen (by telephone, in person,
/// on paper), for the record staff enter.
/// </summary>
/// <param name="Organization">The organization the host keeps the data in.</param>
/// <param name="Language">One of the deployment's message languages, which every message to the record is sent in.</param>
/// <param name="Name">The name the person gave, where they gave one.</param>
/// <param name="Email">The email the person gave, where they gave one; it is held unverified.</param>
/// <param name="Phone">The phone the person gave, where they gave one; it is held unverified.</param>
/// <param name="Consents">The consent-based purposes the person gave consent for to the staff member.</param>
/// <param name="DateOfBirth">
/// The person's own answer, where the staff member asked them the age question; staff
/// never answer it for them.
/// </param>
/// <param name="Reference">
/// What the person will hold of this entry (an order number, say), 1 to 64 characters
/// after trimming, where there is one.
/// </param>
/// <remarks>Implements REC-COL-003 and REC-AGE-001.</remarks>
public sealed record PersonRecordEntry(
    OrganizationId Organization,
    string Language,
    string? Name,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Consents,
    DateOnly? DateOfBirth,
    string? Reference);
