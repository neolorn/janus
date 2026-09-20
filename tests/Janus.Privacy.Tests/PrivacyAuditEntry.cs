using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Privacy.Tests;

/// <summary>
/// One entry the area wrote to the audit trail.
/// </summary>
/// <param name="Action">What happened.</param>
/// <param name="Acting">Who did it.</param>
/// <param name="Subject">Whose account it was done on.</param>
/// <param name="At">When.</param>
/// <param name="Details">The structured context.</param>
internal sealed record PrivacyAuditEntry(
    AuditAction Action,
    SubjectId? Acting,
    SubjectId? Subject,
    DateTimeOffset At,
    IReadOnlyDictionary<string, JsonElement> Details);
