using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// A person record as the person reads it.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization the host keeps it in.</param>
/// <param name="Origin">Whether the person gave it or staff entered it.</param>
/// <param name="State">The state it is in.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="Name">The name it holds, where it holds one.</param>
/// <param name="Language">The language messages to it are sent in.</param>
/// <param name="Contacts">Each contact, with when it was last verified at a collection.</param>
/// <param name="Age">The answer to the age question, where one was given.</param>
/// <param name="Notices">Each notice version its collections presented.</param>
/// <param name="Terms">Each terms version its collections recorded as accepted.</param>
/// <remarks>Implements REC-ACC-002, chapter 09 section 7a.</remarks>
internal sealed record ReadRecord(
    SubjectId Subject,
    OrganizationId Organization,
    PersonRecordOrigin Origin,
    PersonRecordState State,
    DateTimeOffset CreatedAt,
    string? Name,
    string Language,
    IReadOnlyList<CollectedContact> Contacts,
    RecordAge? Age,
    IReadOnlyList<RecordDocument> Notices,
    IReadOnlyList<RecordDocument> Terms);
