using System.Text.Json.Serialization;

namespace Janus.Core.Configuration;

/// <summary>
/// Where the deployment is hosted, as every generated record reflects it.
/// </summary>
/// <remarks>Implements chapter 10 section 4.7, INT-HOST-001, INT-HOST-002.</remarks>
public enum HostingLocation
{
    /// <summary>
    /// Inside Egypt, where no cross-border basis arises.
    /// </summary>
    [JsonStringEnumMemberName("inside")]
    Inside = 0,

    /// <summary>
    /// Outside Egypt, which makes the cross-border basis required and rests it on the
    /// regulator's permit rather than on consent.
    /// </summary>
    [JsonStringEnumMemberName("outside")]
    Outside = 1,
}
