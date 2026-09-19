using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A fact in the host's own data: who reviews what is in one of its workspaces. The
/// library holds no row of this and reads none; a derivation follows from it, and the
/// host passes these rows to the filter.
/// </summary>
public sealed class HostReviewer
{
    /// <summary>
    /// The workspace, under the identifier the host registered it with.
    /// </summary>
    public required string WorkspaceId { get; init; }

    /// <summary>
    /// The person reviewing what is in it.
    /// </summary>
    public required SubjectId Reviewer { get; init; }
}
