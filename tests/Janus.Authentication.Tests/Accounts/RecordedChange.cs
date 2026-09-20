using System;
using Janus.Core;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// One entry of the account's own trail.
/// </summary>
/// <param name="Action">What changed.</param>
/// <param name="Acting">Who made the change.</param>
/// <param name="Subject">Whose account it was made on.</param>
/// <param name="At">When.</param>
internal sealed record RecordedChange(
    AuditAction Action,
    SubjectId Acting,
    SubjectId Subject,
    DateTimeOffset At);
