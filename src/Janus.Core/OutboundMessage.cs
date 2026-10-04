using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One message an operation undertakes to send, as the governed send judges it: where
/// it goes, what it is for, and everything the restrictions are evaluated on.
/// </summary>
/// <param name="Destination">Where it goes.</param>
/// <param name="Message">What it is for.</param>
/// <param name="Purpose">Which restrictions it answers to.</param>
/// <param name="Source">
/// The source of the request that asked for the send, or nothing where no request
/// asked for it: a background job's send or an alert (chapter 10 section 5.14).
/// </param>
/// <param name="Language">
/// The recipient's language, resolved before the send, or nothing where no language
/// of theirs is known, in which case the message goes out in every language the
/// deployment declares.
/// </param>
/// <remarks>
/// Implements AUTH-ABUSE-004, INT-SMS-001, IDN-ATTR-001, CONV-LAYOUT-002 and
/// CONV-CONTENT-001. The library states which message in which language; the words are
/// the deployment's. What the judgement reads (the purpose and the source) stays here
/// and never reaches the handler that carries the admitted message.
/// </remarks>
public sealed record OutboundMessage(
    SendDestination Destination,
    MessageKind Message,
    RestrictionPurpose Purpose,
    string? Source,
    string? Language)
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
    /// Whether this is an alert to an operator destination, which is outside every
    /// restriction and continues below the gateway floor when ordinary sends stop
    /// (OPS-ALERT-003).
    /// </summary>
    public bool IsAlert => Message is MessageKind.Alert;

    /// <summary>
    /// What a host-registered key supplier is told about the send.
    /// </summary>
    public SendContext Context => new(Purpose, Kind, Subject, Source);
}
