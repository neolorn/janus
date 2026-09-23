using System;
using System.Text;

namespace Janus.Hosting.Bff;

/// <summary>
/// The secret this application presents at the provider's token endpoint.
/// </summary>
/// <param name="material">The secret, as its UTF-8 bytes.</param>
/// <remarks>
/// Implements BFF-SESS-006 and OPS-SEC-001. It is handed to the library at startup
/// from the secrets manager and is never read from configuration, never written to the
/// database and never answered to a caller: what it exists for is one form field on
/// one back-channel request.
/// </remarks>
internal sealed class SignOnSecret(ReadOnlyMemory<byte> material)
{
    /// <summary>
    /// Whether the deployment supplied one.
    /// </summary>
    public bool Present => material.Length is not 0;

    /// <summary>
    /// The secret as the form field carries it.
    /// </summary>
    /// <returns>The value.</returns>
    public string Value() => Encoding.UTF8.GetString(material.Span);
}
