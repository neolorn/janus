using Janus.Core;

namespace Janus.Authentication.Sessions;

/// <summary>
/// What the local database made of an address: the city and country a session shows,
/// and where that city lies.
/// </summary>
/// <param name="Location">The city and country, no finer than a city.</param>
/// <param name="Coordinates">Where the city lies, absent where no city was resolved.</param>
/// <remarks>Implements INT-GEN-006, AUTH-SESS-013 and OPS-ALERT-007.</remarks>
internal sealed record ResolvedLocation(SessionLocation Location, Coordinates? Coordinates);
