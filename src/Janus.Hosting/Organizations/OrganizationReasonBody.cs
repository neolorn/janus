namespace Janus.Hosting.Organizations;

/// <summary>
/// What requesting an organization's deletion, or cancelling it, carries, as the
/// request reads.
/// </summary>
/// <param name="Reason">Why, which every change to an organization records.</param>
/// <remarks>Implements chapter 09 section 8a and IDN-ORG-003.</remarks>
internal sealed record OrganizationReasonBody(string? Reason);
