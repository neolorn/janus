using System;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// The profile as the account directory holds it.
/// </summary>
/// <param name="DisplayName">The name shown where a human needs to know who it is.</param>
/// <param name="LegalName">The name a host that invoices or ships collects.</param>
/// <param name="DateOfBirth">The date entered at the age step, where it is retained.</param>
/// <param name="PhotoUpdatedAt">When the photo was last set, where one is shown.</param>
/// <remarks>
/// Implements REG-PROF-001 and CONV-LAYOUT-001. The image itself never crosses this
/// port: a read of the profile is a read of four small fields.
/// </remarks>
internal sealed record HeldProfile(
    DisplayName? DisplayName,
    LegalName? LegalName,
    DateOnly? DateOfBirth,
    DateTimeOffset? PhotoUpdatedAt);
