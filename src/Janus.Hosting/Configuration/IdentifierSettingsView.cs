namespace Janus.Hosting.Configuration;

/// <summary>
/// How many identifiers of each kind an account may hold, and whether it may hold a
/// username.
/// </summary>
/// <param name="EmailMax">The most emails (<c>identifiers.email.max</c>).</param>
/// <param name="PhoneMax">The most phone numbers (<c>identifiers.phone.max</c>).</param>
/// <param name="UsernameEnabled">Whether a username is offered (<c>identifiers.username.enabled</c>).</param>
/// <remarks>Implements REG-IDENT-001 and D-224.</remarks>
internal sealed record IdentifierSettingsView(int EmailMax, int PhoneMax, bool UsernameEnabled);
