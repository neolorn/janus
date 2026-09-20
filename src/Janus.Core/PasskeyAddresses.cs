namespace Janus.Core;

/// <summary>
/// Where a password manager is to send a person who asks to change a password or to
/// manage a passkey.
/// </summary>
/// <param name="ChangePassword">The frontend page that changes a password.</param>
/// <param name="Enrol">The frontend page that enrols a passkey.</param>
/// <param name="Manage">The frontend page that manages enrolled passkeys.</param>
/// <remarks>
/// Implements REG-PM-001 and LIB-HOST-003. The library serves the two well-known
/// documents and knows none of the addresses in them: which page does what is the
/// frontend's arrangement, so the host declares it and nothing here assumes a path.
/// </remarks>
public sealed record PasskeyAddresses(string ChangePassword, string Enrol, string Manage)
{
    /// <summary>
    /// What a deployment that has declared none of them holds, which makes the two
    /// documents answer as absent rather than point at a page that is not there.
    /// </summary>
    public static PasskeyAddresses None { get; } = new(string.Empty, string.Empty, string.Empty);
}
