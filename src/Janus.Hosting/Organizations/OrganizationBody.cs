namespace Janus.Hosting.Organizations;

/// <summary>
/// What creating an organization carries, as the request reads.
/// </summary>
/// <param name="Name">What it is called.</param>
/// <param name="Reason">Why, which every change to an organization records.</param>
/// <remarks>Implements chapter 09 section 8a and IDN-ORG-002.</remarks>
internal sealed record OrganizationBody(string? Name, string? Reason);
