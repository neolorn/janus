using Janus.Core;

namespace Janus.Authentication.Credentials;

/// <summary>
/// One security event a social provider sent, as read from a token its published keys
/// verified.
/// </summary>
/// <param name="Provider">Which social provider sent it.</param>
/// <param name="EventId">The provider's identifier of the event, its <c>jti</c>.</param>
/// <param name="Type">The event's type, as the provider spells it.</param>
/// <param name="Subject">
/// The provider's subject identifier of the identity it concerns, where it names an
/// identity: a revoked token or a test of the stream names none.
/// </param>
/// <param name="Address">
/// The address the event concerns, where it names one: an address the provider
/// forwards to or stopped forwarding to.
/// </param>
/// <remarks>Implements IDN-LIFE-012a.</remarks>
internal sealed record ProviderNotice(
    Factor Provider,
    string EventId,
    string Type,
    string? Subject,
    string? Address);
