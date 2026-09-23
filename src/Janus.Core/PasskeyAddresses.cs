namespace Janus.Core;

/// <summary>
/// Where a password manager is to send a person who asks to change a password or to
/// manage a passkey.
/// </summary>
/// <param name="ChangePassword">The frontend page that changes a password.</param>
/// <param name="Enrol">The frontend page that enrols a passkey.</param>
/// <param name="Manage">The frontend page that manages enrolled passkeys.</param>
/// <remarks>
/// Implements REG-PM-001, LIB-HOST-001 and LIB-HOST-003. The library serves the two
/// well-known documents and knows none of the addresses in them: which page does what
/// is the frontend's arrangement, so the host declares it and nothing here assumes a
/// path. There is no default: a deployment that registers none of these does not start.
/// </remarks>
public sealed record PasskeyAddresses(string ChangePassword, string Enrol, string Manage);
