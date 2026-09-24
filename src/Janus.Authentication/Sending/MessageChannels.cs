using System.Collections.Frozen;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// Which channels each message goes out on, which is what the catalogue has to hold
/// a template for in every configured language.
/// </summary>
/// <remarks>
/// Implements INT-SMS-001, AUTH-ABUSE-003, AUTH-ABUSE-005 and REG-MAIL-001. Two
/// messages are mail alone: the answer to a request made for an address no account
/// holds, which is sent to an address and never to a number (AUTH-ABUSE-003), and an
/// invitation's link, which goes to the email the invitation binds.
/// </remarks>
internal static class MessageChannels
{
    private static readonly SendKind[] Both = [SendKind.Email, SendKind.Sms];

    private static readonly SendKind[] MailAlone = [SendKind.Email];

    /// <summary>
    /// Every message the library sends.
    /// </summary>
    public static IReadOnlyList<MessageKind> Messages { get; } =
    [
        MessageKind.VerificationCode,
        MessageKind.SignInLink,
        MessageKind.SecondStepCode,
        MessageKind.SecurityNotice,
        MessageKind.EnrolmentLink,
        MessageKind.Alert,
        MessageKind.NoAccount,
        MessageKind.AccountExists,
        MessageKind.IdentifierAdded,
        MessageKind.IdentifierRemoved,
        MessageKind.IdentifierDetached,
        MessageKind.IdentifierSettingsChanged,
        MessageKind.CredentialEnrolled,
        MessageKind.IdentifierChangeConfirm,
        MessageKind.RecoveryLink,
        MessageKind.PrivacyRequestReceived,
        MessageKind.PrivacyRequestLapsed,
        MessageKind.DeactivationNotice,
        MessageKind.DeletionNotice,
        MessageKind.InvitationLink,
        MessageKind.RecoveryCodesReminder,
    ];

    /// <summary>
    /// The messages that tell an account holder something happened to their account.
    /// A send of one of these to an address the account already holds is outside the
    /// destination restrictions, so an attacker who drains a bucket cannot silence
    /// the notice that says so.
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
    ]);

    /// <summary>
    /// The messages that carry a factor rather than tell an account something, and
    /// whether each carries a link. Which entry one amounts to follows from that and
    /// the channel it goes out on (AUTH-FACT-016); no property tells a message that
    /// authenticates from one that verifies, so the one place that is written is here.
    /// </summary>
    public static FrozenDictionary<MessageKind, bool> Factors { get; } = FrozenDictionary
        .ToFrozenDictionary<MessageKind, bool>(
        [
            new KeyValuePair<MessageKind, bool>(MessageKind.SignInLink, true),
            new KeyValuePair<MessageKind, bool>(MessageKind.SecondStepCode, false),
        ]);

    /// <summary>
    /// The channels one message goes out on.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>Its channels.</returns>
    public static IReadOnlyList<SendKind> Of(MessageKind message) =>
        message is MessageKind.NoAccount or MessageKind.InvitationLink ? MailAlone : Both;
}
