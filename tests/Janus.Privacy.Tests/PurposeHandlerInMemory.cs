using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// A host's handler for consent and objection changes, keeping what it was offered so a
/// test can read back what it was given and how often.
/// </summary>
/// <param name="name">What its confirmation is recorded under.</param>
/// <param name="purposes">The purposes it does the work for.</param>
internal sealed class PurposeHandlerInMemory(string name, params string[] purposes) : IPurposeHandler
{
    private readonly List<DomainEvent> _offered = [];

    /// <inheritdoc/>
    public string Name => name;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Purposes => purposes;

    /// <summary>
    /// Every change it was offered, in the order it was.
    /// </summary>
    public IReadOnlyList<DomainEvent> Offered => _offered;

    /// <summary>
    /// Whether it does its work when offered a change.
    /// </summary>
    public bool Confirms { get; set; } = true;

    /// <summary>
    /// Whether it faults instead of answering, as a handler over a database that is not
    /// there does.
    /// </summary>
    public bool Faults { get; set; }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The handler is set to fault.</exception>
    public ValueTask<Result> ConsentChangedAsync(ConsentChanged changed, CancellationToken cancellationToken) =>
        Handled(changed);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The handler is set to fault.</exception>
    public ValueTask<Result> ObjectionChangedAsync(ObjectionChanged changed, CancellationToken cancellationToken) =>
        Handled(changed);

    private ValueTask<Result> Handled(DomainEvent changed)
    {
        _offered.Add(changed);

        if (Faults)
        {
            throw new InvalidOperationException("The handler's own store answered nothing.");
        }

        return ValueTask.FromResult(Confirms
            ? Result.Success()
            : Result.Failure(Error.From(ErrorCodes.SystemFault)));
    }
}
