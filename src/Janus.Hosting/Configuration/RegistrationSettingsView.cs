using Janus.Core.Configuration;

namespace Janus.Hosting.Configuration;

/// <summary>
/// What the registration screens ask for beyond the email.
/// </summary>
/// <param name="Phone">Whether a phone number is asked for (<c>registration.phone</c>).</param>
/// <remarks>Implements REG-IDENT-002 and D-224.</remarks>
internal sealed record RegistrationSettingsView(AttributeRequirement Phone);
