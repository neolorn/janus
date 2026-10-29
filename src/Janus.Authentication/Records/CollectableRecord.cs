using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// A person record a collection may reuse: active, of the organization collecting, and
/// attached to no account, read under its lock.
/// </summary>
/// <param name="Subject">The record's subject.</param>
/// <param name="Contacts">Every contact it holds, with when each was last verified at a collection.</param>
/// <param name="AgeAnswered">Whether it already holds an answer to the age question.</param>
/// <remarks>
/// Implements REC-COL-002.
/// </remarks>
internal sealed record CollectableRecord(
    SubjectId Subject,
    IReadOnlyList<CollectedContact> Contacts,
    bool AgeAnswered);
