using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Privacy.Erasures;

/// <summary>
/// One pass over the organization deletion windows the clock has run out on: each
/// organization is erased and the fact announced, in one transaction per organization.
/// </summary>
/// <param name="organizations">Where the windows that have run out are read.</param>
/// <param name="events">Where the erasure and every membership it ended are announced.</param>
/// <param name="audit">Where the erasure is written down.</param>
/// <param name="configuration">Where the window's length is read.</param>
/// <param name="work">The one transaction each organization is carried in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements IDN-ORG-003, IDN-ORG-005, IDN-MEM-001, INF-BG-002 and CONV-DESIGN-002.
/// Nothing here waits on a human: the window is the whole of the decision, and an
/// organization that reaches its end without a cancellation is erased. One organization
/// per transaction, carrying the erasure, its audit record and its events, so a
/// deployment that falls over mid-pass has erased whole organizations and begun none. A
/// window that cannot be read is a fault, and the pass erases nothing (X2 of D-166).
/// </remarks>
internal sealed class OrganizationErasureSweep(
    IOrganizationStates organizations,
    IEvents events,
    IPrivacyAudit audit,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time)
{
    private const string Announced = "organization-erased";

    private const string MembershipEnded = "membership-ended";

    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="context">The system principal the pass runs as.</param>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many organizations the pass erased, or the refusal that stopped it.</returns>
    /// <exception cref="ArgumentException">
    /// A person is asking, or the principal may not sweep what has expired.
    /// </exception>
    public async ValueTask<Result<int>> SweepAsync(AccessContext context, CancellationToken cancellationToken)
    {
        SystemPrincipal principal = Sweeping(context);
        DateTimeOffset now = time.GetUtcNow();

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.OrganizationDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

        IReadOnlyList<PendingOrganizationDeletion> elapsed = await organizations
            .DeletingSinceAsync(now - grace, cancellationToken)
            .ConfigureAwait(false);

        int erased = 0;

        foreach (PendingOrganizationDeletion deletion in elapsed)
        {
            Result<int> done = await ErasedAsync(principal, deletion, grace, now, cancellationToken)
                .ConfigureAwait(false);

            if (done.Match<Error?>(_ => null, error => error) is Error refusal)
            {
                return Result.Failure<int>(refusal);
            }

            erased += done.Match(count => count, _ => 0);
        }

        return Result.Success(erased);
    }

    // INF-BG-002 AC1: the pass runs as a named principal that may sweep what has
    // expired, and never as nobody.
    private static SystemPrincipal Sweeping(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.ExpirySweep)
            ? principal
            : throw new ArgumentException(
                "The pass runs as a system principal that may sweep what has expired.",
                nameof(context));

    private async ValueTask<Result<int>> ErasedAsync(
        SystemPrincipal principal,
        PendingOrganizationDeletion deletion,
        TimeSpan grace,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<int>(notBegun);
        }

        // D-166 X3: a cancellation committed since the pass read its list is the one
        // the organization follows, and the erasure leaves it be.
        if (await organizations.EraseAsync(deletion.Organization, now, grace, cancellationToken).ConfigureAwait(false)
            is not IReadOnlyList<EndedMembership> ended)
        {
            return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match(() => Result.Success(0), Result.Failure<int>);
        }

        // IDN-ORG-005: the record is filed under the organization it erased.
        await audit
            .RecordedAsync(
                AuditActions.OrganizationErased,
                principal,
                subject: null,
                deletion.Organization,
                now,
                Named(deletion, ended.Count),
                cancellationToken)
            .ConfigureAwait(false);

        // X1 of D-166: each event is a row written in the erasure's transaction, so a
        // row that cannot be written fails the erasure and nothing commits.
        foreach (EndedMembership membership in ended)
        {
            if ((await events
                    .PublishAsync(
                        new MembershipChanged(
                            now,
                            Key(membership.Membership, now),
                            membership.Membership,
                            deletion.Organization,
                            MembershipChange.Ended)
                        {
                            Subject = membership.Subject,
                        },
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match(() => (Error?)null, failure => failure) is Error refused)
            {
                return Result.Failure<int>(refused);
            }
        }

        if ((await events
                .PublishAsync(
                    new OrganizationErased(
                        now,
                        Key(deletion.Organization, now),
                        deletion.Organization,
                        ended.Count),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, failure => failure) is Error unannounced)
        {
            return Result.Failure<int>(unannounced);
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => Result.Success(1), Result.Failure<int>);
    }

    private static string Key(OrganizationId organization, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{Announced}:{organization.Value}@{at.UtcTicks}");

    private static string Key(MembershipId membership, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{MembershipEnded}:{membership.Value}@{at.UtcTicks}");

    private static Dictionary<string, JsonElement> Named(
        PendingOrganizationDeletion deletion,
        int ended) =>
        new(capacity: 3, StringComparer.Ordinal)
        {
            ["organization"] = JsonSerializer.SerializeToElement(deletion.Organization.Value),
            ["deletingSince"] = JsonSerializer.SerializeToElement(deletion.Since),
            ["membershipsEnded"] = JsonSerializer.SerializeToElement(ended),
        };
}
