using System;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// A person record a record access session reaches, with its organization's name.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization the host keeps it in.</param>
/// <param name="OrganizationName">The organization's name.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="State">The state it is in.</param>
/// <remarks>Implements REC-ACC-001 and chapter 09 section 7a.</remarks>
internal sealed record AccessibleRecord(
    SubjectId Subject,
    OrganizationId Organization,
    string OrganizationName,
    DateTimeOffset CreatedAt,
    PersonRecordState State);
