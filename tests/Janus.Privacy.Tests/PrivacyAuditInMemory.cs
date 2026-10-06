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
        string? breakGlassReason,
        SubjectId? subject,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken)
    {
        _entries.Add(new PrivacyAuditEntry(action, acting, subject, at, details) { BreakGlassReason = breakGlassReason });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordedAsync(
        AuditAction action,
        SystemPrincipal principal,
        SubjectId? subject,
        OrganizationId? organization,
        DateTimeOffset at,
        IReadOnlyDictionary<string, JsonElement> details,
        CancellationToken cancellationToken)
    {
        _entries.Add(new PrivacyAuditEntry(action, Acting: null, subject, at, details, principal)
        {
            Organization = organization,
        });

        return ValueTask.CompletedTask;
    }
}
