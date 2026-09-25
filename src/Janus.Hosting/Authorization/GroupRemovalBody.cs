namespace Janus.Hosting.Authorization;

/// <summary>
/// What removing a group carries, as the request reads.
/// </summary>
/// <param name="Reason">Why, which every change to a group records.</param>
/// <remarks>Implements chapter 09 section 8a and AUTHZ-GROUP-001.</remarks>
internal sealed record GroupRemovalBody(string? Reason);
