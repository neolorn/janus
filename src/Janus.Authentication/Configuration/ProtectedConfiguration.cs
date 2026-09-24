using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Organizations;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// Changes protected keys from the server: the one way a key the application cannot
/// change is changed, each change written down and alerted as it is made.
/// </summary>
/// <param name="configuration">Where the value in force is read.</param>
/// <param name="settings">Where a protected key's value is written.</param>
/// <param name="organizations">Whether the organization a member names exists.</param>
/// <param name="audit">Where each change is written down.</param>
/// <param name="alerts">Where each change is raised.</param>
/// <param name="work">The one transaction the change runs in.</param>
/// <param name="time">When.</param>
/// <remarks>
/// Implements OPS-CFG-004 AC2, OPS-CFG-005, OPS-ALERT-001, D-071 and chapter 10 section
/// 4.8, as entry 319 of the decisions pending review settles them. Whoever holds the
/// server can already do worse than change a setting (D-071), so the command asks for no
/// step-up; what it cannot skip is the reason, the record and the alert. A change that
/// would leave the deployment unable to start is refused by the rule the host's start
/// applies, and nothing of it is written.
/// </remarks>
internal sealed class ProtectedConfiguration(
    IConfigurationStore configuration,
    IProtectedSettings settings,
    IOrganizationDirectory organizations,
    IConfigurationAudit audit,
    IRaisedAlerts alerts,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// The principal a change from the server is recorded under: the command cannot know
    /// which person at the server runs it.
    /// </summary>
    public static SystemPrincipal Principal { get; } =
        SystemPrincipal.ForDeployment("configure", "OPS-CFG-004", SystemOperation.Configuration);

    /// <summary>
    /// Puts the values in force together, and writes each down and raises it.
    /// </summary>
    /// <param name="values">What each protected key becomes.</param>
    /// <param name="reason">Why, which every change from the server carries.</param>
    /// <param name="cancellationToken">Abandons the change, which then leaves nothing behind.</param>
    /// <returns>Success, or the failure naming what was refused.</returns>
    /// <exception cref="ArgumentNullException">The values are absent.</exception>
    public async ValueTask<Result> ChangeAsync(
        IReadOnlyList<ProtectedValue> values,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.From(ErrorCodes.RestrictionReasonRequired));
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        foreach (ProtectedValue value in values)
        {
            if (value.Organization is OrganizationId organization
                && await organizations.FindAsync(organization, cancellationToken).ConfigureAwait(false) is null)
            {
                return Result.Failure(new Error(
                    ErrorCodes.ConfigurationValueNotAllowed,
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["key"] = JsonSerializer.SerializeToElement(value.Key.ToString()),
                        ["field"] = JsonSerializer.SerializeToElement("organization"),
                    }));
            }

            bool loosening = await value.LoosensAsync(configuration, cancellationToken).ConfigureAwait(false);
            string? before = await settings.WriteAsync(value.Key, value.Written, cancellationToken).ConfigureAwait(false);

            await audit
                .ChangedAsync(value.Key, before, value.Written, loosening, reason, Principal, now, cancellationToken)
                .ConfigureAwait(false);

            await RaiseAsync(AlertCondition.ProtectedSettingChanged, value.Key, now, cancellationToken).ConfigureAwait(false);

            // OPS-CFG-004: the governing language is protected on its own ground and its
            // change is told under its own condition as well.
            if (value.Key == Settings.LegalGoverningLanguage.Key)
            {
                await RaiseAsync(AlertCondition.GoverningLanguageChanged, value.Key, now, cancellationToken).ConfigureAwait(false);
            }
        }

        if ((await CompleteAsync(cancellationToken).ConfigureAwait(false)).Match(() => (Error?)null, failure => failure)
            is Error incomplete)
        {
            return Result.Failure(incomplete);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private async ValueTask RaiseAsync(
        AlertCondition condition,
        ConfigurationKey key,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["key"] = JsonSerializer.SerializeToElement(key.ToString()),
        };

        await alerts
            .AddAsync(new RaisedAlert(RaisedAlertId.Of(now), Alerts.Of(condition, key.ToString(), now, details)), cancellationToken)
            .ConfigureAwait(false);
    }

    // LIB-HOST-001: the rule the host's start applies, over what the settings table
    // holds once the change is written, so a change the command accepts leaves a
    // deployment that starts. The records of processing are served by every deployment
    // (entry 273).
    private async ValueTask<Result> CompleteAsync(CancellationToken cancellationToken)
    {
        IReadOnlySet<ConfigurationKey> held = await settings.HeldAsync(cancellationToken).ConfigureAwait(false);
        HashSet<ConfigurationKey> named = [.. Settings.Required.Select(setting => setting.Key).Where(held.Contains)];
        Error? failure = null;

        HostingLocation? location = named.Contains(Settings.HostingLocation.Key)
            ? (await configuration.ReadAsync(Settings.HostingLocation, cancellationToken).ConfigureAwait(false))
                .Match(value => (HostingLocation?)value, error => Held<HostingLocation?>(error, ref failure))
            : null;

        BlocklistSource source = (await configuration
                .ReadAsync(Settings.PasswordBlocklistSource, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<BlocklistSource>(error, ref failure));

        IReadOnlySet<BlocklistRejectionSource> sources = (await configuration
                .ReadAsync(Settings.PasswordBlocklistSources, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlySet<BlocklistRejectionSource>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        try
        {
            Settings.ThrowIfIncomplete(named, location, source, sources, recordsOfProcessing: true);
        }
        catch (StartupException unnamed)
        {
            return Result.Failure(unnamed.Failure ?? Error.From(ErrorCodes.StartupDeclarationMissing));
        }

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
