using System;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// A person record attached to an account, as the directory holds it.
/// </summary>
/// <param name="Record">The record's subject.</param>
/// <param name="Organization">The organization holding it.</param>
/// <param name="CreatedAt">When the record was created.</param>
/// <param name="AttachedAt">When it was attached.</param>
/// <param name="State">The state it is in, which is its account's.</param>
/// <remarks>Implements REC-ATT-001 and REC-ATT-002.</remarks>
internal sealed record Attachment(
    SubjectId Record,
    OrganizationId Organization,
    DateTimeOffset CreatedAt,
    DateTimeOffset AttachedAt,
    PersonRecordState State);
