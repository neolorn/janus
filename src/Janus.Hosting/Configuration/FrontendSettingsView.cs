using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Configuration;

/// <summary>
/// The deployment's settings a frontend renders its screens from, which
/// <c>GET /settings</c> answers.
/// </summary>
/// <param name="Registration">What the registration screens ask for.</param>
/// <param name="Identifiers">How many identifiers an account may hold.</param>
/// <param name="Profile">Which profile attributes are asked for.</param>
/// <param name="Password">The lengths a password is held to.</param>
/// <param name="Providers">The social providers the deployment declares.</param>
/// <param name="Purposes">The purposes a person consents to or objects to.</param>
/// <param name="Policy">The part of the policy in force the screens need.</param>
/// <remarks>
/// Implements REG-IDENT-001, REG-IDENT-002, REG-PROF-001, AUTH-PASS-001, AUTH-PRIN-002,
/// PRIV-BASIS-003, REG-INV-001 and D-224.
/// </remarks>
internal sealed record FrontendSettingsView(
    RegistrationSettingsView Registration,
    IdentifierSettingsView Identifiers,
    ProfileSettingsView Profile,
    PasswordSettingsView Password,
    IReadOnlyList<Factor> Providers,
    IReadOnlyList<PurposeSettingView> Purposes,
    PolicySettingsView Policy);
