using System;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// Where a person record stands.
/// </summary>
/// <param name="Organization">The organization the host keeps it in.</param>
/// <param name="State">The state it is in.</param>
/// <param name="DeletingBy">What began its deletion window, where one runs.</param>
/// <param name="DeletingSince">When that window began.</param>
/// <param name="AttachedTo">The account it is attached to, where it is.</param>
/// <remarks>Implements REC-REC-002, REC-ACC-002 and REC-ATT-002.</remarks>
internal sealed record RecordStanding(
    OrganizationId Organization,
    PersonRecordState State,
    DeletionOrigin? DeletingBy,
    DateTimeOffset? DeletingSince,
    SubjectId? AttachedTo)
{
    /// <summary>
    /// Whether it is attached to an account.
    /// </summary>
    public bool Attached => AttachedTo is not null;
}
