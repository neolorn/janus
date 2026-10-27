using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Organizations;

/// <summary>
/// Which organizations a caller lists.
/// </summary>
/// <param name="Every">Whether the caller lists every organization the deployment holds.</param>
/// <param name="Organizations">Where it does not, the organizations it lists.</param>
/// <remarks>Implements IDN-READ-001 (D-219).</remarks>
internal sealed record ReadableOrganizations(bool Every, IReadOnlyList<OrganizationId> Organizations);
