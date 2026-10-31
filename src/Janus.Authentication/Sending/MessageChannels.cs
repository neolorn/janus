using System.Collections.Frozen;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// Which messages are notices, which carry a factor, and which answer an ask before
/// they are attempted.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-003, AUTH-ABUSE-004, AUTH-FACT-016, REG-MAIL-001 and REC-ACC-001.
/// </remarks>
internal static class MessageChannels
{
    /// <summary>
    /// The messages that tell an account holder something happened to their account,
    /// and a person record's existing holder, its notice contacts, that its deletion
    /// began (REC-ACC-002). A send of one of these to an address the account or the
    /// record already holds is outside the destination restrictions, so an attacker who
    /// drains a bucket cannot silence the notice that says so.
    /// </summary>
    public static FrozenSet<MessageKind> Notices { get; } = FrozenSet.ToFrozenSet(
    [
        MessageKind.SecurityNotice,
        MessageKind.AccountExists,
        MessageKind.IdentifierAdded,
        MessageKind.IdentifierRemoved,
        MessageKind.IdentifierDetached,
        MessageKind.IdentifierSettingsChanged,
        MessageKind.CredentialEnrolled,
        MessageKind.DeactivationNotice,
        MessageKind.DeletionNotice,
        MessageKind.RecoveryCodesReminder,
        MessageKind.CredentialSuspended,
        MessageKind.OobDeletionNotice,
        MessageKind.CredentialUnbound,
        MessageKind.RecordDeletionNotice,
    ]);

    /// <summary>
    /// The messages that carry a factor rather than tell an account something, and
    /// whether each carries a link. Which entry one amounts to follows from that and
    /// the channel it goes out on (AUTH-FACT-016); no property tells a message that
    /// authenticates from one that verifies, so the one place that is written is here.
    /// A recovery link is among them: by text it reaches a number as a sign-in link
    /// does, so the carrier's signal is asked about it the same way (AUTH-FACT-002b).
    /// A record access code by text is a phone code, asked about as a sign-in's is
    /// (REC-ACC-001).
    /// </summary>
    public static FrozenDictionary<MessageKind, bool> Factors { get; } = FrozenDictionary
        .ToFrozenDictionary<MessageKind, bool>(
        [
            new KeyValuePair<MessageKind, bool>(MessageKind.SignInLink, true),
            new KeyValuePair<MessageKind, bool>(MessageKind.SecondStepCode, false),
            new KeyValuePair<MessageKind, bool>(MessageKind.RecoveryLink, true),
            new KeyValuePair<MessageKind, bool>(MessageKind.RecordAccessCode, false),
        ]);

    /// <summary>
    /// The messages that answer an ask of a sign-in link, an email code, a recovery or
    /// a record access code, the notices to an address nothing is held for among them.
    /// The ask is answered before the host is handed anything, whether its message is
    /// sent or not, so none of these has an attempt inside the request: the outbox
    /// publisher carries each (AUTH-ABUSE-003, AUTH-ABUSE-004, REC-ACC-001).
    /// </summary>
    public static FrozenSet<MessageKind> AnsweredFirst { get; } = FrozenSet.ToFrozenSet(
    [
        MessageKind.SignInLink,
        MessageKind.SignInCode,
        MessageKind.RecoveryLink,
        MessageKind.NoAccount,
        MessageKind.RecordAccessCode,
        MessageKind.RecordAccessNone,
    ]);
}
