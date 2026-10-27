using System;
using Janus.Core;

namespace Janus.Identity.Organizations;

/// <summary>
/// The end an administrator set a membership to reach at a later instant, as the
/// membership keeps it.
/// </summary>
/// <param name="At">The instant the membership ends.</param>
/// <param name="SetBy">The administrator who set it, whom the end is recorded as.</param>
/// <param name="Reason">Why, which the end is recorded with.</param>
/// <remarks>Implements IDN-MEM-001 (D-213).</remarks>
internal sealed record MembershipEndSet(DateTimeOffset At, SubjectId SetBy, string Reason);
