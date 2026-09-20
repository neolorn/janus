using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The audit trail of restriction changes, holding every field written so a test can
/// prove the plain key value is not among them.
/// </summary>
internal sealed class SendAuditInMemory : ISendAudit
{
    /// <summary>
    /// Every edit recorded, in order.
    /// </summary>
    public List<Edit> Edits { get; } = [];

    /// <summary>
    /// Every grant recorded, in order.
    /// </summary>
    public List<(string Restriction, int Credit, string Reason, SubjectId Actor)> Grants { get; } = [];

    /// <inheritdoc/>
    public ValueTask EditedAsync(
        string restriction,
        Restriction? before,
        Restriction? after,
        bool loosening,
        string? reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Edits.Add(new Edit(restriction, before, after, loosening, reason, actor));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask GrantedAsync(
        string restriction,
        int credit,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Grants.Add((restriction, credit, reason, actor));

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// One edit as it was recorded.
    /// </summary>
    /// <param name="Name">Which restriction.</param>
    /// <param name="Before">What it was.</param>
    /// <param name="After">What it became.</param>
    /// <param name="Loosening">Whether it lets more through than before.</param>
    /// <param name="Reason">The written reason.</param>
    /// <param name="Actor">Who made it.</param>
    internal sealed record Edit(
        string Name,
        Restriction? Before,
        Restriction? After,
        bool Loosening,
        string? Reason,
        SubjectId Actor);
}
