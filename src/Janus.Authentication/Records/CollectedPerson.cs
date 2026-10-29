using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// What a collection writes of a person record: the record created or reused, and what
/// it gains.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization the host keeps the data in.</param>
/// <param name="Created">Whether the record is created rather than reused.</param>
/// <param name="Language">The language every message to the record is sent in.</param>
/// <param name="Name">The name given, where one was.</param>
/// <param name="Age">The age answer the record takes, where it holds none.</param>
/// <param name="Contacts">Each contact the record takes, verified now or kept unverified.</param>
/// <param name="Reference">
/// The reference the person holds, in its <c>NFKC_Casefold</c> form, kept only as a
/// keyed fingerprint.
/// </param>
/// <param name="NoticeVersion">The version of the privacy notice presented.</param>
/// <param name="TermsVersion">The version of the terms accepted, where the host has terms.</param>
/// <param name="At">The instant of the collection.</param>
/// <remarks>
/// Implements REC-COL-002, REC-REC-001 and PRIV-CONS-008a.
/// </remarks>
internal sealed record CollectedPerson(
    SubjectId Subject,
    OrganizationId Organization,
    bool Created,
    string Language,
    DisplayName? Name,
    CollectedAge? Age,
    IReadOnlyList<CollectedContact> Contacts,
    string? Reference,
    string NoticeVersion,
    string? TermsVersion,
    DateTimeOffset At);
