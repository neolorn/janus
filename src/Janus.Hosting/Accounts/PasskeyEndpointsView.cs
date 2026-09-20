namespace Janus.Hosting.Accounts;

/// <summary>
/// The passkey endpoints document, in the two fields the specification names.
/// </summary>
/// <param name="Enroll">Where a passkey is enrolled.</param>
/// <param name="Manage">Where enrolled passkeys are managed.</param>
/// <remarks>Implements REG-PM-001.</remarks>
internal sealed record PasskeyEndpointsView(string Enroll, string Manage);
