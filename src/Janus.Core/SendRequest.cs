using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One message the library has admitted and written to its outbox, as the handler that
/// carries it receives it.
/// </summary>
/// <param name="Destination">Where it goes.</param>
/// <param name="Message">What it is for.</param>
/// <param name="Language">
/// The language it goes out in, or nothing for a mail owed in every language the
/// deployment declares, which is one message composed from each language's text. A text
/// message always names its language.
/// </param>
/// <param name="Reference">The correlation reference it is carried under.</param>
/// <remarks>
/// Implements LIB-EXT-001, AUTH-ABUSE-004, INT-SMS-001, IDN-ATTR-001 and
/// CONV-CONTENT-001. It carries none of the restrictions' inputs: the handler decides no
/// restriction and counts nothing.
/// </remarks>
public sealed record SendRequest(
    SendDestination Destination,
    MessageKind Message,
    string? Language,
    SendReference Reference)
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
}
