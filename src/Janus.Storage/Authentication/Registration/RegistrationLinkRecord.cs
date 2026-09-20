using Janus.Core;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// The <c>registration_links</c> row: one outstanding verification link, by what the
/// token it carried fingerprints to.
/// </summary>
/// <remarks>
/// Implements REG-SESS-003. The landing route resolves a token to the session that
/// sent it without the session being named in the link, and the row goes with the
/// session it belongs to.
/// </remarks>
internal sealed class RegistrationLinkRecord
{
    /// <summary>The <c>fingerprint</c> column, which is this table's key.</summary>
    public byte[] Fingerprint { get; set; } = [];

    /// <summary>The <c>session</c> column.</summary>
    public RegistrationSessionId Session { get; set; }
}
