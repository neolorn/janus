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
/// Implements IDN-ORG-003, IDN-ORG-005 and IDN-MEM-001. Nothing here waits on a
/// human: the window is the whole of the decision, and an organization that reaches
/// its end without a cancellation is erased. One organization per transaction, so a
/// deployment that falls over mid-pass has erased whole organizations and begun none.
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
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many organizations the pass erased, or the refusal that stopped it.</returns>
    public async ValueTask<Result<int>> SweepAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.OrganizationDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => Settings.OrganizationDeletionGrace.Default);

        IReadOnlyList<PendingOrganizationDeletion> elapsed = await organizations
            .DeletingSinceAsync(now - grace, cancellationToken)
            .ConfigureAwait(false);

        int erased = 0;

        foreach (PendingOrganizationDeletion deletion in elapsed)
        {
            Error? refusal = await ErasedAsync(deletion, grace, now, cancellationToken)
                .ConfigureAwait(false);

            if (refusal is not null)
            {
                return Result.Failure<int>(refusal);
            }

            erased++;
        }

        return Result.Success(erased);
    }

    private async ValueTask<Error?> ErasedAsync(
        PendingOrganizationDeletion deletion,
        TimeSpan grace,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<EndedMembership> ended = await organizations
            .EraseAsync(deletion.Organization, now, grace, cancellationToken)
            .ConfigureAwait(false);

        await audit
            .RecordedAsync(
                AuditActions.OrganizationErased,
                acting: null,
                subject: null,
                now,
                Named(deletion, ended.Count),
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        // The erasure has committed, so the announcements are the outstanding work and
        // a consumer that refuses one stops the pass rather than the erasure.
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
                return refused;
            }
        }

        return (await events
                .PublishAsync(
                    new OrganizationErased(
                        now,
                        Key(deletion.Organization, now),
                        deletion.Organization,
                        ended.Count),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, failure => failure);
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
