using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Exports;

namespace Janus.Privacy.Tests.Exports;

/// <summary>
/// The parts of an export the other areas hold, as a test arranges them.
/// </summary>
internal sealed class ExportSourceInMemory : IExportSource
{
    private readonly Dictionary<SubjectId, List<ExportSection>> _sections = [];

    private readonly Dictionary<SubjectId, List<ExportRecord>> _attached = [];

    /// <summary>
    /// How many times the sections were read.
    /// </summary>
    public int Reads { get; private set; }

    /// <summary>
    /// Gives a subject one section.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="section">The section.</param>
    public void Holds(SubjectId subject, ExportSection section)
    {
        if (!_sections.TryGetValue(subject, out List<ExportSection>? held))
        {
            held = [];
            _sections[subject] = held;
        }

        held.Add(section);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ExportSection>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        Reads++;

        return ValueTask.FromResult<IReadOnlyList<ExportSection>>(
            _sections.TryGetValue(subject, out List<ExportSection>? held) ? held : []);
    }

    /// <summary>
    /// Attaches a person record to an account, as its export names it.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <param name="record">How the record is named.</param>
    public void Attaches(SubjectId account, ExportRecord record)
    {
        if (!_attached.TryGetValue(account, out List<ExportRecord>? held))
        {
            held = [];
            _attached[account] = held;
        }

        held.Add(record);
    }

    /// <inheritdoc/>
    public ValueTask<ExportSection> RecordsAsync(
        SubjectId account,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(new ExportSection(
            "records",
            _attached.TryGetValue(account, out List<ExportRecord>? held) ? held : []));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ExportSection>> RecordSectionsAsync(
        SubjectId record,
        CancellationToken cancellationToken) =>
        SectionsAsync(record, cancellationToken);
}
