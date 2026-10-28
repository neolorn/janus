using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// What staff entry writes of a person record: a record of its own, of origin
/// <c>staff</c>, whose contacts are unverified.
/// </summary>
/// <param name="Subject">The record's subject, issued for it.</param>
/// <param name="Organization">The organization the host keeps the data in.</param>
/// <param name="Language">The language every message to the record is sent in.</param>
/// <param name="Name">The name given, where one was.</param>
/// <param name="Age">The person's own answer to the age question, where they gave one.</param>
/// <param name="Contacts">Each contact given, unverified.</param>
/// <param name="Reference">
/// The reference the person holds, in its <c>NFKC_Casefold</c> form, kept only as a
/// keyed fingerprint.
/// </param>
/// <param name="At">The instant of the entry.</param>
/// <remarks>
/// Implements REC-COL-003 and REC-AGE-001.
/// </remarks>
internal sealed record EnteredPerson(
    SubjectId Subject,
    OrganizationId Organization,
    string Language,
    DisplayName? Name,
    CollectedAge? Age,
    IReadOnlyList<CollectedContact> Contacts,
    string? Reference,
    DateTimeOffset At);
