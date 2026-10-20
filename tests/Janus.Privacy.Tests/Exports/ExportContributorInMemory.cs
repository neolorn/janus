using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests.Exports;

/// <summary>
/// A host's export contributor, answering the sections a test gives it and keeping
/// whom it was asked about, so a test can read back when and how often it was asked.
/// </summary>
/// <param name="name">What the contributor is called.</param>
internal sealed class ExportContributorInMemory(string name) : IExportContributor
{
    private readonly List<SubjectId> _asked = [];

    /// <inheritdoc/>
    public string Name => name;

    /// <inheritdoc/>
    public IReadOnlyCollection<ResourceType> Covers { get; init; } = [];

    /// <summary>
    /// The sections it answers.
    /// </summary>
    public IReadOnlyList<ExportSection> Sections { get; set; } = [];

    /// <summary>
    /// The failure it answers instead of its sections, where a test sets one.
    /// </summary>
    public Error? Fails { get; set; }

    /// <summary>
    /// Whether it faults instead of answering, as a contributor over a database that is
    /// not there does.
    /// </summary>
    public bool Faults { get; set; }

    /// <summary>
    /// What a test does while the contributor is being asked.
    /// </summary>
    public Action? Meanwhile { get; set; }

    /// <summary>
    /// Every subject it was asked about, in the order it was.
    /// </summary>
    public IReadOnlyList<SubjectId> Asked => _asked;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The contributor is set to fault.</exception>
    public ValueTask<Result<IReadOnlyList<ExportSection>>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        _asked.Add(subject);
        Meanwhile?.Invoke();

        if (Faults)
        {
            throw new InvalidOperationException("The contributor's own store answered nothing.");
        }

        return ValueTask.FromResult(Fails is Error failure
            ? Result.Failure<IReadOnlyList<ExportSection>>(failure)
            : Result.Success(Sections));
    }
}
