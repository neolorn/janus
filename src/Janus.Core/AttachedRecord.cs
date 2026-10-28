using System;

namespace Janus.Core;

/// <summary>
/// A person record attached to the account, as the account lists it.
/// </summary>
/// <param name="Subject">The record's subject, which it keeps after its attachment.</param>
/// <param name="Organization">The organization holding it.</param>
/// <param name="OrganizationName">That organization's name.</param>
/// <param name="CreatedAt">When the record was created.</param>
/// <param name="AttachedAt">When it was attached.</param>
/// <remarks>Implements REC-ATT-001 and chapter 09 section 7a.</remarks>
public sealed record AttachedRecord(
    SubjectId Subject,
    OrganizationId Organization,
    string OrganizationName,
    DateTimeOffset CreatedAt,
    DateTimeOffset AttachedAt);
