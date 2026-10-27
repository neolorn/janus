using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One page of an organization's current members.
/// </summary>
/// <param name="Members">The members, ordered by subject.</param>
/// <param name="Next">The last subject answered where more remain, or nothing.</param>
/// <remarks>Implements IDN-READ-002 (D-218).</remarks>
public sealed record MemberPage(IReadOnlyList<MemberSummary> Members, SubjectId? Next);
