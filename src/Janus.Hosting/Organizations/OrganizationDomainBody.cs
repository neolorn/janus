namespace Janus.Hosting.Organizations;

/// <summary>
/// What adding a domain to an organization's lock carries, as the request reads.
/// </summary>
/// <param name="Domain">The domain, in its Unicode or its ASCII form.</param>
/// <param name="Reason">Why, which every change to the lock records.</param>
/// <remarks>Implements chapter 09 section 8a, REG-DOM-001 and IDN-ORG-006.</remarks>
internal sealed record OrganizationDomainBody(string? Domain, string? Reason);
