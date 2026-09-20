using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// One message the library needs delivered, before any restriction has looked at it.
/// </summary>
/// <param name="Destination">Where it goes.</param>
/// <param name="Message">What it is for.</param>
/// <param name="Purpose">Which restrictions it answers to.</param>
/// <param name="Source">The address the send was asked for from.</param>
/// <param name="Language">The recipient's language, resolved before the send.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, INT-SMS-001 and CONV-CONTENT-001. The library states
/// which message in which language; the words are the deployment's.
/// </remarks>
internal sealed record SendRequest(
    SendDestination Destination,
    MessageKind Message,
    RestrictionPurpose Purpose,
    string Source,
    string Language)
{
    private static readonly IReadOnlyDictionary<string, string> Nothing =
        new Dictionary<string, string>(capacity: 0);

    /// <summary>
    /// Whose account the destination belongs to, where it belongs to one.
    /// </summary>
    public SubjectId? Subject { get; init; }

    /// <summary>
    /// What the library puts in the template's places, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = Nothing;

    /// <summary>
    /// The channel the message goes out on.
    /// </summary>
    public SendKind Kind => Destination.Kind;

    /// <summary>
    /// Whether this is a security notice to an address an account already holds,
    /// which is outside the destination restrictions so that an attacker who drains a
    /// bucket cannot silence the notice that says so (AUTH-ABUSE-004).
    /// </summary>
    public bool IsNoticeToHolder =>
        MessageChannels.Notices.Contains(Message) && Subject is not null;

    /// <summary>
    /// Whether this is an alert to an operator destination, which continues below the
    /// gateway floor when ordinary sends stop (OPS-ALERT-003).
    /// </summary>
    public bool IsAlert => Message is MessageKind.Alert;

    /// <summary>
    /// What a host-registered key supplier is told about the send.
    /// </summary>
    public SendContext Context => new(Purpose, Kind, Subject, Source);
}
