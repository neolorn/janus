using System;
using Janus.Core;

namespace Janus.Authentication.Bootstrap;

/// <summary>
/// The enrolment link bootstrap issued the first administrator, and the administrative
/// organization it created.
/// </summary>
/// <param name="Address">The full <c>/enrol</c> address, which is printed once and kept nowhere.</param>
/// <param name="ExpiresAt">When the link stops opening anything.</param>
/// <param name="Organization">
/// The administrative organization, whose identifier a host keeps in its own
/// configuration and the library reads from none (D-204).
/// </param>
/// <remarks>Implements OPS-BOOT-001 and AUTH-RECOV-002.</remarks>
internal sealed record BootstrapEnrolment(Uri Address, DateTimeOffset ExpiresAt, OrganizationId Organization);
