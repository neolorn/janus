using System;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What creating a group carries, as the request reads.
/// </summary>
/// <param name="Organization">The organization it belongs to.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Reason">Why, which every change to a group records.</param>
/// <remarks>Implements chapter 09 section 8a and AUTHZ-GROUP-001.</remarks>
internal sealed record GroupBody(Guid? Organization, string? Name, string? Reason);
