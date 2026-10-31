namespace Janus.Hosting.Configuration;

/// <summary>
/// The lengths a password field is held to.
/// </summary>
/// <param name="FloorSingleFactor">
/// The shortest password on an account it is the only factor of
/// (<c>password.floor.singlefactor</c>).
/// </param>
/// <param name="FloorWithMfa">
/// The shortest password on an account with a second factor (<c>password.floor.withmfa</c>).
/// </param>
/// <param name="Maximum">The longest password (<c>password.maximum</c>).</param>
/// <remarks>Implements AUTH-PASS-001 and D-224.</remarks>
internal sealed record PasswordSettingsView(int FloorSingleFactor, int FloorWithMfa, int Maximum);
