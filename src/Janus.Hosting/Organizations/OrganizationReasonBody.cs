namespace Janus.Hosting.Organizations;

/// <summary>
/// What requesting an organization's deletion, or cancelling it, and ending a membership
/// carry, as the request reads.
/// </summary>
/// <param name="Reason">Why, which every change to an organization records.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-ORG-003 and IDN-MEM-001.</remarks>
internal sealed record OrganizationReasonBody(string? Reason);
