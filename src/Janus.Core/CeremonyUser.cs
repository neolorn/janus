namespace Janus.Core;

/// <summary>
/// Who a WebAuthn credential is created for, as the ceremony carries them.
/// </summary>
/// <param name="Id">
/// The user handle: the account's subject identifier, base64url of its sixteen bytes.
/// </param>
/// <param name="Name">The primary email.</param>
/// <param name="DisplayName">The display name, or empty where the account shows none.</param>
/// <remarks>
/// Implements REG-PM-001 and AUTH-FACT-014. The handle is the subject identifier and
/// nothing else, which is what keeps personal data out of the one field an
/// authenticator stores and returns unprompted; the other two are shown by the
/// authenticator when it offers the credential, so they are the two identifiers a
/// person recognises themselves by and no more.
/// </remarks>
public sealed record CeremonyUser(string Id, string Name, string DisplayName);
