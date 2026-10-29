using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// A person record as staff read it: as the person reads it, with its origin and the
/// account it is attached to.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization that holds it.</param>
/// <param name="OrganizationName">What that organization is called.</param>
/// <param name="Origin">Whether it was collected from the person or entered by staff.</param>
/// <param name="State">Where it stands.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="Name">The person's name, or nothing.</param>
/// <param name="Language">The language messages to the person are written in.</param>
/// <param name="Contacts">The emails and phones it holds.</param>
/// <param name="Age">The age answer, or nothing.</param>
/// <param name="Notices">Each presentation of the privacy notice.</param>
/// <param name="Terms">Each acceptance of the terms.</param>
/// <param name="AttachedTo">The account it is attached to, or nothing.</param>
/// <remarks>Implements REC-ADM-001.</remarks>
public sealed record PersonRecordDetail(
    SubjectId Subject,
    OrganizationId Organization,
    string OrganizationName,
    PersonRecordOrigin Origin,
    PersonRecordState State,
    DateTimeOffset CreatedAt,
    string? Name,
    string Language,
    IReadOnlyList<PersonRecordContact> Contacts,
    PersonRecordAge? Age,
    IReadOnlyList<PersonRecordDocument> Notices,
    IReadOnlyList<PersonRecordDocument> Terms,
    SubjectId? AttachedTo);
