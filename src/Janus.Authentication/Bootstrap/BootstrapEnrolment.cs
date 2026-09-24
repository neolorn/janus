using System;

namespace Janus.Authentication.Bootstrap;

/// <summary>
/// The enrolment link bootstrap issued the first administrator.
/// </summary>
/// <param name="Address">The full <c>/enrol</c> address, which is printed once and kept nowhere.</param>
/// <param name="ExpiresAt">When the link stops opening anything.</param>
/// <remarks>Implements OPS-BOOT-001 and AUTH-RECOV-002.</remarks>
internal sealed record BootstrapEnrolment(Uri Address, DateTimeOffset ExpiresAt);
