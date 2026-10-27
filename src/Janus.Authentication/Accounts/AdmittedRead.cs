using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// Where a read of another person's account was admitted.
/// </summary>
/// <param name="Organization">The organization the permission was held in.</param>
/// <param name="Administrative">Whether it is the administrative organization.</param>
/// <remarks>Implements IDN-READ-004.</remarks>
internal sealed record AdmittedRead(OrganizationId Organization, bool Administrative);
