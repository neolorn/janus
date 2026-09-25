using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Organizations;

/// <summary>
/// The sweep's pass over the verified domains: each one last checked a
/// <c>domain.reverify.interval</c> ago or more has its TXT record looked for again.
/// </summary>
/// <param name="domains">Where the domains and their checks are kept.</param>
/// <param name="dns">
/// Where a domain's TXT record is read, absent where the deployment registered none.
/// </param>
/// <param name="configuration">Where the interval is read.</param>
/// <param name="events">Where a failed check's alert goes.</param>
/// <param name="work">The one transaction each check is recorded in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements REG-DOM-001 AC3 and OPS-ALERT-001. A failed check raises
/// <c>domain-reverification-failed</c> and changes nothing else: the domain stays
/// verified and every membership and session stands, because a DNS outage or a
/// mistaken edit of a zone is not a reason to lock a whole organization out.
/// </remarks>
internal sealed class DomainReverification(
    IDomainStore domains,
    IDnsResolver? dns,
    IConfigurationStore configuration,
    IEvents events,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// Checks every domain that is due.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many domains were checked, or the failure that stopped the pass.</returns>
    public async ValueTask<Result<int>> SweepAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan interval = (await configuration
                .ReadAsync(Settings.DomainReverifyInterval, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<int>(failure);
        }

        DateTimeOffset now = time.GetUtcNow();
        IReadOnlyList<LockedDomain> due = await domains
            .DueAsync(now - interval, cancellationToken)
            .ConfigureAwait(false);

        foreach (LockedDomain domain in due)
        {
            // A lookup that could not be made proves nothing, and is a failed check.
            bool passed = dns is not null
                && (await dns.TextRecordsAsync(domain.RecordName, cancellationToken).ConfigureAwait(false))
                    .Match(domain.IsProvedBy, _ => false);

            domain.Checked(passed, now);

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);
            await domains.RecordAsync(domain, cancellationToken).ConfigureAwait(false);

            if (!passed
                && (await events
                        .PublishAsync(
                            Alerts.Of(
                                AlertCondition.DomainReverificationFailed,
                                domain.Organization + ":" + domain.Domain,
                                now,
                                OrganizationDomainService.Named(domain.Organization, domain.Domain)),
                            cancellationToken)
                        .ConfigureAwait(false))
                    .Match(() => (Error?)null, error => error) is Error unalerted)
            {
                return Result.Failure<int>(unalerted);
            }

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(due.Count);
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }
}
