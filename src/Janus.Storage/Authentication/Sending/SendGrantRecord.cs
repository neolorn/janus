namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>send_grants</c> row: the credit support added to one restriction key, kept
/// apart from the counter so that the counter holds an HMAC and times alone.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004.</remarks>
internal sealed class SendGrantRecord
{
    /// <summary>The <c>key</c> column, which is this table key.</summary>
    public byte[] Key { get; set; } = [];

    /// <summary>The <c>credit</c> column.</summary>
    public int Credit { get; set; }
}
