using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Conformance.Tests;

/// <summary>
/// A host's rows of a type that a truth table refuses before it writes anything: every
/// member but the type fails the test that reaches it.
/// </summary>
/// <typeparam name="TResource">The host's type.</typeparam>
/// <param name="type">The type the table is over.</param>
/// <remarks>Implements CONV-TEST-007: a fake, written by hand, never a mock.</remarks>
internal sealed class UnwrittenRows<TResource>(ResourceType type) : IConformanceRows<TResource>
    where TResource : class
{
    /// <inheritdoc/>
    public ResourceType Type => type;

    /// <inheritdoc/>
    public IQueryable<TResource> Rows => throw new InvalidOperationException("A refused table reads no row.");

    /// <inheritdoc/>
    public FilterSources<TResource> Sources => throw new InvalidOperationException("A refused table filters nothing.");

    /// <inheritdoc/>
    public ValueTask WriteAsync(
        ResourceReference record,
        OrganizationId organization,
        ResourceReference? containedIn,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A refused table writes no row.");

    /// <inheritdoc/>
    public ValueTask RelateAsync(
        string relationship,
        ResourceReference record,
        SubjectId subject,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A refused table relates no row.");
}
