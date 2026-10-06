namespace Janus.Hosting.Credentials;

/// <summary>
/// The body a refused Security Event Token is answered with on the route of a provider
/// that delivers as RFC 8935 does (section 2.3).
/// </summary>
/// <param name="Err">The code RFC 8935 section 2.4 gives the failure.</param>
/// <param name="Description">
/// What the standard leaves to the receiver, which carries the same code again and
/// never a sentence.
/// </param>
/// <remarks>Implements LIB-API-003, IDN-LIFE-012a AC8 and chapter 09 section 10.</remarks>
internal sealed record SecurityEventError(string Err, string Description);
