using System;

namespace Janus.Core;

/// <summary>
/// One current member of an organization, as a read answers it.
/// </summary>
/// <param name="Subject">Whose account.</param>
/// <param name="Membership">Which membership.</param>
/// <param name="BeganAt">When the membership began.</param>
/// <param name="EndsAt">The end set in advance, or nothing where none is set.</param>
/// <param name="Invitation">
/// The invitation whose acknowledgement began it, or nothing where it began otherwise.
/// </param>
/// <remarks>Implements IDN-READ-002 (D-218).</remarks>
public sealed record MemberSummary(
    SubjectId Subject,
    MembershipId Membership,
    DateTimeOffset BeganAt,
    DateTimeOffset? EndsAt,
    InvitationId? Invitation);
