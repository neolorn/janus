using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// The host's own rows of one resource type, as the truth table writes and reads them.
/// </summary>
/// <typeparam name="TResource">The host's type of the records asked about.</typeparam>
/// <remarks>
/// Implements AUTHZ-TEST-001 and LIB-HOST-002. The library reads nothing of the
/// host's, so the host writes its own rows for the records a case needs, writes the
/// facts its derivations follow from, and hands over, from its own context, the rows
/// the filter is applied to.
/// </remarks>
public interface IConformanceRows<TResource>
    where TResource : class
{
    /// <summary>
    /// The resource type the records asked about are of.
    /// </summary>
    ResourceType Type { get; }

    /// <summary>
    /// The host's rows of the type, read through its own context, which the list
    /// filter is applied to.
    /// </summary>
    IQueryable<TResource> Rows { get; }

    /// <summary>
    /// The sets the filter is composed over, from the same context.
    /// </summary>
    FilterSources<TResource> Sources { get; }

    /// <summary>
    /// Writes the host's own row for a record a case needs, of this type or of a type
    /// containing it. The suite registers the record once this returns.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="organization">The organization owning it.</param>
    /// <param name="containedIn">What contains it, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask WriteAsync(
        ResourceReference record,
        OrganizationId organization,
        ResourceReference? containedIn,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes the host's own fact that a relationship holds between a subject and a
    /// record, which a derivation confers a role from.
    /// </summary>
    /// <param name="relationship">The relationship's declared name.</param>
    /// <param name="record">The record the fact is about.</param>
    /// <param name="subject">Who the fact is about.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask RelateAsync(
        string relationship,
        ResourceReference record,
        SubjectId subject,
        CancellationToken cancellationToken);
}
