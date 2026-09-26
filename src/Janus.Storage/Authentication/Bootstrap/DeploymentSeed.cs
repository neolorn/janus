using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Bootstrap;
using Janus.Authentication.Configuration;
using Janus.Authentication.Registration;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Accounts;
using Janus.Identity.Identifiers;
using Janus.Identity.Organizations;
using Janus.Identity.Profiles;
using Janus.Privacy.SubjectKeys;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Bootstrap;

/// <summary>
/// What bootstrap seeds a fresh deployment with, over the identity, authorization and
/// privacy stores.
/// </summary>
/// <param name="context">The context the writes are tracked on.</param>
/// <param name="settings">Where each value set is written.</param>
/// <param name="organizations">Where the administrative organization is written.</param>
/// <param name="accounts">Where each account row is written.</param>
/// <param name="subjectKeys">Where each account's data key is drawn and written.</param>
/// <param name="identifiers">Where each account's identifiers are written.</param>
/// <param name="profiles">Where a display name is written.</param>
/// <param name="roles">Where the administrative roles are defined.</param>
/// <param name="roleAudit">Where each role defined is recorded.</param>
/// <param name="configurationAudit">Where each value set is recorded.</param>
/// <remarks>
/// Implements OPS-BOOT-001, OPS-BOOT-002, IDN-ORG-001, IDN-PRIN-001 and chapter 10
/// section 3. Each
/// write is saved in the caller's transaction as it is made, so the stores that read
/// back what bootstrap wrote before it commits find it.
/// </remarks>
internal sealed class DeploymentSeed(
    StoreContext context,
    IProtectedSettings settings,
    IOrganizationStore organizations,
    IAccountStore accounts,
    ISubjectKeyStore subjectKeys,
    IIdentifierStore identifiers,
    IProfileStore profiles,
    IRoleStore roles,
    IRoleAudit roleAudit,
    IConfigurationAudit configurationAudit) : IDeploymentSeed
{
    /// <inheritdoc/>
    public async ValueTask<bool> AdministeredAsync(CancellationToken cancellationToken)
    {
        Permission administers = Permissions.SystemAdminister;

        // A grant that was revoked administers nothing; one that has expired is still
        // counted, so a deployment that ever had an administrator is never stood up anew.
        return await context.Organizations
                .AnyAsync(organization => organization.IsAdministrative, cancellationToken)
                .ConfigureAwait(false)
            || await context.Grants
                .AnyAsync(
                    grant => !grant.Deny
                        && grant.RevokedAt == null
                        && context.RolePermissions.Any(held => held.Role == grant.Role && held.Permission == administers),
                    cancellationToken)
                .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ConfigureAsync(
        IReadOnlyDictionary<ConfigurationKey, string> written,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(written);

        // OPS-CFG-004 and OPS-CFG-005: what bootstrap sets, protected keys among them, is
        // written by whoever holds the server and never through the application's
        // configuration store, and each value is recorded as any change is. It is the
        // value the deployment starts from, so it is recorded as no loosening:
        // OPS-CFG-002 prices a change made through the application (entry 315).
        foreach ((ConfigurationKey key, string value) in written)
        {
            string? before = await settings.WriteAsync(key, value, cancellationToken).ConfigureAwait(false);

            await configurationAudit
                .ChangedAsync(key, before, value, loosening: false, principal.Reason, principal, at, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DefineRolesAsync(
        IReadOnlyDictionary<RoleName, IReadOnlyList<Permission>> defined,
        SystemPrincipal principal,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(defined);

        foreach ((RoleName name, IReadOnlyList<Permission> permissions) in defined)
        {
            if (await roles.FindAsync(name, cancellationToken).ConfigureAwait(false) is null)
            {
                await roles.CreateAsync(Role.Of(name, permissions), cancellationToken).ConfigureAwait(false);
                await roleAudit.DefinedAsync(name, permissions, principal, at, cancellationToken).ConfigureAwait(false);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CreateOrganizationAsync(
        OrganizationId organization,
        string name,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        await organizations
            .CreateAsync(Organization.CreateAdministrative(organization, name, at), cancellationToken)
            .ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CreateAccountAsync(
        SubjectId subject,
        IReadOnlyList<NewIdentifier> held,
        DisplayName? name,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(held);

        await accounts.AddAsync(Account.Create(subject, at), cancellationToken).ConfigureAwait(false);
        await subjectKeys.CreateAsync(subject, cancellationToken).ConfigureAwait(false);

        // The rows the account's own stores write are read back through them, so the
        // account exists before anything names it.
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IdentifierSet set = await identifiers.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false);

        foreach (NewIdentifier named in held)
        {
            // What bootstrap names is all the account holds, so each kind's count is
            // the most it takes here.
            set.Add(Taken(subject, named), held.Count(other => other.Kind == named.Kind));
            set.Verify(named.Id, named.VerifiedAt);
        }

        await identifiers.RecordAsync(set, cancellationToken).ConfigureAwait(false);

        if (name is DisplayName shown)
        {
            Profile profile = await profiles.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false);
            profile.SetDisplayName(shown);

            await profiles.RecordAsync(profile, cancellationToken).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CreateEmergencyAsync(SubjectId subject, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await accounts.AddAsync(Account.CreateEmergency(subject, at), cancellationToken).ConfigureAwait(false);

        // A break-glass session acts under this subject, and what it does is recorded
        // like any other account's action, so the account has a key like any other.
        await subjectKeys.CreateAsync(subject, cancellationToken).ConfigureAwait(false);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // What bootstrap names is canonical already, so a form that no longer parses is a
    // defect rather than a value to take on quietly.
    private static Identifier Taken(SubjectId subject, NewIdentifier named) =>
        named.Kind is IdentifierKind.Email
            ? Identifier.Email(
                named.Id,
                subject,
                EmailAddress.TryParse(named.Canonical, out EmailAddress address)
                    ? address
                    : throw new InvalidOperationException("The named address is not an address."),
                named.Entered,
                named.VerifiedAt,
                named.Locked)
            : Identifier.Phone(
                named.Id,
                subject,
                PhoneNumber.TryParse(named.Canonical, out PhoneNumber number)
                    ? number
                    : throw new InvalidOperationException("The named number is not a number."),
                named.Entered,
                named.VerifiedAt);
}
