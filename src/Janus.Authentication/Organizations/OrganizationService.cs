using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Authentication.Policies;
using Janus.Authentication.Sessions;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Organizations;

/// <summary>
/// The organizations of the deployment, created, suspended for deletion and restored by
/// an administrator.
/// </summary>
/// <param name="scope">Whether the caller may manage organizations.</param>
/// <param name="stepUp">What a deletion request or its cancellation asks of the caller's session.</param>
/// <param name="directory">Where organizations are written and their members read.</param>
/// <param name="sessions">What a deletion request ends for every member.</param>
/// <param name="audit">Where every change is written down.</param>
/// <param name="configuration">Where the grace window and the policies are read.</param>
/// <param name="administration">Where an organization's policy key is written and recorded.</param>
/// <param name="policies">Where what a change of policy raised is recorded.</param>
/// <param name="alerts">Where a change of policy that weakens a step-up gate is told.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, IDN-ORG-002, IDN-ORG-003, IDN-ORG-004 and OPS-ALERT-001. An organization is
/// the deployment's, and its own grants confer nothing while it is suspended, so
/// <c>organization:manage</c> is asked in the administrative organization: that is the
/// one place a suspended organization can still be restored from.
/// </remarks>
internal sealed class OrganizationService(
    AdministrativeScope scope,
    StepUpGuard stepUp,
    IOrganizationDirectory directory,
    ISessionStore sessions,
    IOrganizationAudit audit,
    IConfigurationStore configuration,
    ConfigurationAdministration administration,
    PolicyResolution policies,
    IAlertChannels alerts,
    IUnitOfWork work,
    TimeProvider time) : IOrganizations
{
    /// <inheritdoc/>
    public async ValueTask<Result<OrganizationId>> CreateAsync(
        AccessContext context,
        string name,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A change is made by a person, whose identity the record carries.
        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure<OrganizationId>(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<OrganizationId>(refused);
        }

        if (Stated(name) is not string named)
        {
            return Result.Failure<OrganizationId>(Malformed("name"));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure<OrganizationId>(Malformed("reason"));
        }

        DateTimeOffset now = time.GetUtcNow();
        var organization = OrganizationId.New(time);

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await directory.CreateAsync(organization, named, now, cancellationToken).ConfigureAwait(false);

        // IDN-ORG-002 and chapter 10 section 4: the organization's policy key is created
        // with it, holding no override, so it starts under the system policy.
        if ((await administration
                .ChangeMemberAsync(
                    Settings.OrganizationPolicy,
                    organization.ToString(),
                    PolicyOverride.None,
                    loosening: false,
                    stated,
                    acting,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error unwritten)
        {
            return Result.Failure<OrganizationId>(unwritten);
        }

        await audit
            .RecordedAsync(AuditActions.OrganizationCreated, organization, stated, acting, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(organization);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RequestDeletionAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is not OrganizationStanding standing)
        {
            return Result.Failure(Malformed("id"));
        }

        // IDN-ORG-004: the refusal is the domain's, answered before anything else is
        // asked of the caller.
        if (standing.IsAdministrative)
        {
            return Result.Failure(Error.From(ErrorCodes.OrganizationProtected));
        }

        if (standing.DeletionRequestedAt is not null)
        {
            return Result.Success();
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.OrganizationDelete, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        DateTimeOffset now = time.GetUtcNow();

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if ((await directory.RequestDeletionAsync(organization, now, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error protectedOrganization)
        {
            return Result.Failure(protectedOrganization);
        }

        // IDN-ORG-003 AC1: the first request on any member's session after the commit
        // is refused, because the sessions end in the transaction that suspends.
        foreach (SubjectId member in await directory.MembersAsync(organization, cancellationToken).ConfigureAwait(false))
        {
            await sessions.EndAccountAsync(member, now, cancellationToken).ConfigureAwait(false);
        }

        await audit
            .RecordedAsync(AuditActions.OrganizationDeletionRequested, organization, stated, acting, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is not OrganizationStanding standing)
        {
            return Result.Failure(Malformed("id"));
        }

        if (standing.DeletionRequestedAt is not DateTimeOffset requestedAt)
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.OrganizationDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => Settings.OrganizationDeletionGrace.Default);

        // IDN-ORG-003: cancellable at any point before the window closes, whether or not
        // the pass that erases has reached it yet.
        if (standing.ErasedAt is not null || now >= requestedAt + grace)
        {
            return Result.Failure(Error.From(ErrorCodes.DeletionWindowElapsed));
        }

        // 09 section 8a: a cancellation gives every member back what the organization
        // grants, so it is stepped up as the request is.
        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.OrganizationDelete, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await directory.CancelDeletionAsync(organization, cancellationToken).ConfigureAwait(false);
        await audit
            .RecordedAsync(AuditActions.OrganizationDeletionCancelled, organization, stated, acting, now, cancellationToken)
            .ConfigureAwait(false);

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<OrganizationPolicy>> PolicyAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure<OrganizationPolicy>(refused);
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<OrganizationPolicy>(Malformed("id"));
        }

        Error? failure = null;

        Policy system = (await configuration.ReadAsync(Settings.PolicyDefault, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));
        PolicyOverride stated = (await configuration
                .ReadAsync(Settings.OrganizationPolicy, organization.ToString(), cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<PolicyOverride>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<OrganizationPolicy>(failure);
        }

        Policy resolved = PolicyStrictness.Tighten(system, stated);

        return Result.Success(
            new OrganizationPolicy(resolved, PolicyStrictness.InForce(system, resolved, stated)));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> ReplacePolicyAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        PolicyOverride replacement,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(replacement);

        if (context.Acting is not SubjectId acting)
        {
            return Result.Failure(Error.From(ErrorCodes.Denied));
        }

        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error refused)
        {
            return Result.Failure(refused);
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure(Malformed("reason"));
        }

        // Chapter 10 section 4.1a: the domain lock is written only through the domain
        // operations, never through the policy.
        if (replacement.EmailDomains is not null)
        {
            return Result.Failure(Malformed("emailDomains"));
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is not OrganizationStanding standing)
        {
            return Result.Failure(Malformed("id"));
        }

        Error? failure = null;

        Policy system = (await configuration.ReadAsync(Settings.PolicyDefault, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));
        PolicyOverride before = (await configuration
                .ReadAsync(Settings.OrganizationPolicy, organization.ToString(), cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<PolicyOverride>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        PolicyOverride after = replacement with { EmailDomains = before.EmailDomains };

        // AUTH-STEP-002a: an organization may tighten any field and may not loosen one
        // below the system policy.
        if (PolicyStrictness.BelowSystem(system, after) is string looser)
        {
            return Result.Failure(Error.From(
                ErrorCodes.ConfigurationPolicyBelowSystem,
                "field",
                JsonSerializer.SerializeToElement(looser)));
        }

        Policy was = PolicyStrictness.Tighten(system, before);
        Policy becomes = PolicyStrictness.Tighten(system, after);

        // AUTH-SESS-005b: the administrative organization is held to a stated floor of
        // AAL2, which no change of its policy takes it below.
        if (standing.IsAdministrative && becomes.RequiredAssurance < AssuranceLevel.Aal2)
        {
            return Result.Failure(Error.From(
                ErrorCodes.ConfigurationValueBelowFloor,
                "field",
                JsonSerializer.SerializeToElement("requiredAssurance")));
        }

        bool loosening = PolicyStrictness.Loosens(was, becomes);

        // OPS-CFG-002 and chapter 10 section 2.1: a loosening of runtime configuration
        // also needs the permission to loosen the deployment.
        if (loosening
            && await scope.RefusedAsync(context, Permissions.SystemAdminister, cancellationToken).ConfigureAwait(false)
                is Error withheld)
        {
            return Result.Failure(withheld);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.PolicyChange, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);

        if ((await administration
                .ChangeMemberAsync(
                    Settings.OrganizationPolicy,
                    organization.ToString(),
                    after,
                    loosening,
                    stated,
                    acting,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error unwritten)
        {
            return Result.Failure(unwritten);
        }

        // AUTH-FACT-017: what the change raised is what a member's sign-in that does
        // not yet meet it is held against.
        _ = await policies
            .RaisedAsync(organization, was, becomes, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        // D-083, OPS-ALERT-001: a policy that asks less at a step-up gate is told as it
        // is made, under the organization whose it is.
        if (PolicyStrictness.WeakenedGates(was, becomes) is { Count: > 0 } weakened
            && (await alerts
                    .RaiseAsync(
                        StepUpWeakening.Of(
                            Settings.OrganizationPolicy.For(organization.ToString()),
                            organization,
                            weakened,
                            time.GetUtcNow()),
                        cancellationToken)
                    .ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unalerted)
        {
            return Result.Failure(unalerted);
        }

        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // API-CONV-002: a free-text field is 1 to 1024 characters after trimming.
    private static string? Stated(string text) =>
        text?.Trim() is { Length: > 0 and <= 1024 } stated ? stated : null;

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));
}
