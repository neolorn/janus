using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Authentication.Alerting;
using Janus.Authentication.Configuration;
using Janus.Authentication.Policies;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Organizations;

/// <summary>
/// An organization set up from the server: created by name where none holds it, its
/// policy written from a file and, where asked, whether an account may hold several
/// memberships, each change written down under the command's principal.
/// </summary>
/// <param name="administrative">Which organization administers the deployment, which bootstrap made.</param>
/// <param name="directory">Where organizations are found by name and created.</param>
/// <param name="audit">Where the organization's creation is written down.</param>
/// <param name="configuration">Where the values in force are read.</param>
/// <param name="locks">Where each setting's row is held before its value in force is read.</param>
/// <param name="settings">Where each value is written, as a command on the server writes one.</param>
/// <param name="changes">Where each change of a value is written down.</param>
/// <param name="policies">Where what a change of policy raised is recorded.</param>
/// <param name="alerts">Where a change of policy that weakens a step-up gate is told.</param>
/// <param name="events">Where the creation is announced.</param>
/// <param name="work">The one transaction the command runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-BOOT-001, IDN-ORG-002, IDN-PRIN-001, OPS-CFG-005, OPS-CFG-003 and
/// D-248. Whoever reaches the database already holds more than an administrator does
/// (D-028), so the command asks no step-up and no permission; what it keeps is every
/// check the policy route makes of a policy, the reason and the record. It runs only on
/// a deployment bootstrap stood up, and it writes no grant and no membership: the
/// organization's first grants are a system administrator's to write (AUTHZ-SCOPE-001,
/// D-207). The command reads no host declaration, so it never writes photos on, as
/// bootstrap does not.
/// </remarks>
internal sealed class OrganizationFromServer(
    IAdministrativeOrganization administrative,
    IOrganizationDirectory directory,
    IOrganizationAudit audit,
    IConfigurationStore configuration,
    IConfigurationWrites locks,
    IProtectedSettings settings,
    IConfigurationAudit changes,
    PolicyResolution policies,
    IAlertRaising alerts,
    IEvents events,
    IUnitOfWork work,
    TimeProvider time)
{
    /// <summary>
    /// The name of the principal the command records what it does under.
    /// </summary>
    internal const string PrincipalName = "organization";

    private const string Created = "organization-created";

    // IDN-PRIN-001: nobody is signed in while the command runs, so what it does is
    // recorded under a principal of its own that may do nothing else.
    private static readonly SystemPrincipal Principal =
        SystemPrincipal.ForDeployment(PrincipalName, "OPS-BOOT-001", SystemOperation.Configuration);

    /// <summary>
    /// Sets the organization up, or refuses to.
    /// </summary>
    /// <param name="request">What the command line named.</param>
    /// <param name="cancellationToken">Abandons the command, which then leaves nothing behind.</param>
    /// <returns>The organization's identifier, or the failure naming what was refused.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public async ValueTask<Result<OrganizationId>> RunAsync(
        OrganizationFromServerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Stated(request.Name) is not string named)
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

        // OPS-CFG-005: every change the command makes carries the reason, so a command
        // without one changes nothing, as a change of a protected key from the server.
        if (request.Reason.Trim() is not { Length: > 0 } stated)
        {
            return Result.Failure<OrganizationId>(Error.From(ErrorCodes.ConfigurationChangeReasonRequired));
        }

        if (stated.Length > 1024)
        {
            return Result.Failure<OrganizationId>(Malformed("reason"));
        }

        // Chapter 10 section 4.1a: the domain lock is written only through the domain
        // operations, never through the policy.
        if (request.Policy.EmailDomains is not null)
        {
            return Result.Failure<OrganizationId>(Malformed("emailDomains"));
        }

        // IDN-ATTR-002, OPS-CFG-003: a policy that shows photos needs the codec a host
        // declares, and the command reads no declaration.
        if (request.Policy.Photos is true)
        {
            return Result.Failure<OrganizationId>(ProfilePhotos.Undeclared);
        }

        DateTimeOffset now = time.GetUtcNow();

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure<OrganizationId>(notBegun);
        }

        Error? failure = null;

        OrganizationId organization = (await SetUpAsync(request, named, stated, now, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<OrganizationId>(error, ref failure));

        if (failure is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure<OrganizationId>(failure);
        }

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure<OrganizationId>(notCommitted);
        }

        return Result.Success(organization);
    }

    // Inside the command's transaction: everything is judged on the values in force
    // under their locks before anything is written, so a refusal leaves nothing behind
    // and a concurrent change waits (X3 of D-166, OPS-CFG-002 AC6).
    private async ValueTask<Result<OrganizationId>> SetUpAsync(
        OrganizationFromServerRequest request,
        string named,
        string stated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // OPS-BOOT-001 and AUTHZ-SCOPE-001: an organization is the administrative
        // organization's to manage, so before bootstrap marked one nothing is set up.
        if (await administrative.FindAsync(cancellationToken).ConfigureAwait(false) is null)
        {
            return Result.Failure<OrganizationId>(Error.From(ErrorCodes.Denied));
        }

        IReadOnlyList<OrganizationStanding> holding = await directory
            .HoldNamedAsync(named, cancellationToken)
            .ConfigureAwait(false);

        // D-248: a name two organizations hold names no one organization to set up.
        if (holding.Count > 1)
        {
            return Result.Failure<OrganizationId>(Malformed("name"));
        }

        OrganizationStanding? existing = holding.Count is 1 ? holding[0] : null;
        OrganizationId organization = existing?.Id ?? OrganizationId.New(time);
        ConfigurationKey policyKey = Settings.OrganizationPolicy.For(organization.ToString());

        await locks.HoldAsync(Settings.PolicyDefault.Key, cancellationToken).ConfigureAwait(false);
        await locks.HoldAsync(policyKey, cancellationToken).ConfigureAwait(false);

        if (request.MultipleMemberships is not null)
        {
            await locks.HoldAsync(Settings.OrganizationMultipleMemberships.Key, cancellationToken).ConfigureAwait(false);
        }

        Error? failure = null;

        Policy system = (await configuration.ReadAsync(Settings.PolicyDefault, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        // IDN-ORG-002 and chapter 10 section 4: a new organization's policy key starts
        // as the route that creates one writes it, holding the grammatical gender on
        // and no other override (D-236), and the file replaces that.
        PolicyOverride before = existing is null
            ? Settings.OrganizationPolicy.Default
            : (await configuration
                    .ReadAsync(Settings.OrganizationPolicy, organization.ToString(), cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Held<PolicyOverride>(error, ref failure));

        bool? multipleInForce = failure is null && request.MultipleMemberships is not null
            ? (await configuration
                    .ReadAsync(Settings.OrganizationMultipleMemberships, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => (bool?)value, error => Held<bool?>(error, ref failure))
            : null;

        PolicyReplacement? judged = failure is null
            ? PolicyReplacement.Of(system, before, request.Policy, existing?.IsAdministrative ?? false)
                .Match(value => value, error => Held<PolicyReplacement>(error, ref failure))
            : null;

        if (failure is not null)
        {
            return Result.Failure<OrganizationId>(failure);
        }

        if (existing is null
            && await CreatedAsync(organization, named, stated, now, cancellationToken).ConfigureAwait(false)
                is Error uncreated)
        {
            return Result.Failure<OrganizationId>(uncreated);
        }

        // D-248: an organization that stands is changed only where the file differs.
        if (judged!.Changes
            && await ReplacedAsync(organization, judged, stated, now, cancellationToken).ConfigureAwait(false)
                is Error unreplaced)
        {
            return Result.Failure<OrganizationId>(unreplaced);
        }

        if (request.MultipleMemberships is bool multiple && multiple != multipleInForce)
        {
            await ChangedAsync(
                    Settings.OrganizationMultipleMemberships.Key,
                    Settings.OrganizationMultipleMemberships.Write(multipleInForce!.Value),
                    Settings.OrganizationMultipleMemberships.Write(multiple),
                    Settings.OrganizationMultipleMemberships.Loosens(multipleInForce.Value, multiple),
                    stated,
                    now,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success(organization);
    }

    // IDN-ORG-002, chapter 10 section 5b: the organization is created as the route
    // creates one, its policy key written with it as a configuration change, its
    // creation recorded and announced in the transaction that makes it, so a host hears
    // of none that did not commit.
    private async ValueTask<Error?> CreatedAsync(
        OrganizationId organization,
        string named,
        string stated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await directory.CreateAsync(organization, named, now, cancellationToken).ConfigureAwait(false);

        await ChangedAsync(
                Settings.OrganizationPolicy.For(organization.ToString()),
                Settings.OrganizationPolicy.Write(PolicyOverride.None),
                Settings.OrganizationPolicy.Write(Settings.OrganizationPolicy.Default),
                loosening: false,
                stated,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        await audit
            .RecordedAsync(AuditActions.OrganizationCreated, organization, stated, Principal, now, cancellationToken)
            .ConfigureAwait(false);

        return (await events
                .PublishAsync(
                    new OrganizationCreated(
                        now,
                        string.Create(CultureInfo.InvariantCulture, $"{Created}:{organization.Value}@{now.UtcTicks}"),
                        organization),
                    cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
    }

    // Chapter 09 section 8a: the policy is written as the policy route writes it, with
    // what the change raised recorded (AUTH-FACT-017) and a weakened step-up gate told
    // (D-083, OPS-ALERT-001) in the same transaction.
    private async ValueTask<Error?> ReplacedAsync(
        OrganizationId organization,
        PolicyReplacement judged,
        string stated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ConfigurationKey key = Settings.OrganizationPolicy.For(organization.ToString());

        await ChangedAsync(
                key,
                Settings.OrganizationPolicy.Write(judged.Before),
                Settings.OrganizationPolicy.Write(judged.After),
                judged.Loosening,
                stated,
                now,
                cancellationToken)
            .ConfigureAwait(false);

        _ = await policies
            .RaisedAsync(organization, judged.Was, judged.Becomes, now, cancellationToken)
            .ConfigureAwait(false);

        if (PolicyStrictness.WeakenedGates(judged.Was, judged.Becomes) is not { Count: > 0 } weakened)
        {
            return null;
        }

        return (await alerts
                .RaiseAsync(StepUpWeakening.Of(key, organization, weakened, now), cancellationToken)
                .ConfigureAwait(false))
            .Match(() => (Error?)null, error => error);
    }

    // OPS-CFG-005 and OPS-CFG-008: a value set from the server is written by the one
    // writer of the values a command sets, and recorded under the command's principal
    // with what it was, what it is, its direction and the reason.
    private async ValueTask ChangedAsync(
        ConfigurationKey key,
        string before,
        string after,
        bool loosening,
        string stated,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _ = await settings.WriteAsync(key, after, cancellationToken).ConfigureAwait(false);

        await changes
            .ChangedAsync(key, before, after, loosening, stated, Principal, now, cancellationToken)
            .ConfigureAwait(false);
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
