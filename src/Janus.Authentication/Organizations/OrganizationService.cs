using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
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
/// <param name="administrative">Which organization administers the deployment.</param>
/// <param name="stepUp">What a deletion request or its cancellation asks of the caller's session.</param>
/// <param name="directory">Where organizations are written and their members read.</param>
/// <param name="sessions">What a deletion request ends for every member.</param>
/// <param name="audit">Where every change is written down.</param>
/// <param name="configuration">Where the grace window and the policies are read.</param>
/// <param name="administration">Where an organization's policy key is written and recorded.</param>
/// <param name="policies">Where what a change of policy raised is recorded.</param>
/// <param name="alerts">Where a change of policy that weakens a step-up gate is told.</param>
/// <param name="events">Where a creation, a deletion request and its cancellation are announced.</param>
/// <param name="codec">
/// What the deployment reads uploaded images with, or nothing where it declared none.
/// </param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements LIB-API-005, IDN-ACCT-004, IDN-ACCT-005, IDN-ORG-002, IDN-ORG-003,
/// IDN-ORG-004, IDN-READ-001 and OPS-ALERT-001. An organization is the deployment's,
/// and its own grants confer nothing while it is suspended, so <c>organization:manage</c>
/// is asked in the administrative organization: that is the one place a suspended
/// organization can still be restored from. A name is judged on its comparison key, as
/// every identifier is.
/// </remarks>
internal sealed class OrganizationService(
    AdministrativeScope scope,
    IAdministrativeOrganization administrative,
    StepUpGuard stepUp,
    IOrganizationDirectory directory,
    ISessionStore sessions,
    IOrganizationAudit audit,
    IConfigurationStore configuration,
    ConfigurationAdministration administration,
    PolicyResolution policies,
    IAlertChannels alerts,
    IEvents events,
    ImageCodec? codec,
    IUnitOfWork work,
    TimeProvider time) : IOrganizations
{
    private const string Created = "organization-created";

    private const string DeletionRequested = "organization-deletion-requested";

    private const string DeletionCancelled = "organization-deletion-cancelled";

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

        // IDN-ACCT-004 and IDN-ACCT-005: the name is judged on its comparison key, as
        // every identifier is, and one the key reduces to nothing is no name.
        string key = CanonicalForm.Of(named);

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<OrganizationId>(Malformed("name"));
        }

        if (!ScriptMixing.IsSingleScriptPerWord(key))
        {
            return Result.Failure<OrganizationId>(
                Error.From(ErrorCodes.IdentifierMixedScript, "member", JsonSerializer.SerializeToElement("name")));
        }

        if (Stated(reason) is not string stated)
        {
            return Result.Failure<OrganizationId>(Malformed("reason"));
        }

        DateTimeOffset now = time.GetUtcNow();
        var organization = OrganizationId.New(time);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<OrganizationId>(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<OrganizationId>(since);
        }

        await directory.CreateAsync(organization, named, now, cancellationToken).ConfigureAwait(false);

        // IDN-ORG-002 and chapter 10 section 4: the organization's policy key is created
        // with it, holding no override, so it starts under the system policy.
        if ((await administration
                .ChangeMemberAsync(
                    Settings.OrganizationPolicy,
                    organization.ToString(),
                    PolicyOverride.None,
                    before: PolicyOverride.None,
                    loosening: false,
                    stated,
                    acting,
                    context.BreakGlassReason,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unwritten)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<OrganizationId>(unwritten);
        }

        // Chapter 10 section 5b (D-218): the event is written in the transaction that
        // creates the organization, so a host hears of none that did not commit.
        if (await AnnouncedAsync(
                new OrganizationCreated(now, Key(Created, organization, now), organization),
                context,
                cancellationToken)
                .ConfigureAwait(false)
            is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<OrganizationId>(unannounced);
        }

        await audit
            .RecordedAsync(AuditActions.OrganizationCreated, organization, stated, acting, context.BreakGlassReason, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<OrganizationId>(notCommitted);
        }

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
            return Result.Failure(Error.From(ErrorCodes.OrganizationNotFound));
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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: decided again on the organization's row under its lock, so a second
        // request at the same moment finds the first and answers as it would after it.
        if ((await directory.HoldAsync(organization, cancellationToken).ConfigureAwait(false))?.DeletionRequestedAt
            is not null)
        {
            // CONV-DESIGN-003: nothing was written, so the unit of work is rolled back.
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        if ((await directory.RequestDeletionAsync(organization, now, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error protectedOrganization)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(protectedOrganization);
        }

        // IDN-ORG-003 AC1: the first request on any member's session after the commit
        // is refused, because the sessions end in the transaction that suspends.
        foreach (SubjectId member in await directory.MembersAsync(organization, cancellationToken).ConfigureAwait(false))
        {
            if (await SessionEnds
                    .AnnouncedAsync(
                        events,
                        await sessions.EndAccountAsync(member, now, cancellationToken).ConfigureAwait(false),
                        SessionEndReason.Organization,
                        now,
                        context,
                        cancellationToken)
                    .ConfigureAwait(false) is Error unended)
            {
                await work.RollbackAsync().ConfigureAwait(false);

                return Result.Failure(unended);
            }
        }

        if (await AnnouncedAsync(
                new OrganizationDeletionRequested(now, Key(DeletionRequested, organization, now), organization),
                context,
                cancellationToken)
                .ConfigureAwait(false)
            is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        await audit
            .RecordedAsync(AuditActions.OrganizationDeletionRequested, organization, stated, acting, context.BreakGlassReason, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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
            return Result.Failure(Error.From(ErrorCodes.OrganizationNotFound));
        }

        if (standing.DeletionRequestedAt is not DateTimeOffset requestedAt)
        {
            return Result.Success();
        }

        DateTimeOffset now = time.GetUtcNow();

        TimeSpan grace = (await configuration
                .ReadAsync(Settings.OrganizationDeletionGrace, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => throw new InvalidOperationException(error.Code.ToString()));

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

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        // D-166 X3: decided again on the organization's row under its lock, which the
        // erasure at the window's end holds too, so of the two only the first stands.
        OrganizationStanding held = await directory.HoldAsync(organization, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("An organization's row is never removed.");

        if (held.ErasedAt is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Error.From(ErrorCodes.DeletionWindowElapsed));
        }

        // A deletion cancelled meanwhile stands as asked: the operation is done with
        // nothing to write.
        if (held.DeletionRequestedAt is null)
        {
            // CONV-DESIGN-003: nothing was written, so the unit of work is rolled back.
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Success();
        }

        await directory.CancelDeletionAsync(organization, cancellationToken).ConfigureAwait(false);

        if (await AnnouncedAsync(
                new OrganizationDeletionCancelled(now, Key(DeletionCancelled, organization, now), organization),
                context,
                cancellationToken)
                .ConfigureAwait(false)
            is Error unannounced)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(unannounced);
        }

        await audit
            .RecordedAsync(AuditActions.OrganizationDeletionCancelled, organization, stated, acting, context.BreakGlassReason, now, cancellationToken)
            .ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

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
            return Result.Failure<OrganizationPolicy>(Error.From(ErrorCodes.OrganizationNotFound));
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

        // 09 section 8a: a replacement is a configuration change of the organization's
        // policy key, so a blank reason is refused as a change without one is.
        if (ConfigurationAdministration.Unexplained(Settings.OrganizationPolicy.For(organization.ToString()), reason)
            is Error unexplained)
        {
            return Result.Failure(unexplained);
        }

        string stated = reason.Trim();

        // Chapter 10 section 4.1a: the domain lock is written only through the domain
        // operations, never through the policy.
        if (replacement.EmailDomains is not null)
        {
            return Result.Failure(Malformed("emailDomains"));
        }

        // IDN-ATTR-002, OPS-CFG-003: a policy that shows photos needs the codec the host
        // declares, so it is refused while there is none.
        if (replacement.Photos is true && codec is null)
        {
            return Result.Failure(ProfilePhotos.Undeclared);
        }

        if (await directory.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is not OrganizationStanding standing)
        {
            return Result.Failure(Error.From(ErrorCodes.OrganizationNotFound));
        }

        // CONV-DESIGN-002, D-207: every refusal the step-up must follow is judged first
        // on the values in force without a lock, so the step-up, which may ask the
        // carrier's signal, is judged before the unit of work begins.
        if ((await JudgedAsync(context, organization, standing, replacement, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error unjudged)
        {
            return Result.Failure(unjudged);
        }

        if (await stepUp
                .PassedAsync(acting, session, StepUpAction.PolicyChange, cancellationToken)
                .ConfigureAwait(false)
            is Error challenged)
        {
            return Result.Failure(challenged);
        }

        // OPS-CFG-002 AC6, X3: the direction, the system's floor and the lock the change
        // keeps are decided again on the values in force under their rows' locks, taken
        // in the order every change takes them, so a concurrent change waits and cannot
        // turn this one into another.
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-GATE-006, D-183: the gate is asked again inside the unit of work, with the
        // acting account's row held before any other lock, so a restriction committed since
        // the gate step refuses the change before anything is written.
        if (await scope.RefusedAsync(context, Permissions.OrganizationManage, cancellationToken).ConfigureAwait(false)
            is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        await administration.HoldAsync(Settings.PolicyDefault, cancellationToken).ConfigureAwait(false);
        await administration
            .HoldAsync(Settings.OrganizationPolicy, organization.ToString(), cancellationToken)
            .ConfigureAwait(false);

        Error? failure = null;

        Replacement judged = (await JudgedAsync(context, organization, standing, replacement, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Replacement>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(failure);
        }

        (PolicyOverride before, PolicyOverride after, Policy was, Policy becomes, bool loosening) = judged;

        if ((await administration
                .ChangeMemberAsync(
                    Settings.OrganizationPolicy,
                    organization.ToString(),
                    after,
                    before,
                    loosening,
                    stated,
                    acting,
                    context.BreakGlassReason,
                    cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unwritten)
        {
            await work.RollbackAsync().ConfigureAwait(false);

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

    /// <inheritdoc/>
    public async ValueTask<Result<OrganizationId>> AdministrativeAsync(CancellationToken cancellationToken) =>
        await administrative.FindAsync(cancellationToken).ConfigureAwait(false) is OrganizationId found
            ? Result.Success(found)
            : Result.Failure<OrganizationId>(Error.From(ErrorCodes.OrganizationNotFound));

    // AUTH-STEP-002a, AUTH-SESS-005b, OPS-CFG-002: what a replacement would make of the
    // policy in force, or the refusal it meets: below the system policy, below the
    // administrative organization's floor, or a loosening the caller may not make. It
    // reads the values as they stand, held where the caller holds their rows.
    private async ValueTask<Result<Replacement>> JudgedAsync(
        AccessContext context,
        OrganizationId organization,
        OrganizationStanding standing,
        PolicyOverride replacement,
        CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy system = (await configuration.ReadAsync(Settings.PolicyDefault, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));
        PolicyOverride before = (await configuration
                .ReadAsync(Settings.OrganizationPolicy, organization.ToString(), cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<PolicyOverride>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<Replacement>(failure);
        }

        PolicyOverride after = replacement with { EmailDomains = before.EmailDomains };

        // AUTH-STEP-002a: an organization may tighten any field and may not loosen one
        // below the system policy.
        if (PolicyStrictness.BelowSystem(system, after) is string looser)
        {
            return Result.Failure<Replacement>(
                Error.From(
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
            return Result.Failure<Replacement>(
                Error.From(
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
            return Result.Failure<Replacement>(withheld);
        }

        return Result.Success(new Replacement(before, after, was, becomes, loosening));
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

    // Chapter 10 section 5b: an organization's event names no subject, and carries the
    // person who acted as the context gives them.
    private async ValueTask<Error?> AnnouncedAsync(
        DomainEvent raised,
        AccessContext context,
        CancellationToken cancellationToken) =>
        (await events
            .PublishAsync(raised with { Actor = context.Acting, Effective = context.Effective }, cancellationToken)
            .ConfigureAwait(false))
        .Match(() => (Error?)null, error => error);

    private static string Key(string kind, OrganizationId organization, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{kind}:{organization.Value}@{at.UtcTicks}");

    private sealed record Replacement(
        PolicyOverride Before,
        PolicyOverride After,
        Policy Was,
        Policy Becomes,
        bool Loosening);
}
