namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// What is held under the person's key about where a session was used: the address it
/// came from and the city the local database resolved.
/// </summary>
/// <param name="Address">The address the request came from.</param>
/// <param name="City">The city, absent where the local database could not say.</param>
/// <param name="Country">The country, as an ISO 3166-1 alpha-2 code.</param>
/// <remarks>
/// Implements AUTH-SESS-013 and PRIV-RET-002. Neither the address nor the place is
/// readable once the person's key is destroyed.
/// </remarks>
internal sealed record SessionPlace(string Address, string? City, string? Country);
