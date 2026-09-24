using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// The daily comparison of the mailboxes the library provisions with the ones the mail
/// server hosts, existence and enabled state both.
/// </summary>
/// <param name="mailboxes">Where the library's mailboxes are.</param>
/// <param name="server">The mail server, absent where the deployment registered none.</param>
/// <param name="alerts">Where the drift's alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements INT-MAIL-006 AC1b and AC1c, INT-MAIL-006a AC3, INT-MAIL-007 AC2 and
/// OPS-OBS-002. The comparison is against the state each mailbox is owed now, so a
/// mailbox reserved for an open or expired invitation is expected disabled and is no
/// drift, and an account that holds none, the reserved emergency account among them,
/// is nothing to look for. What differs is reported as <c>degradation</c> and changed
/// on neither side: silent correction would hide the pipeline that failed.
/// </remarks>
internal sealed class MailboxReconciliation(
    IMailboxStore mailboxes,
    IMailServer? server,
    IAlertChannels alerts,
    TimeProvider time)
{
    private const string Scope = "mailbox.reconciliation";

    /// <summary>
    /// Compares both sides once.
    /// </summary>
    /// <param name="cancellationToken">Abandons the comparison.</param>
    /// <returns>What differs, or the failure that stopped the comparison.</returns>
    public async ValueTask<Result<MailboxDrift>> ReconcileAsync(CancellationToken cancellationToken)
    {
        if (server is null)
        {
            return Result.Success(new MailboxDrift([], Unknown: 0));
        }

        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        IReadOnlyList<HostedMailbox> hosted = (await server.MailboxesAsync(cancellationToken)
                .ConfigureAwait(false))
            .Match(listed => listed, error => Withheld<IReadOnlyList<HostedMailbox>>(error, ref failure));

        // A comparison that could not be made is itself a degradation: the one
        // check that would have found a failed push did not run.
        if (failure is not null)
        {
            return await AlertedAsync(
                    now,
                    new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["listed"] = JsonSerializer.SerializeToElement(false),
                    },
                    Result.Failure<MailboxDrift>(failure),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        IReadOnlyList<MailboxStanding> held = await mailboxes.AllAsync(cancellationToken)
            .ConfigureAwait(false);

        var enabled = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (HostedMailbox mailbox in hosted)
        {
            enabled[mailbox.Address] = mailbox.Enabled;
        }

        var drifted = new List<MailboxId>();
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (MailboxStanding standing in held)
        {
            Mailbox mailbox = standing.Mailbox;
            MailboxState owed = mailbox.Owed(standing.Stands);

            _ = known.Add(mailbox.Address.Value);

            bool agrees = enabled.TryGetValue(mailbox.Address.Value, out bool serving)
                ? owed is not MailboxState.Removed && serving == (owed is MailboxState.Enabled)
                : owed is MailboxState.Removed;

            if (!agrees)
            {
                drifted.Add(mailbox.Id);
            }
        }

        var drift = new MailboxDrift(
            drifted,
            enabled.Keys.Count(address => !known.Contains(address)));

        if (drift.IsEmpty)
        {
            return Result.Success(drift);
        }

        return await AlertedAsync(
                now,
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["mailboxes"] = JsonSerializer.SerializeToElement(
                        drift.Mailboxes.Select(mailbox => mailbox.ToString())),
                    ["unknown"] = JsonSerializer.SerializeToElement(drift.Unknown),
                },
                Result.Success(drift),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<Result<MailboxDrift>> AlertedAsync(
        DateTimeOffset now,
        Dictionary<string, JsonElement> details,
        Result<MailboxDrift> outcome,
        CancellationToken cancellationToken) =>
        (await alerts
                .RaiseAsync(Alerts.Of(AlertCondition.Degradation, Scope, now, details), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => outcome, Result.Failure<MailboxDrift>);
}
