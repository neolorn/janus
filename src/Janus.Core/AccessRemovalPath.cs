using System.Text.Json.Serialization;

namespace Janus.Core;

/// <summary>
/// Which way a removal of access came.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281); the names are those the record of a refusal
/// carries in <c>details.path</c>.
/// </remarks>
public enum AccessRemovalPath
{
    /// <summary>
    /// Through one of the library's own routes.
    /// </summary>
    [JsonStringEnumMemberName("administration")]
    Administration = 0,

    /// <summary>
    /// Through a call the host makes to the library's services in its own code.
    /// </summary>
    [JsonStringEnumMemberName("library")]
    Library = 1,

    /// <summary>
    /// Through the job <c>membership-ends</c>, carrying out an end set in advance.
    /// </summary>
    [JsonStringEnumMemberName("scheduledEnd")]
    ScheduledEnd = 2,
}
