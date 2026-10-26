namespace Janus.Hosting.Bff;

/// <summary>
/// What stage 8 leaves on a request it passed before routing matched an endpoint, so the
/// endpoint routing then selects is judged as the stage judges it.
/// </summary>
/// <remarks>
/// Implements BFF-HOST-002 and BFF-ORDER-001 (D-209). The type is the library's own, so
/// nothing a host writes can put it on a request; only the stage does.
/// </remarks>
internal sealed class SessionRequirementDeferred
{
    /// <summary>
    /// The one mark, which carries nothing.
    /// </summary>
    public static readonly SessionRequirementDeferred Mark = new();

    private SessionRequirementDeferred()
    {
    }
}
