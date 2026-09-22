using System;
using Janus.Core;

namespace Janus.Privacy.Erasures;

/// <summary>
/// An organization standing in its deletion grace window: which it is, and when the
/// window began running.
/// </summary>
/// <param name="Organization">Which organization.</param>
/// <param name="Since">When the window began, which is when access stopped.</param>
/// <remarks>Implements IDN-ORG-003.</remarks>
internal sealed record PendingOrganizationDeletion(
    OrganizationId Organization,
    DateTimeOffset Since);
