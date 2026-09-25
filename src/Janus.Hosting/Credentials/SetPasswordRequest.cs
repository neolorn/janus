using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The password being set.
/// </summary>
/// <param name="Password">The password.</param>
/// <remarks>Implements AUTH-PASS-004 and AUTH-RECOV-007a.</remarks>
internal sealed record SetPasswordRequest([property: NeverLogged] string? Password);
