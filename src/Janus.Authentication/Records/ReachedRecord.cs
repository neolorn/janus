using System;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// A person record a record access session reaches, as its state lists it.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization the host keeps it in.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="State">The state it is in.</param>
/// <remarks>Implements REC-ACC-001.</remarks>
internal sealed record ReachedRecord(
    SubjectId Subject,
    OrganizationId Organization,
    DateTimeOffset CreatedAt,
    PersonRecordState State);
