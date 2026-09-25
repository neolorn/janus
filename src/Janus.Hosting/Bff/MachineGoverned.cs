namespace Janus.Hosting.Bff;

/// <summary>
/// What the machine profile leaves on a request it governs, so the browser profile
/// mounted after it does not govern the same request a second time.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-001. The type is the library's own, so nothing a host writes can
/// put it on a request; only mounting a route on the machine profile does.
/// </remarks>
internal sealed class MachineGoverned
{
    /// <summary>
    /// The one mark, which carries nothing.
    /// </summary>
    public static readonly MachineGoverned Mark = new();

    private MachineGoverned()
    {
    }
}
