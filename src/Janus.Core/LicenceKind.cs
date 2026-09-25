using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Whether a dated authorisation is the regulatory licence or a permit.
/// </summary>
/// <remarks>Implements OPS-MAINT-001 (D-153).</remarks>
public enum LicenceKind
{
    /// <summary>The regulatory licence.</summary>
    [JsonStringEnumMemberName("licence")]
    Licence = 0,

    /// <summary>A permit.</summary>
    [JsonStringEnumMemberName("permit")]
    Permit = 1,
}
