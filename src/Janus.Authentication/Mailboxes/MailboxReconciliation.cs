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
/// The daily comparison of the mailboxes the library provisions with the accounts the
/// mail server hosts, existence, address and enabled state all.
/// </summary>
/// <param name="mailboxes">Where the library's mailboxes are.</param>
/// <param name="inUse">The mail server in use, where the deployment has one.</param>
/// <param name="alerts">Where the drift's alert goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements INT-MAIL-006 AC1b and AC1c, INT-MAIL-006a AC3, INT-MAIL-007 AC2 and
/// OPS-OBS-002. The comparison is against the state each mailbox is owed now, so a
/// mailbox reserved for an open or expired invitation is expected disabled and is no
/// drift, and an account that holds none, the reserved emergency account among them,
/// is nothing to look for. Each mailbox is compared with the account listed under its
/// own identifier, never by address, so the two mailboxes a replacement leaves at one
/// address stay apart (D-177); an account carrying no identifier of a mailbox the
/// library holds is counted, and the account a mailbox of an erased holder left is
/// among those, since that mailbox is not read at all. What differs is reported as
/// <c>degradation</c> and changed on neither side: silent correction would hide the
/// pipeline that failed.
/// </remarks>
internal sealed class MailboxReconciliation(
    IMailboxStore mailboxes,
    IMailServerInUse inUse,
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
        if (inUse.Chosen().Match<IMailServer?>(chosen => chosen, _ => null) is not IMailServer server)
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

        var known = new HashSet<MailboxId>(held.Select(standing => standing.Mailbox.Id));
        var listed = new Dictionary<MailboxId, HostedMailbox>();
        int unknown = 0;

        foreach (HostedMailbox account in hosted)
        {
            if (account.Mailbox is MailboxId carried && known.Contains(carried))
            {
                listed[carried] = account;
            }
            else
            {
                unknown++;
            }
        }

        var drifted = new List<MailboxId>();

        foreach (MailboxStanding standing in held)
        {
            Mailbox mailbox = standing.Mailbox;
            MailboxState owed = mailbox.Owed(standing.Stands);

            bool agrees = listed.TryGetValue(mailbox.Id, out HostedMailbox? account)
                ? owed is not MailboxState.Removed
                    && account.Enabled == (owed is MailboxState.Enabled)
                    && Addresses(account, mailbox)
                : owed is MailboxState.Removed;

            if (!agrees)
            {
                drifted.Add(mailbox.Id);
            }
        }

        var drift = new MailboxDrift(drifted, unknown);

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

    // IDN-ACCT-004: the server's address is read in its canonical form before it is
    // compared, and one that does not read is no mailbox's address.
    private static bool Addresses(HostedMailbox account, Mailbox mailbox) =>
        account.Address is EmailAddress listed
        && string.Equals(listed.Value, mailbox.Address.Value, StringComparison.Ordinal);

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
                .RaiseAsync(Alerts.Scoped(AlertCondition.Degradation, Scope, now, details), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => outcome, Result.Failure<MailboxDrift>);
}
