using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Which organizations a caller lists.
/// </summary>
/// <param name="Every">Whether the caller lists every organization the deployment holds.</param>
/// <param name="Organizations">Where it does not, the organizations it lists.</param>
/// <remarks>Implements IDN-READ-001 (D-219).</remarks>
internal sealed record ListedOrganizations(bool Every, IReadOnlyList<OrganizationId> Organizations);
