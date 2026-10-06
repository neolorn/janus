using System.Text.Json;

namespace Janus.Core;

/// <summary>
/// The names of the secrets the key ring holds, and the refusal of one that is not
/// available.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007, CONV-CODE-007 and D-190. The service that fills the key
/// ring refuses a secret under the name the ring refuses it under, and names no seam's
/// implementation to do so: the names and the refusal are declared here, beside the
/// contract the ring is filled through (<see cref="IKeyRingFilling"/>), and the ring
/// reads them from here as well.
/// </remarks>
internal static class KeyRingSecrets
{
    /// <summary>
    /// The name the mail server's key is refused under.
    /// </summary>
    internal const string MailServerSecret = "mailServerSecret";

    /// <summary>
    /// The name the key-encryption key is refused under.
    /// </summary>
    internal const string KeyEncryptionKeysName = "keyEncryptionKeys";

    /// <summary>
    /// The name the fingerprint key is refused under.
    /// </summary>
    internal const string FingerprintKeysName = "fingerprintKeys";

    /// <summary>
    /// The name the maintenance credential is refused under.
    /// </summary>
    internal const string MaintenanceCredentialName = "maintenanceCredential";

    /// <summary>
    /// The name a social provider's credential is refused under.
    /// </summary>
    /// <param name="provider">The provider's name.</param>
    /// <returns>The name, <c>socialProvider.&lt;provider&gt;</c>.</returns>
    internal static string Named(string provider) => "socialProvider." + provider;

    /// <summary>
    /// The refusal of a secret the ring cannot answer.
    /// </summary>
    /// <param name="key">The secret, as <c>details.key</c> names it.</param>
    /// <returns>The failure.</returns>
    internal static Error Unavailable(string key) =>
        Error.From(ErrorCodes.StartupSecretUnavailable, "key", JsonSerializer.SerializeToElement(key));
}
