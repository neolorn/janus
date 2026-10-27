using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One page of the organizations a caller may read.
/// </summary>
/// <param name="Organizations">The organizations, ordered by identifier.</param>
/// <param name="Next">The last identifier answered where more remain, or nothing.</param>
/// <remarks>Implements IDN-READ-001 (D-218).</remarks>
public sealed record OrganizationPage(IReadOnlyList<OrganizationSummary> Organizations, OrganizationId? Next);
