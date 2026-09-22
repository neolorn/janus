using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// The audit trail, keeping what the area wrote so a test can read it back.
/// </summary>
internal sealed class PrivacyAuditInMemory : IPrivacyAudit
{
    private readonly List<PrivacyAuditEntry> _entries = [];

    /// <summary>
    /// What was recorded, in the order it was.
    /// </summary>
    public IReadOnlyList<PrivacyAuditEntry> Entries => _entries;

    /// <inheritdoc/>
    public ValueTask RecordedAsync(
        AuditAction action,
        SubjectId? acting,
        SubjectId? subject,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken)
    {
        _entries.Add(new PrivacyAuditEntry(action, acting, subject, at, details));

        return ValueTask.CompletedTask;
    }
}
