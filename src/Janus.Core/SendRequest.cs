using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One message the library has admitted and written to its outbox, as the host's
/// notification handler receives it.
/// </summary>
/// <param name="Reference">
/// The send's reference, the same at every attempt, which the host's idempotency key and
/// a later outcome report name.
/// </param>
/// <param name="Kind">What the message is for (chapter 10 section 5.25).</param>
/// <param name="Channel">The channel it goes out on.</param>
/// <param name="Subject">Whose account the message is about, where it is about one.</param>
/// <param name="Destination">The canonical address or number it goes to.</param>
/// <param name="Languages">
/// The one language it goes out in, or every language the deployment declares, in the
/// order of <c>notification.languages</c>, for the host to compose into one message.
/// </param>
/// <param name="Values">The kind's places, each by name (chapter 10 section 5.26).</param>
/// <remarks>
/// Implements INT-SMS-006, LIB-EXT-001, AUTH-ABUSE-004, IDN-ATTR-001 and
/// CONV-CONTENT-001 (D-225). It carries none of the restrictions' inputs: the handler
/// decides no restriction and counts nothing, and its text is the host's.
/// </remarks>
public sealed record SendRequest(
    SendReference Reference,
    MessageKind Kind,
    SendKind Channel,
    SubjectId? Subject,
    string Destination,
    IReadOnlyList<string> Languages,
    IReadOnlyDictionary<string, string> Values);
