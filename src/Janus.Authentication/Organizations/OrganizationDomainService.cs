using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Organizations;

/// <summary>
/// The domains an organization locks its members' email addresses to, listed, verified
/// by their TXT record and removed by an administrator.
/// </summary>
/// <param name="scope">Whether the caller may manage the domains, and loosen them.</param>
/// <param name="stepUp">What every change asks of the caller's session.</param>
/// <param name="directory">Where the organization is found.</param>
/// <param name="domains">Where each domain's token and verification are kept.</param>
/// <param name="configuration">Where the organization's list is read.</param>
/// <param name="administration">Where the list is written and recorded.</param>
/// <param name="dns">
/// Where a domain's TXT record is read, absent where the deployment registered none.
/// </param>
/// <param name="audit">Where every change is written down.</param>
/// <param name="events">Where a removal's alert goes.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a token is drawn from.</param>
/// <remarks>
/// Implements LIB-API-005, REG-DOM-001, IDN-ORG-006, OPS-CFG-002 and OPS-ALERT-001. The
/// lock is a field of the organization's policy, which is governed from the
/// administrative organization, so <c>domain:manage</c> is asked there as
/// <c>organization:manage</c> is for the rest of the policy. The list and the domains'
/// rows are written in one transaction, so a listed domain always has its token.
/// </remarks>
internal sealed class OrganizationDomainService(
    AdministrativeScope scope,
    StepUpGuard stepUp,
    IOrganizationDirectory directory,
    IDomainStore domains,
    IConfigurationStore configuration,
    ConfigurationAdministration administration,
    IDnsResolver? dns,
    IOrganizationAudit audit,
    IEvents events,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : IOrganizationDomains
{
    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<OrganizationDomain>>> DomainsAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.DomainManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<IReadOnlyList<OrganizationDomain>>(refused);
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<IReadOnlyList<OrganizationDomain>>(Malformed("id"));
        }

        IReadOnlyList<LockedDomain> held = await domains.OfAsync(organization, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<OrganizationDomain>>(
            [.. held.Where(domain => domain.IsListed).Select(domain => domain.Answered())]);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<OrganizationDomain>> AddDomainAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string domain,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domain);

        if (await ReadAsync(context, organization, domain, reason, cancellationToken).ConfigureAwait(false)
            is not { } read)
        {
            return Result.Failure<OrganizationDomain>(Error.From(ErrorCodes.Denied));
        }

        if (read.Refusal is Error refused)
        {
            return Result.Failure<OrganizationDomain>(refused);
        }

        if (read.Listed is LockedDomain listed)
        {
            return Result.Success(listed.Answered());
        }

        // 10 section 4.1a: adding a domain is a loosening, which also asks the
        // permission to loosen the deployment (OPS-CFG-002, entry 201).
        if (await RefusedAsync(context, session, read.Acting, loosening: true, cancellationToken)
                .ConfigureAwait(false)
            is Error withheld)
        {
            return Result.Failure<OrganizationDomain>(withheld);
        }

        DateTimeOffset now = time.GetUtcNow();
        var added = LockedDomain.Listed(organization, read.Domain, randomness, now);
        IReadOnlyList<string> before = read.Stated.EmailDomains ?? [];

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await domains.AddAsync(added, cancellationToken).ConfigureAwait(false);

        if (await WrittenAsync(read, [.. before, read.Domain], loosening: true, cancellationToken)
                .ConfigureAwait(false)
            is Error unwritten)
        {
            return Result.Failure<OrganizationDomain>(unwritten);
        }

        await audit
            .DomainChangedAsync(
                AuditActions.OrganizationDomainAdded,
                organization,
                read.Domain,
                read.Reason,
                read.Acting,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(added.Answered());
    }

    /// <inheritdoc/>
    public async ValueTask<Result<OrganizationDomain>> VerifyDomainAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string domain,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domain);

        if (await ReadAsync(context, organization, domain, reason, cancellationToken).ConfigureAwait(false)
            is not { } read)
        {
            return Result.Failure<OrganizationDomain>(Error.From(ErrorCodes.Denied));
        }

        if (read.Refusal is Error refused)
        {
            return Result.Failure<OrganizationDomain>(refused);
        }

        if (read.Listed is not LockedDomain listed)
        {
            return Result.Failure<OrganizationDomain>(Malformed("domain"));
        }

        if (listed.VerifiedAt is not null)
        {
            return Result.Success(listed.Answered());
        }

        // A verified domain admits the addresses in it, which a listed one did not.
        if (await RefusedAsync(context, session, read.Acting, loosening: true, cancellationToken)
                .ConfigureAwait(false)
            is Error withheld)
        {
            return Result.Failure<OrganizationDomain>(withheld);
        }

        // A lookup that could not be made proves nothing, and is refused exactly as a
        // record that does not carry the token.
        bool proved = dns is not null
            && (await dns.TextRecordsAsync(listed.RecordName, cancellationToken).ConfigureAwait(false))
                .Match(listed.IsProvedBy, _ => false);

        if (!proved)
        {
            return Result.Failure<OrganizationDomain>(Error.From(ErrorCodes.DomainUnverified));
        }

        DateTimeOffset now = time.GetUtcNow();

        listed.Checked(passed: true, now);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await domains.RecordAsync(listed, cancellationToken).ConfigureAwait(false);
        await audit
            .DomainChangedAsync(
                AuditActions.OrganizationDomainVerified,
                organization,
                read.Domain,
                read.Reason,
                read.Acting,
                now,
                cancellationToken)
            .ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(listed.Answered());
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RemoveDomainAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string domain,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(domain);

        if (await ReadAsync(context, organization, domain, reason, cancellationToken).ConfigureAwait(false)
            is not { } read)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (read.Refusal is Error refused)
        {
            return Result.Failure(refused);
        }

        if (read.Listed is not LockedDomain listed)
        {
            return Result.Success();
        }

        List<string> after = [.. (read.Stated.EmailDomains ?? [])
            .Where(named => !string.Equals(named, read.Domain, StringComparison.Ordinal))];

        // Removing the last domain turns the lock off for every other address, which
        // admits more than the lock did.
        bool loosening = after.Count is 0;

        if (await RefusedAsync(context, session, read.Acting, loosening, cancellationToken).ConfigureAwait(false)
            is Error withheld)
        {
            return Result.Failure(withheld);
        }

        DateTimeOffset now = time.GetUtcNow();

        listed.Remove(now);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await domains.RecordAsync(listed, cancellationToken).ConfigureAwait(false);

        if (await WrittenAsync(read, after, loosening, cancellationToken).ConfigureAwait(false)
            is Error unwritten)
        {
            return Result.Failure(unwritten);
        }

        await audit
            .DomainChangedAsync(
                AuditActions.OrganizationDomainRemoved,
                organization,
                read.Domain,
                read.Reason,
                read.Acting,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        // OPS-ALERT-001: a removal stops new sign-ins with addresses in the domain, which
        // the people behind them will notice before anyone reads the audit trail.
        if ((await events
                .PublishAsync(
                    Alerts.Of(
                        AlertCondition.DomainRemoved,
                        organization + ":" + read.Domain,
                        now,
                        Named(organization, read.Domain)),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error unalerted)
        {
            return Result.Failure(unalerted);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <summary>
    /// What an alert about one domain names.
    /// </summary>
    /// <param name="organization">Whose lock.</param>
    /// <param name="domain">Which domain.</param>
    /// <returns>The details.</returns>
    public static Dictionary<string, JsonElement> Named(OrganizationId organization, string domain) =>
        new(capacity: 2, StringComparer.Ordinal)
        {
            ["organization"] = JsonSerializer.SerializeToElement(organization.Value),
            ["domain"] = JsonSerializer.SerializeToElement(domain),
        };

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Stated(string text) =>
        text?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The part every change shares: the gate, the reason, the domain, the organization,
    // its list and the domain's row, in that order.
    private async ValueTask<Change?> ReadAsync(
        AccessContext context,
        OrganizationId organization,
        string domain,
        string reason,
        CancellationToken cancellationToken)
    {
        // A change is made by a person, whose identity the record carries.
        if (context.Acting is not SubjectId acting)
        {
            return null;
        }

        if (await scope.RefusedAsync(context, Permissions.DomainManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Change.Refused(acting, refused);
        }

        if (!DomainName.TryRead(domain, out string named))
        {
            return Change.Refused(acting, Malformed("domain"));
        }

        if (Stated(reason) is not string stated)
        {
            return Change.Refused(acting, Malformed("reason"));
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Change.Refused(acting, Malformed("id"));
        }

        Error? failure = null;

        PolicyOverride held = (await configuration
                .ReadAsync(Settings.OrganizationPolicy, organization.ToString(), cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<PolicyOverride>(error, ref failure));

        if (failure is not null)
        {
            return Change.Refused(acting, failure);
        }

        IReadOnlyList<LockedDomain> rows = await domains.OfAsync(organization, cancellationToken)
            .ConfigureAwait(false);

        return new Change(
            acting,
            organization,
            named,
            stated,
            held,
            rows.FirstOrDefault(row => row.IsListed && string.Equals(row.Domain, named, StringComparison.Ordinal)),
            Refusal: null);
    }

    // A loosening also needs the permission to loosen the deployment; every change is
    // the domain:manage step-up action, judged last.
    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SessionId session,
        SubjectId acting,
        bool loosening,
        CancellationToken cancellationToken)
    {
        if (loosening
            && await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken).ConfigureAwait(false)
                is Error withheld)
        {
            return withheld;
        }

        return await stepUp
            .PassedAsync(acting, session, StepUpAction.DomainManage, cancellationToken)
            .ConfigureAwait(false);
    }

    // The list goes through the one operation that writes runtime configuration, which
    // records the change beside the domain's own row (OPS-CFG-005).
    private async ValueTask<Error?> WrittenAsync(
        Change read,
        List<string> after,
        bool loosening,
        CancellationToken cancellationToken) =>
        (await administration
            .ChangeMemberAsync(
                Settings.OrganizationPolicy,
                read.Organization.ToString(),
                read.Stated with { EmailDomains = after.Count is 0 ? null : after },
                loosening,
                read.Reason,
                read.Acting,
                cancellationToken)
            .ConfigureAwait(false))
        .Match<Error?>(_ => null, error => error);

    private sealed record Change(
        SubjectId Acting,
        OrganizationId Organization,
        string Domain,
        string Reason,
        PolicyOverride Stated,
        LockedDomain? Listed,
        Error? Refusal)
    {
        public static Change Refused(SubjectId acting, Error refusal) =>
            new(acting, default, string.Empty, string.Empty, PolicyOverride.None, null, refusal);
    }
}
