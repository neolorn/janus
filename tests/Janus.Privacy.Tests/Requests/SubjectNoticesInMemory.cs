using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Requests;

namespace Janus.Privacy.Tests.Requests;

/// <summary>
/// Where a message to a subject goes, kept so a test can read what was sent, and
/// refusing it on every channel where a test asks it to.
/// </summary>
internal sealed class SubjectNoticesInMemory : ISubjectNotices
{
    private readonly List<(SubjectId Subject, MessageKind Message)> _told = [];

    /// <summary>
    /// What was sent, in the order it was.
    /// </summary>
    public IReadOnlyList<(SubjectId Subject, MessageKind Message)> Told => _told;

    /// <summary>
    /// Whether every channel refuses the message, as a restriction would, so that none
    /// takes it.
    /// </summary>
    public bool Refuses { get; set; }

    /// <inheritdoc/>
    public ValueTask<int> TellAsync(
        SubjectId subject,
        MessageKind message,
        string? source,
        CancellationToken cancellationToken)
    {
        if (Refuses)
        {
            return ValueTask.FromResult(0);
        }

        _told.Add((subject, message));

        return ValueTask.FromResult(1);
    }
}
