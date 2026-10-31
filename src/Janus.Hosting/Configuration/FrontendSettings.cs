using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Policies;
using Janus.Authentication.Registration;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;

namespace Janus.Hosting.Configuration;

/// <summary>
/// Reads the settings <c>GET /settings</c> answers. It is not an operation: it reads the
/// deployment's settings and the browser's own records, and no record of a person
/// (07, D-224).
/// </summary>
/// <param name="configuration">Where the keys are read.</param>
/// <param name="providers">The social providers the deployment declares.</param>
/// <param name="processing">The purposes the deployment declares.</param>
/// <param name="policies">Resolves the policy in force for an account.</param>
/// <param name="registrations">Resolves the policy that governs a registration.</param>
/// <remarks>
/// Implements REG-IDENT-001, REG-IDENT-002, REG-PROF-001, AUTH-PASS-001, AUTH-PRIN-002,
/// PRIV-BASIS-003, REG-INV-001 and D-224.
/// </remarks>
internal sealed class FrontendSettings(
    IConfigurationStore configuration,
    IEnumerable<SocialProvider> providers,
    DeclaredProcessing processing,
    PolicyResolution policies,
    RegistrationService registrations)
{
    /// <summary>
    /// Reads the settings for the browser a request comes from.
    /// </summary>
    /// <param name="browser">The browser's session and first contact, where it carries them.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ArgumentNullException">The browser is absent.</exception>
    /// <exception cref="InvalidOperationException">A key the catalogue declares is unreadable.</exception>
    public async ValueTask<FrontendSettingsView> ReadAsync(
        RequestSession browser,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(browser);

        return new FrontendSettingsView(
            new RegistrationSettingsView(
                await ValueAsync(Settings.RegistrationPhone, cancellationToken).ConfigureAwait(false)),
            new IdentifierSettingsView(
                await ValueAsync(Settings.IdentifiersEmailMax, cancellationToken).ConfigureAwait(false),
                await ValueAsync(Settings.IdentifiersPhoneMax, cancellationToken).ConfigureAwait(false),
                await ValueAsync(Settings.IdentifiersUsernameEnabled, cancellationToken).ConfigureAwait(false)),
            new ProfileSettingsView(
                await ValueAsync(Settings.ProfileLegalName, cancellationToken).ConfigureAwait(false),
                await ValueAsync(Settings.ProfileDateOfBirth, cancellationToken).ConfigureAwait(false)),
            new PasswordSettingsView(
                await ValueAsync(Settings.PasswordFloorSingleFactor, cancellationToken).ConfigureAwait(false),
                await ValueAsync(Settings.PasswordFloorWithMfa, cancellationToken).ConfigureAwait(false),
                await ValueAsync(Settings.PasswordMaximum, cancellationToken).ConfigureAwait(false)),
            [.. providers.Select(provider => provider.Provider).Order()],
            [.. processing.Purposes.Where(PurposeSettingView.Decided).Select(PurposeSettingView.Of)],
            PolicySettingsView.Of(await InForceAsync(browser, cancellationToken).ConfigureAwait(false)));
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The fault names what could not be read: a key, or the policy that governs.
    private static TValue Readable<TValue>(string unreadable, TValue value, Error? failure) =>
        failure is null
            ? value
            : throw new InvalidOperationException(unreadable + " is unreadable: " + failure.Code.ToString() + ".");

    // AUTH-PRIN-002, REG-INV-001: the account's policy where the request carries its
    // session, the policy of the organization whose invitation a registration carries,
    // and the system policy where it carries neither. The fault names what was read.
    private async ValueTask<Policy> InForceAsync(RequestSession browser, CancellationToken cancellationToken)
    {
        (Result<Policy> Policy, bool Invited)? governing =
            browser.Live is null && browser.FirstContact?.Registration is RegistrationSessionId registering
                ? await registrations.GoverningAsync(registering, cancellationToken).ConfigureAwait(false)
                : null;

        Result<Policy> resolved = browser.Live is { } live
            ? await policies.ForAsync(live.Subject, cancellationToken).ConfigureAwait(false)
            : governing?.Policy
                ?? await configuration.ReadAsync(Settings.PolicyDefault, cancellationToken).ConfigureAwait(false);

        string source = browser.Live is not null
            ? "The policy of the account"
            : governing is { Invited: true }
                ? "The policy of the invitation's organization"
                : "The key " + Settings.PolicyDefault.Key.ToString();

        Error? failure = null;
        Policy policy = resolved.Match(value => value, error => Held<Policy>(error, ref failure));

        return Readable(source, policy, failure);
    }

    private async ValueTask<TValue> ValueAsync<TValue>(Setting<TValue> setting, CancellationToken cancellationToken)
    {
        Error? failure = null;

        TValue value = (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Held<TValue>(error, ref failure));

        return Readable("The key " + setting.Key.ToString(), value, failure);
    }
}
