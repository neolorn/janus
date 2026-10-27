using System;

namespace Janus.Core;

/// <summary>
/// One organization the deployment holds, as a read answers it.
/// </summary>
/// <param name="Id">Which organization.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Administrative">Whether it administers the deployment.</param>
/// <param name="DeletionRequestedAt">When its deletion was requested, or nothing where it was not.</param>
/// <remarks>Implements IDN-READ-001 (D-218).</remarks>
public sealed record OrganizationSummary(
    OrganizationId Id,
    string Name,
    bool Administrative,
    DateTimeOffset? DeletionRequestedAt);
