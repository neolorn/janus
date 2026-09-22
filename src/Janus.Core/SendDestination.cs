namespace Janus.Core;

/// <summary>
/// Where one message goes: an address or a number, in the canonical form the
/// restriction key is taken over.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-004 and chapter 10 section 5.14. The canonical form is what
/// the destination key hashes, so two spellings of one address share a bucket.
/// </remarks>
public sealed record SendDestination
{
    private SendDestination(SendKind kind, EmailAddress mail, PhoneNumber phone)
    {
        Kind = kind;
        Mail = mail;
        Phone = phone;
    }

    /// <summary>
    /// The channel the destination is reached on.
    /// </summary>
    public SendKind Kind { get; }

    /// <summary>
    /// The address, where the channel is mail.
    /// </summary>
    public EmailAddress Mail { get; }

    /// <summary>
    /// The number, where the channel is SMS.
    /// </summary>
    public PhoneNumber Phone { get; }

    /// <summary>
    /// The canonical text of whichever of the two the destination is.
    /// </summary>
    public string Canonical => Kind is SendKind.Email ? Mail.Value : Phone.Value;

    /// <summary>
    /// A destination reached by mail.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The destination.</returns>
    public static SendDestination Of(EmailAddress address) =>
        new(SendKind.Email, address, default);

    /// <summary>
    /// A destination reached by SMS.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The destination.</returns>
    public static SendDestination Of(PhoneNumber number) =>
        new(SendKind.Sms, default, number);
}
