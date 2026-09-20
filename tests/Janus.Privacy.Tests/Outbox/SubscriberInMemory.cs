using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests.Outbox;

/// <summary>
/// A host's subject-event handler, keeping what it was offered so a test can read
/// back what it was given and how often.
/// </summary>
/// <param name="name">What its confirmation is recorded under.</param>
/// <param name="required">Whether a delivery waits for it.</param>
internal sealed class SubscriberInMemory(string name, bool required) : ISubjectEventSubscriber
{
    private readonly List<SubjectEvent> _offered = [];

    /// <inheritdoc/>
    public string Name => name;

    /// <inheritdoc/>
    public bool Required => required;

    /// <inheritdoc/>
    public IReadOnlyCollection<ResourceType> Covers { get; init; } = [];

    /// <summary>
    /// Every event it was offered, in the order it was.
    /// </summary>
    public IReadOnlyList<SubjectEvent> Offered => _offered;

    /// <summary>
    /// Whether it does its work when offered one.
    /// </summary>
    public bool Confirms { get; set; } = true;

    /// <summary>
    /// Whether it faults instead of answering, as a handler over a database that is
    /// not there does.
    /// </summary>
    public bool Faults { get; set; }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The handler is set to fault.</exception>
    public ValueTask<Result> HandleAsync(SubjectEvent raised, CancellationToken cancellationToken)
    {
        _offered.Add(raised);

        if (Faults)
        {
            throw new InvalidOperationException("The handler's own store answered nothing.");
        }

        return ValueTask.FromResult(Confirms
            ? Result.Success()
            : Result.Failure(Error.From(ErrorCodes.SystemFault)));
    }
}
