using System;
using System.Collections.Generic;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Authentication.Tests.Records;

/// <summary>
/// A person record as the in-memory directory holds it: what a collection wrote or staff
/// entered of it, and the state and attachment a test sets.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Organization">The organization it belongs to.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <remarks>Implements REC-COL-002 and REC-COL-003 for the tests.</remarks>
internal sealed record HeldRecord(SubjectId Subject, OrganizationId Organization, DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Whether a collection wrote it or staff entered it.
    /// </summary>
    public PersonRecordOrigin Origin { get; init; } = PersonRecordOrigin.Collection;

    /// <summary>
    /// The state it is in.
    /// </summary>
    public PersonRecordState State { get; set; } = PersonRecordState.Active;

    /// <summary>
    /// The account it is attached to, where it is.
    /// </summary>
    public SubjectId? AttachedTo { get; set; }

    /// <summary>
    /// The language messages to it are sent in.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// The name it holds, where it holds one.
    /// </summary>
    public DisplayName? Name { get; set; }

    /// <summary>
    /// The answer to the age question, where one was given.
    /// </summary>
    public CollectedAge? Age { get; set; }

    /// <summary>
    /// Its contacts, at most one of each kind.
    /// </summary>
    public Dictionary<IdentifierKind, CollectedContact> Contacts { get; init; } = [];

    /// <summary>
    /// The references it holds, in the form their fingerprints are taken of.
    /// </summary>
    public List<string> References { get; init; } = [];

    /// <summary>
    /// Each presentation of the notice and acceptance of the terms recorded.
    /// </summary>
    public List<(string NoticeVersion, string? TermsVersion, DateTimeOffset At)> Documents { get; init; } = [];

    /// <summary>
    /// A copy that shares nothing with this one.
    /// </summary>
    /// <returns>The copy.</returns>
    public HeldRecord Copied() =>
        this with
        {
            Contacts = new Dictionary<IdentifierKind, CollectedContact>(Contacts),
            References = [.. References],
            Documents = [.. Documents],
        };
}
