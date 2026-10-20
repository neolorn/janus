using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Authorization.Model;
using Janus.Core;

namespace Janus.Authorization.Resources;

/// <summary>
/// The host's records, placed where the declaration says their types sit.
/// </summary>
/// <param name="model">What the host declared about its domain.</param>
/// <param name="resources">Where the records and their ancestry are written.</param>
/// <param name="holders">Which subjects hold an account.</param>
/// <param name="grants">Where the grants naming a removed record are revoked.</param>
/// <param name="audit">Where each revocation a removal makes is written down.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-SCOPE-001, LIB-HOST-002,
/// AUTHZ-GRANT-003 and IDN-LIFE-002a. A record is placed only in a container of the type
/// its own type is declared contained in, and only in one of its own organization, so no
/// record inherits across a boundary the declaration or the organization draws. A record
/// of a sensitive type is always some account holder's, so every right has an account to
/// hang off. A container is judged again with its organization's tree held, since a
/// removal may have taken it away since it was read.
/// </remarks>
internal sealed class ResourceService(
    AuthorizationModel model,
    IResourceStore resources,
    IAccountHolders holders,
    IGrantStore grants,
    IGrantAudit audit,
    IUnitOfWork work,
    TimeProvider time) : IResources
{
    // AUTHZ-GRANT-003 (D-203): a grant the removal of its record revokes was revoked by
    // nobody's decision, so it names the nil subject and the item that requires it.
    private const string Removal = "AUTHZ-INHERIT-002";

    /// <inheritdoc/>
    public async ValueTask<Result> RegisterAsync(
        ResourceRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return await RegisterManyAsync([registration], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RegisterManyAsync(
        IReadOnlyList<ResourceRegistration> registrations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        foreach (ResourceRegistration registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);
        }

        // What the library already holds is read once per type rather than once per
        // record, so a bulk import asks as many questions as it has types.
        Dictionary<ResourceReference, OrganizationId> held = await HeldAsync(
            [
                .. registrations.Select(registration => registration.Resource),
                .. registrations
                    .Select(registration => registration.ContainedIn)
                    .OfType<ResourceReference>(),
            ],
            cancellationToken).ConfigureAwait(false);

        // IDN-LIFE-002a AC1: whose account stands is read once for the batch.
        IReadOnlySet<SubjectId> standing = await holders
            .HoldingAsync(
                [
                    .. registrations
                        .Select(registration => registration.Subject)
                        .OfType<SubjectId>()
                        .Distinct(),
                ],
                cancellationToken)
            .ConfigureAwait(false);

        // A container registered earlier in the same batch holds its contents as one
        // already registered would.
        var placed = new Dictionary<ResourceReference, OrganizationId>();
        var registered = new List<RegisteredResource>(registrations.Count);

        foreach (ResourceRegistration registration in registrations)
        {
            if (Refused(registration, held, placed, standing) is Error refused)
            {
                return Result.Failure(refused);
            }

            placed.Add(registration.Resource, registration.Organization);
            registered.Add(RegisteredResource.Create(
                registration.Resource,
                registration.Organization,
                registration.Subject,
                registration.ContainedIn));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        if (await ContainersRemovedAsync(registrations, cancellationToken).ConfigureAwait(false))
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Invalid("containedIn"));
        }

        await resources.RegisterManyAsync(registered, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> MoveAsync(
        ResourceReference resource,
        ResourceReference? containedIn,
        CancellationToken cancellationToken)
    {
        if (model.Find(resource.Type) is null)
        {
            return Result.Failure(Malformed("resourceType"));
        }

        if (await resources.FindAsync(resource, cancellationToken).ConfigureAwait(false)
            is not RegisteredResource moving)
        {
            return Result.Failure(Invalid("resourceId"));
        }

        // AUTHZ-MODEL-004 refuses a containment cycle among the types, so a record
        // placed only in a container of its declared type is never placed beneath
        // itself.
        if (!Placeable(resource.Type, containedIn)
            || (containedIn is ResourceReference container
                && (await resources.FindAsync(container, cancellationToken).ConfigureAwait(false)
                    is not RegisteredResource holding
                    || holding.Organization != moving.Organization)))
        {
            return Result.Failure(Invalid("containedIn"));
        }

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // AUTHZ-INHERIT-002 (D-203): the record and its new container are read again with
        // the tree held, which a removal holds too, so neither is one removed since.
        await resources.HoldAsync(moving.Organization, cancellationToken).ConfigureAwait(false);

        if (await resources.FindAsync(resource, cancellationToken).ConfigureAwait(false) is null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Invalid("resourceId"));
        }

        if (containedIn is ResourceReference target
            && await resources.FindAsync(target, cancellationToken).ConfigureAwait(false) is null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(Invalid("containedIn"));
        }

        await resources.MoveAsync(resource, containedIn, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> RemoveAsync(
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        // AUTHZ-INHERIT-002 (D-203): a refusal is told before any transaction begins, so
        // the host's unit of work this call joins is left as it was.
        if (await RemovalRefusedAsync(resource, cancellationToken).ConfigureAwait(false) is Error refused)
        {
            return Result.Failure(refused);
        }

        RegisteredResource removing = await resources.FindAsync(resource, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A registration judged present is read.");

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(_ => null, error => error) is Error notBegun)
        {
            return Result.Failure(notBegun);
        }

        // CONV-DESIGN-003: the tree is held, as a registration and a move hold it, so no
        // record comes to sit inside this one, and none is granted on it, while it goes.
        await resources.HoldAsync(removing.Organization, cancellationToken).ConfigureAwait(false);

        if (await RemovalRefusedAsync(resource, cancellationToken).ConfigureAwait(false) is Error since)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return Result.Failure(since);
        }

        DateTimeOffset now = time.GetUtcNow();

        // AUTHZ-GRANT-003: no grant naming the record is left live for a later
        // registration of the same identifier to inherit.
        foreach (Grant named in await grants.NamingForUpdateAsync(resource, now, cancellationToken).ConfigureAwait(false))
        {
            named.Revoke(default, now, Removal)
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));

            await grants.RecordAsync(named, cancellationToken).ConfigureAwait(false);
            await audit.RevokedAsync(named, cancellationToken).ConfigureAwait(false);
        }

        await resources.RemoveAsync(resource, cancellationToken).ConfigureAwait(false);

        if ((await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notCommitted)
        {
            return Result.Failure(notCommitted);
        }

        return Result.Success();
    }

    // X5, D-166: a type outside the vocabulary the model fixes at startup is a word the
    // request cannot be read with.
    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    // X5, D-166: a registration read and understood whose meaning cannot be carried out.
    private static Error Invalid(string member) =>
        Error.From(ErrorCodes.RequestInvalid, "member", JsonSerializer.SerializeToElement(member));

    private Error? Refused(
        ResourceRegistration registration,
        Dictionary<ResourceReference, OrganizationId> held,
        Dictionary<ResourceReference, OrganizationId> placed,
        IReadOnlySet<SubjectId> standing)
    {
        if (model.Find(registration.Resource.Type) is not ResourceTypeDeclaration declared)
        {
            return Malformed("resourceType");
        }

        if (placed.ContainsKey(registration.Resource) || held.ContainsKey(registration.Resource))
        {
            return Invalid("resourceId");
        }

        // AUTHZ-SCOPE-001: a tree begins and ends in one organization.
        if (!Placeable(registration.Resource.Type, registration.ContainedIn)
            || (registration.ContainedIn is ResourceReference container
                && !((placed.TryGetValue(container, out OrganizationId holding)
                        || held.TryGetValue(container, out holding))
                    && holding == registration.Organization)))
        {
            return Invalid("containedIn");
        }

        // IDN-LIFE-002a AC1: data of a sensitive type about nobody, or about somebody
        // with no account that stands, would have no route by which a right is
        // exercised, so it is not registered.
        if (declared.SensitiveCategories.Count > 0
            && (registration.Subject is not SubjectId subject || !standing.Contains(subject)))
        {
            return Invalid("subject");
        }

        return null;
    }

    // AUTHZ-INHERIT-002 AC5: a record the library holds no registration for is no record
    // to remove, and one that holds a registered record would leave that record inside
    // nothing.
    private async ValueTask<Error?> RemovalRefusedAsync(
        ResourceReference resource,
        CancellationToken cancellationToken)
    {
        if (await resources.FindAsync(resource, cancellationToken).ConfigureAwait(false) is null)
        {
            return Invalid("resource");
        }

        return await resources.HoldsAnyAsync(resource, cancellationToken).ConfigureAwait(false)
            ? Error.From(ErrorCodes.ResourceInUse)
            : null;
    }

    // AUTHZ-INHERIT-002 (D-203): a container registered before the batch is read again
    // with each organization's tree held, in one order whatever the batch, so none is
    // one a removal took away after it was judged.
    private async ValueTask<bool> ContainersRemovedAsync(
        IReadOnlyList<ResourceRegistration> registrations,
        CancellationToken cancellationToken)
    {
        foreach (OrganizationId organization in registrations
            .Select(registration => registration.Organization)
            .Distinct()
            .OrderBy(organization => organization.Value))
        {
            await resources.HoldAsync(organization, cancellationToken).ConfigureAwait(false);
        }

        var batch = new HashSet<ResourceReference>(registrations.Select(registration => registration.Resource));
        ResourceReference[] outside =
        [
            .. registrations
                .Select(registration => registration.ContainedIn)
                .OfType<ResourceReference>()
                .Where(container => !batch.Contains(container))
                .Distinct(),
        ];

        Dictionary<ResourceReference, OrganizationId> standing =
            await HeldAsync(outside, cancellationToken).ConfigureAwait(false);

        return outside.Any(container => !standing.ContainsKey(container));
    }

    private async ValueTask<Dictionary<ResourceReference, OrganizationId>> HeldAsync(
        IReadOnlyList<ResourceReference> asked,
        CancellationToken cancellationToken)
    {
        var held = new Dictionary<ResourceReference, OrganizationId>();

        foreach (IGrouping<ResourceType, ResourceReference> type in asked
            .Distinct()
            .Where(reference => model.Find(reference.Type) is not null)
            .GroupBy(reference => reference.Type))
        {
            foreach (RegisteredResource found in await resources
                .FindManyAsync(type.Key, [.. type.Select(reference => reference.Id)], cancellationToken)
                .ConfigureAwait(false))
            {
                held.Add(found.Reference, found.Organization);
            }
        }

        return held;
    }

    // AUTHZ-MODEL-003: containment is declared per type, so a record sits in a
    // container of the declared type, and outside one only where its type belongs to
    // the organization directly.
    private bool Placeable(ResourceType type, ResourceReference? containedIn)
    {
        ResourceTypeDeclaration declared = model.Find(type)
            ?? throw new InvalidOperationException("Only a declared type is placed.");

        return containedIn is ResourceReference container
            ? declared.ContainedIn == container.Type
            : declared.ContainedIn is null || declared.BelongsToOrganization;
    }
}
