using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What the enrolment screen renders from: the username a password manager pairs a
/// new password with, whether a password is set, the credentials the session may set,
/// and, where the mailbox was recorded lost, the emails a replace names.
/// </summary>
/// <param name="Username">The account's primary email (FE-PM-002).</param>
/// <param name="Password">Whether a password is set, and when it was last set.</param>
/// <param name="Offers">
/// Each of <c>password</c>, <c>passkey</c>, <c>securityKey</c> and <c>totp</c> the policy
/// in force holds in its login factors.
/// </param>
/// <param name="MailboxLost">
/// Whether the approver recorded the account's mailbox as unreachable (AUTH-RECOV-002).
/// </param>
/// <param name="Identifiers">
/// The account's emails where the mailbox was recorded lost, and none otherwise.
/// </param>
/// <param name="ExpiresAt">When the enrolment session ends.</param>
/// <remarks>Implements AUTH-RECOV-002, FE-SEC-001, FE-PM-002 and D-224.</remarks>
public sealed record EnrolmentDocument(
    string Username,
    PasswordStatus Password,
    IReadOnlyList<Factor> Offers,
    bool MailboxLost,
    IReadOnlyList<IdentifierSummary> Identifiers,
    DateTimeOffset ExpiresAt);
