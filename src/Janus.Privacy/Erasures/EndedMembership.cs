using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// A membership an organization's erasure ended: which it was, and whose.
/// </summary>
/// <param name="Membership">Which membership.</param>
/// <param name="Subject">Whose it was.</param>
/// <remarks>Implements IDN-ORG-003 and IDN-MEM-001.</remarks>
internal sealed record EndedMembership(MembershipId Membership, SubjectId Subject);
