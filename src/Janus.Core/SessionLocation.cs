namespace Janus.Core;

/// <summary>
/// Where a session was used, no finer than a city, resolved from a local database so
/// that no third party learns the addresses a person signs in from.
/// </summary>
/// <param name="City">The city, absent where the local database cannot say.</param>
/// <param name="Country">
/// The country as an ISO 3166-1 alpha-2 code, absent where the local database cannot
/// say.
/// </param>
/// <remarks>
/// Implements AUTH-SESS-013. Stored under the person's key and retained with the
/// session record, no longer.
/// </remarks>
public sealed record SessionLocation(string? City, string? Country);
