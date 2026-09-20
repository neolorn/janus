using System.Text.Json.Serialization;

namespace Janus.Authentication.Accounts;

/// <summary>
/// What a lifecycle link undoes, which is what a presented token is allowed to do
/// and nothing else.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-013, IDN-LIFE-014 and CONV-ENUM-001. A suspended or deleting
/// account cannot sign in, so each of the two states issues the one link that ends
/// it, and neither link ends the other state.
/// </remarks>
internal enum LifecycleLinkKind
{
    /// <summary>The link in the deactivation notice, which stands the account up.</summary>
    [JsonStringEnumMemberName("reactivation")]
    Reactivation = 0,

    /// <summary>The link in the deletion notice, which ends the grace window.</summary>
    [JsonStringEnumMemberName("deletion-cancellation")]
    DeletionCancellation = 1,
}
