using System;

namespace Janus.Core;

/// <summary>
/// What an admin-assisted recovery link opened: the person it belongs to, how long it
/// lasts, and whether it may settle a new address on the new address alone.
/// </summary>
/// <param name="Id">Which session.</param>
/// <param name="Subject">Whose account it acts on.</param>
/// <param name="ExpiresAt">
/// When it ends, which is the remainder of the link's own lifetime and never longer.
/// </param>
/// <param name="MailboxLost">
/// Whether the approver recorded the account's mailbox as unreachable, which is what
/// lets a new address be confirmed by the new address alone (AUTH-RECOV-002,
/// REG-IDENT-007).
/// </param>
/// <remarks>
/// Implements AUTH-RECOV-002, D-147 and D-148. It carries no application access: it
/// reaches the credential endpoints of the account it belongs to and nothing else,
/// and completing the enrolment ends it.
/// </remarks>
public sealed record EnrolmentSession(
    EnrolmentSessionId Id,
    SubjectId Subject,
    DateTimeOffset ExpiresAt,
    bool MailboxLost);
