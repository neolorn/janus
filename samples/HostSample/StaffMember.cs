using System;

namespace HostSample;

/// <summary>
/// A member of the organization that runs a workshop, as its staff page shows them.
/// </summary>
/// <param name="Subject">The member's account.</param>
/// <param name="Since">When their membership began.</param>
/// <param name="Until">When it is set to end, or nothing.</param>
public sealed record StaffMember(Guid Subject, DateTimeOffset Since, DateTimeOffset? Until);
