using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's own rows of one of its kinds of thing, as the truth table writes
/// and reads them, through the host's own context.
/// </summary>
/// <typeparam name="TResource">The kind of thing.</typeparam>
/// <param name="context">The host's context the rows are written and read through.</param>
/// <param name="type">The name the host declared the kind of thing under.</param>
/// <param name="identifier">The field of a row holding its identifier.</param>
internal sealed class SampleRows<TResource>(
    SampleContext context,
    ResourceType type,
    Expression<Func<TResource, string>> identifier) : IConformanceRows<TResource>
    where TResource : class
{
    /// <inheritdoc/>
    public ResourceType Type => type;

    /// <inheritdoc/>
    public IQueryable<TResource> Rows => context.Set<TResource>();

    /// <inheritdoc/>
    public FilterSources<TResource> Sources { get; } =
        new FilterSources<TResource>(context.Ancestry, context.Grants, identifier)
            .Relationship(SampleHost.Keeper, context.Keepers)
            .Relationship(SampleHost.Steward, context.Stewards);

    /// <inheritdoc/>
    public async ValueTask WriteAsync(
        ResourceReference record,
        OrganizationId organization,
        ResourceReference? containedIn,
        CancellationToken cancellationToken)
    {
        string id = record.Id.ToString();

        if (record.Type == SampleHost.ShelfType)
        {
            context.Shelves.Add(new Shelf { Id = id, Organization = organization.Value });
        }
        else if (record.Type == SampleHost.BinderType)
        {
            context.Binders.Add(new Binder { Id = id, ShelfId = Container(containedIn) });
        }
        else if (record.Type == SampleHost.SheetType)
        {
            context.Sheets.Add(new Sheet { Id = id, BinderId = Container(containedIn) });
        }
        else
        {
            throw new ArgumentException("The sample host holds no such kind of thing.", nameof(record));
        }

        _ = await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask RelateAsync(
        string relationship,
        ResourceReference record,
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        string id = record.Id.ToString();

        switch (relationship)
        {
            case SampleHost.Keeper:
                context.Keepers.Add(new ShelfKeeper { ShelfId = id, Keeper = subject });
                break;

            case SampleHost.Steward:
                context.Stewards.Add(new BinderSteward { BinderId = id, Steward = subject });
                break;

            default:
                throw new ArgumentException("The sample host declares no such relationship.", nameof(relationship));
        }

        _ = await context.SaveChangesAsync(cancellationToken);
    }

    private static string Container(ResourceReference? containedIn) =>
        containedIn?.Id.ToString()
            ?? throw new ArgumentException("The sample host keeps nothing of this kind outside a container.", nameof(containedIn));
}
