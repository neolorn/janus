using System;

namespace Janus.Hosting.Organizations;

/// <summary>
/// What setting a membership to end at a later instant, or moving the instant, carries,
/// as the request reads.
/// </summary>
/// <param name="EndsAt">The instant the membership ends, after the present.</param>
/// <param name="Reason">Why, which the end is recorded with.</param>
/// <remarks>Implements chapter 09 section 8a and IDN-MEM-001 (D-213).</remarks>
internal sealed record MembershipEndBody(DateTimeOffset? EndsAt, string? Reason);
