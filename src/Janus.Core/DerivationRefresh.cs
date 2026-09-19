namespace Janus.Core;

/// <summary>
/// What one refresh of a materialised derivation changed: the grants it wrote and the
/// grants it took back.
/// </summary>
/// <param name="Written">The grants the refresh wrote.</param>
/// <param name="Revoked">The grants the refresh took back.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-005 (D-161). A refresh the host runs inside the write that
/// changed the relationship carries that change and nothing else. A refresh the drift
/// check runs is expected to change nothing, so anything it changes is the drift the
/// degradation condition reports.
/// </remarks>
public sealed record DerivationRefresh(int Written, int Revoked)
{
    /// <summary>
    /// Whether the stored rows and the host's own relation said different things.
    /// </summary>
    public bool Drifted => Written > 0 || Revoked > 0;
}
