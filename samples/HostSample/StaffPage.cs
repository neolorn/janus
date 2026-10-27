using System;
using System.Collections.Generic;

namespace HostSample;

/// <summary>
/// One page of a workshop's staff.
/// </summary>
/// <param name="Members">The members of the organization that runs it, ordered by account.</param>
/// <param name="Next">The account the next page begins after, or nothing where none remain.</param>
public sealed record StaffPage(IReadOnlyList<StaffMember> Members, Guid? Next);
