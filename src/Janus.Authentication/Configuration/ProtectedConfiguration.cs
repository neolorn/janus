using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Authentication.Oidc;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Configuration;

/// <summary>
/// Changes protected keys from the server: the one way a key the application cannot
/// change is changed, each change written down and alerted as it is made.
/// </summary>
/// <param name="configuration">Where the value in force is read.</param>
/// <param name="settings">Where a protected key's value is written.</param>
/// <param name="audit">Where each change is written down.</param>
/// <param name="alerts">Where each change is raised, with its event, in the change's transaction.</param>
/// <param name="redirects">The start's check of the default client a browser lands on.</param>
/// <param name="work">The one transaction the change runs in.</param>
/// <param name="time">When.</param>
/// <remarks>
/// Implements OPS-CFG-004 AC2, OPS-CFG-005, OPS-ALERT-001, D-071 and chapter 10 section
/// 4.8, as entry 319 of the decisions pending review settles them. Whoever holds the
/// server can already do worse than change a setting (D-071), so the command asks for no
/// step-up; what it cannot skip is the reason, the record and the alert. A change that
/// would leave the deployment unable to start is refused by the rules the host's start
/// applies, run over the written values before the commit (D-166), and nothing of it is
/// written.
/// </remarks>
internal sealed class ProtectedConfiguration(
    IConfigurationStore configuration,
    IProtectedSettings settings,
    IConfigurationAudit audit,
    IAlertChannels alerts,
    RedirectValidation redirects,
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
            return Result.Failure(Error.From(ErrorCodes.ConfigurationChangeReasonRequired));
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        foreach (ProtectedValue value in values)
        {
            bool loosening = await value.LoosensAsync(configuration, cancellationToken).ConfigureAwait(false);
            string? before = await value.BeforeAsync(configuration, cancellationToken).ConfigureAwait(false);

            _ = await settings.WriteAsync(value.Key, value.Written, cancellationToken).ConfigureAwait(false);

            await audit
                .ChangedAsync(value.Key, before, value.Written, loosening, reason, Principal, now, cancellationToken)
                .ConfigureAwait(false);

            if (await RaiseAsync(AlertCondition.ProtectedSettingChanged, value.Key, now, cancellationToken).ConfigureAwait(false)
                is Error unannounced)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure(unannounced);
            }

            // OPS-CFG-004: the governing language is protected on its own ground and its
            // change is told under its own condition as well.
            if (value.Key == Settings.LegalGoverningLanguage.Key
                && await RaiseAsync(AlertCondition.GoverningLanguageChanged, value.Key, now, cancellationToken).ConfigureAwait(false)
                    is Error unannouncedLanguage)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure(unannouncedLanguage);
            }
        }

        if ((await CompleteAsync(cancellationToken).ConfigureAwait(false)).Match(() => (Error?)null, failure => failure)
            is Error incomplete)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(incomplete);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // OPS-ALERT-001 and D-166 (308): the alert is raised through the channels, so its
    // event is written with the row in the change's transaction, and a change whose
    // alert cannot be raised is not made.
    private async ValueTask<Error?> RaiseAsync(
        AlertCondition condition,
        ConfigurationKey key,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var details = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["key"] = JsonSerializer.SerializeToElement(key.ToString()),
        };

        return (await alerts
                .RaiseAsync(Alerts.Of(condition, key.ToString(), now, details), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
    }

    // LIB-HOST-001 and D-166 (319): the rules the host's start applies, over what the
    // settings table holds once the change is written, so a change the command accepts
    // leaves a deployment that starts: the keys it has to name, the relying party
    // (AUTH-FACT-010), the endpoints (INT-GEN-001), the signing algorithm (AUTH-KEY-001)
    // and the default client (API-REDIR-002), each refused with the code the start
    // gives. The records of processing are served by every deployment (entry 273).
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

        try
        {
            _ = await RelyingParty.ForAsync(configuration, cancellationToken).ConfigureAwait(false);
        }
        catch (StartupException unsettled)
        {
            return Result.Failure(unsettled.Failure ?? Error.From(ErrorCodes.StartupRelyingPartyId));
        }

        if (await SendingValidation.InsecureAsync(configuration, cancellationToken).ConfigureAwait(false)
            is Error insecure)
        {
            return Result.Failure(insecure);
        }

        string algorithm = (await configuration
                .ReadAsync(Settings.TokenSigningAlgorithm, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<string>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // AUTH-KEY-001: the algorithm the key is made for at the next rotation. A key the
        // version cannot make is a value the key does not admit.
        if (!SigningKeys.Signs(algorithm))
        {
            return Result.Failure(Error.From(
                ErrorCodes.ConfigurationValueNotAllowed,
                "key",
                JsonSerializer.SerializeToElement(Settings.TokenSigningAlgorithm.Key.ToString())));
        }

        return await redirects.ValidateAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
