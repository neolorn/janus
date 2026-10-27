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
/// <param name="alerts">Where a removal's alert goes.</param>
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
    IAlertChannels alerts,
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
            return Result.Failure<IReadOnlyList<OrganizationDomain>>(Error.From(ErrorCodes.OrganizationNotFound));
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
            is not { } unlocked)
        {
            return Result.Failure<OrganizationDomain>(Error.From(ErrorCodes.Denied));
        }

        if (unlocked.Refusal is Error refused)
        {
            return Result.Failure<OrganizationDomain>(refused);
        }

        if (unlocked.Listed is LockedDomain standing)
        {
            return Result.Success(standing.Answered());
        }

        // REG-DOM-001, X6 of D-166: a listed domain is verified and re-verified by its
        // TXT record, which the library reads through the resolver the deployment
        // declares, so without one no domain is listed.
        if (dns is null)
        {
            return Result.Failure<OrganizationDomain>(Unresolvable);
        }

        // 10 section 4.1a: adding a domain is a loosening, which also asks the
        // permission to loosen the deployment (OPS-CFG-002, entry 201). CONV-DESIGN-002,
        // D-207: the step-up, which may ask the carrier's signal, is judged before the
        // unit of work begins, after every refusal it must follow.
        if (await RefusedAsync(context, session, unlocked.Acting, loosening: true, cancellationToken)
                .ConfigureAwait(false)
            is Error withheld)
        {
            return Result.Failure<OrganizationDomain>(withheld);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<OrganizationDomain>(notBegun);
        }

        Change read = await HeldAsync(context, unlocked, cancellationToken).ConfigureAwait(false);

        if (read.Refusal is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<OrganizationDomain>(since);
        }

        // CONV-DESIGN-003: a domain listed meanwhile is added having written nothing, so
        // the unit of work is rolled back.
        if (read.Listed is LockedDomain listed)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success(listed.Answered());
        }

        // The loosening is judged again under the list's lock, as every refusal the
        // step-up follows is.
        if (await LooseningRefusedAsync(context, loosening: true, cancellationToken).ConfigureAwait(false)
            is Error unloosened)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<OrganizationDomain>(unloosened);
        }

        DateTimeOffset now = time.GetUtcNow();
        var added = LockedDomain.Listed(organization, read.Domain, randomness, now);
        IReadOnlyList<string> before = read.Stated.EmailDomains ?? [];

        await domains.AddAsync(added, cancellationToken).ConfigureAwait(false);

        if (await WrittenAsync(read, [.. before, read.Domain], loosening: true, context.BreakGlassReason, cancellationToken)
                .ConfigureAwait(false)
            is Error unwritten)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<OrganizationDomain>(unwritten);
        }

        await audit
            .DomainChangedAsync(
                AuditActions.OrganizationDomainAdded,
                organization,
                read.Domain,
                read.Reason,
                read.Acting,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<OrganizationDomain>(notCommitted);
        }

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

        // API-CONV-003: a domain never listed, or removed, is a record the organization
        // does not hold.
        if (read.Listed is not LockedDomain listed)
        {
            return Result.Failure<OrganizationDomain>(Error.From(ErrorCodes.DomainNotFound));
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<OrganizationDomain>(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.DomainManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<OrganizationDomain>(since);
        }

        await domains.RecordAsync(listed, cancellationToken).ConfigureAwait(false);
        await audit
            .DomainChangedAsync(
                AuditActions.OrganizationDomainVerified,
                organization,
                read.Domain,
                read.Reason,
                read.Acting,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<OrganizationDomain>(notCommitted);
        }

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
            is not { } unlocked)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (unlocked.Refusal is Error refused)
        {
            return Result.Failure(refused);
        }

        if (unlocked.Listed is null)
        {
            return Result.Success();
        }

        // CONV-DESIGN-002, D-207: the step-up, which may ask the carrier's signal, is
        // judged before the unit of work begins, after every refusal it must follow.
        if (await RefusedAsync(context, session, unlocked.Acting, Loosens(unlocked), cancellationToken)
                .ConfigureAwait(false)
            is Error withheld)
        {
            return Result.Failure(withheld);
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        Change read = await HeldAsync(context, unlocked, cancellationToken).ConfigureAwait(false);

        if (read.Refusal is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // CONV-DESIGN-003: a domain removed meanwhile is removed having written nothing,
        // so the unit of work is rolled back.
        if (read.Listed is not LockedDomain listed)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        // The loosening is judged again under the list's lock, as every refusal the
        // step-up follows is.
        bool loosening = Loosens(read);

        if (await LooseningRefusedAsync(context, loosening, cancellationToken).ConfigureAwait(false)
            is Error unloosened)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unloosened);
        }

        List<string> after = Remaining(read);

        DateTimeOffset now = time.GetUtcNow();

        listed.Remove(now);

        await domains.RecordAsync(listed, cancellationToken).ConfigureAwait(false);

        if (await WrittenAsync(read, after, loosening, context.BreakGlassReason, cancellationToken).ConfigureAwait(false)
            is Error unwritten)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unwritten);
        }

        await audit
            .DomainChangedAsync(
                AuditActions.OrganizationDomainRemoved,
                organization,
                read.Domain,
                read.Reason,
                read.Acting,
                context.BreakGlassReason,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        // OPS-ALERT-001: a removal stops new sign-ins with addresses in the domain, which
        // the people behind them will notice before anyone reads the audit trail.
        if ((await alerts
                .RaiseAsync(
                    Alerts.Of(
                        AlertCondition.DomainRemoved,
                        organization + ":" + read.Domain,
                        now,
                        Named(organization, read.Domain)),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error) is Error unalerted)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unalerted);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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

    private static Error Unresolvable { get; } = new(
        ErrorCodes.ConfigurationValueNotAllowed,
        new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
        {
            ["field"] = JsonSerializer.SerializeToElement("emailDomains"),
            ["requires"] = JsonSerializer.SerializeToElement("dnsResolver"),
        });

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // The part every change shares: the gate, the reason, the domain, the organization,
    // its list and the domain's row, in that order, read without a lock.
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

        // 09 section 8a: every change to the list is a configuration change of the
        // organization's policy key, so a blank reason is refused as a change without
        // one is.
        if (ConfigurationAdministration.Unexplained(Settings.OrganizationPolicy.For(organization.ToString()), reason)
            is Error unexplained)
        {
            return Change.Refused(acting, unexplained);
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Change.Refused(acting, Error.From(ErrorCodes.OrganizationNotFound));
        }

        return await ListedAsync(
                new Change(acting, organization, named, reason.Trim(), PolicyOverride.None, null, Refusal: null),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // A change that writes the list reads it again inside its unit of work, with the
    // list's row under a lock, so what it decides is decided on the list in force (X3,
    // OPS-CFG-002 AC6). The caller has begun the unit of work and ends it on every
    // return, a refusal by rolling it back (CONV-DESIGN-003).
    private async ValueTask<Change> HeldAsync(
        AccessContext context,
        Change unlocked,
        CancellationToken cancellationToken)
    {
        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed
        // since the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.DomainManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            return Change.Refused(unlocked.Acting, since);
        }

        await administration
            .HoldAsync(Settings.OrganizationPolicy, unlocked.Organization.ToString(), cancellationToken)
            .ConfigureAwait(false);

        return await ListedAsync(unlocked, cancellationToken).ConfigureAwait(false);
    }

    // The organization's list as it stands, and the domain's row where it is listed.
    private async ValueTask<Change> ListedAsync(Change change, CancellationToken cancellationToken)
    {
        Error? failure = null;

        PolicyOverride held = (await configuration
                .ReadAsync(Settings.OrganizationPolicy, change.Organization.ToString(), cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<PolicyOverride>(error, ref failure));

        if (failure is not null)
        {
            return Change.Refused(change.Acting, failure);
        }

        IReadOnlyList<LockedDomain> rows = await domains.OfAsync(change.Organization, cancellationToken)
            .ConfigureAwait(false);

        return change with
        {
            Stated = held,
            Listed = rows.FirstOrDefault(
                row => row.IsListed && string.Equals(row.Domain, change.Domain, StringComparison.Ordinal)),
        };
    }

    // What the list holds once the domain is taken off it.
    private static List<string> Remaining(Change read) =>
        [.. (read.Stated.EmailDomains ?? [])
            .Where(named => !string.Equals(named, read.Domain, StringComparison.Ordinal))];

    // Removing the last domain turns the lock off for every other address, which admits
    // more than the lock did.
    private static bool Loosens(Change read) => Remaining(read).Count is 0;

    // A loosening also needs the permission to loosen the deployment; every change is
    // the domain:manage step-up action, judged last.
    private async ValueTask<Error?> RefusedAsync(
        AccessContext context,
        SessionId session,
        SubjectId acting,
        bool loosening,
        CancellationToken cancellationToken) =>
        await LooseningRefusedAsync(context, loosening, cancellationToken).ConfigureAwait(false)
            ?? await stepUp
                .PassedAsync(acting, session, StepUpAction.DomainManage, cancellationToken)
                .ConfigureAwait(false);

    // OPS-CFG-002 and chapter 10 section 2.1: a loosening of runtime configuration also
    // needs the permission to loosen the deployment.
    private async ValueTask<Error?> LooseningRefusedAsync(
        AccessContext context,
        bool loosening,
        CancellationToken cancellationToken) =>
        loosening
            ? await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken).ConfigureAwait(false)
            : null;

    // The list goes through the one operation that writes runtime configuration, which
    // records the change beside the domain's own row (OPS-CFG-005).
    private async ValueTask<Error?> WrittenAsync(
        Change read,
        List<string> after,
        bool loosening,
        string? breakGlassReason,
        CancellationToken cancellationToken) =>
        (await administration
            .ChangeMemberAsync(
                Settings.OrganizationPolicy,
                read.Organization.ToString(),
                read.Stated with { EmailDomains = after.Count is 0 ? null : after },
                read.Stated,
                loosening,
                read.Reason,
                read.Acting,
                breakGlassReason,
                cancellationToken)
            .ConfigureAwait(false))
        .Match<Error?>(() => null, error => error);

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
