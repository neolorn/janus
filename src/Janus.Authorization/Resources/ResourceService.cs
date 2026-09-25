using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Janus.Core;

namespace Janus.Authorization.Resources;

/// <summary>
/// The host's records, placed where the declaration says their types sit.
/// </summary>
/// <param name="model">What the host declared about its domain.</param>
/// <param name="resources">Where the records and their ancestry are written.</param>
/// <param name="work">The one transaction an operation runs in.</param>
/// <remarks>
/// Implements AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-SCOPE-001 and LIB-HOST-002.
/// A record is placed only in a container of the type its own type is declared
/// contained in, and only in one of its own organization, so no record inherits
/// across a boundary the declaration or the organization draws.
/// </remarks>
internal sealed class ResourceService(
    AuthorizationModel model,
    IResourceStore resources,
    IUnitOfWork work) : IResources
{
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

        // A container registered earlier in the same batch holds its contents as one
        // already registered would.
        var placed = new Dictionary<ResourceReference, OrganizationId>();
        var registered = new List<RegisteredResource>(registrations.Count);

        foreach (ResourceRegistration registration in registrations)
        {
            if (Refused(registration, held, placed) is Error refused)
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

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await resources.RegisterManyAsync(registered, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async ValueTask<Result> MoveAsync(
        ResourceReference resource,
        ResourceReference? containedIn,
        CancellationToken cancellationToken)
    {
        if (model.Find(resource.Type) is null
            || await resources.FindAsync(resource, cancellationToken).ConfigureAwait(false)
                is not RegisteredResource moving)
        {
            return Result.Failure(Malformed("resourceId"));
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
            return Result.Failure(Malformed("containedIn"));
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await resources.MoveAsync(resource, containedIn, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static Error Malformed(string member) =>
        Error.From(ErrorCodes.RequestMalformed, "member", JsonSerializer.SerializeToElement(member));

    private Error? Refused(
        ResourceRegistration registration,
        Dictionary<ResourceReference, OrganizationId> held,
        Dictionary<ResourceReference, OrganizationId> placed)
    {
        if (model.Find(registration.Resource.Type) is null)
        {
            return Malformed("resourceType");
        }

        if (placed.ContainsKey(registration.Resource) || held.ContainsKey(registration.Resource))
        {
            return Malformed("resourceId");
        }

        // AUTHZ-SCOPE-001: a tree begins and ends in one organization.
        if (!Placeable(registration.Resource.Type, registration.ContainedIn)
            || (registration.ContainedIn is ResourceReference container
                && !((placed.TryGetValue(container, out OrganizationId holding)
                        || held.TryGetValue(container, out holding))
                    && holding == registration.Organization)))
        {
            return Malformed("containedIn");
        }

        return null;
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
