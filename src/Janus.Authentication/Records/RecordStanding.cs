using System;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// Where a person record stands, read with its row held for a change.
/// </summary>
/// <param name="Organization">The organization the host keeps it in.</param>
/// <param name="State">The state it is in.</param>
/// <param name="DeletingBy">What began its deletion window, where one runs.</param>
/// <param name="DeletingSince">When that window began.</param>
/// <param name="Attached">Whether it is attached to an account.</param>
/// <remarks>Implements REC-REC-002, REC-ACC-002.</remarks>
internal sealed record RecordStanding(
    OrganizationId Organization,
    PersonRecordState State,
    DeletionOrigin? DeletingBy,
    DateTimeOffset? DeletingSince,
    bool Attached);
