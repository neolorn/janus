using System;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Where an organization stands: what it is called, whether it is the administrative
/// one, and how far its deletion has gone.
/// </summary>
/// <param name="Id">The organization.</param>
/// <param name="Name">What it is called.</param>
/// <param name="IsAdministrative">Whether it is the administrative organization.</param>
/// <param name="DeletionRequestedAt">When its deletion was requested, where it was.</param>
/// <param name="ErasedAt">When it was erased, where it was.</param>
/// <remarks>Implements IDN-ORG-003 and IDN-ORG-004.</remarks>
internal sealed record OrganizationStanding(
    OrganizationId Id,
    string Name,
    bool IsAdministrative,
    DateTimeOffset? DeletionRequestedAt,
    DateTimeOffset? ErasedAt);
