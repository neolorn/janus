namespace Janus.Hosting.Recovery;

/// <summary>
/// The username a password manager pairs the reset form's new password with.
/// </summary>
/// <param name="Username">The account's primary email.</param>
/// <remarks>Implements FE-PM-002 and D-224.</remarks>
internal sealed record UsernameView(string Username);
