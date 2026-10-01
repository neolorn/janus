using System.Collections.Frozen;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// Which channels each message goes out on, which is what the catalogue has to hold
/// a template for in every configured language.
/// </summary>
/// <remarks>
/// Implements INT-SMS-001, AUTH-ABUSE-003, AUTH-ABUSE-005 and REG-MAIL-001. Three
/// messages are mail alone: the answer to a request made for an address no account
/// holds, which is sent to an address and never to a number (AUTH-ABUSE-003), an
/// invitation's link, which goes to the email the invitation binds, and the
/// <c>emailCode</c> sign-in code, which is not among the uses INT-SMS-001 admits.
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
        MessageKind.VerificationLink,
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
        MessageKind.SignInCode,
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
    /// A recovery link is among them: by text it reaches a number as a sign-in link
    /// does, so the carrier's signal is asked about it the same way (AUTH-FACT-002b).
    /// </summary>
    public static FrozenDictionary<MessageKind, bool> Factors { get; } = FrozenDictionary
        .ToFrozenDictionary<MessageKind, bool>(
        [
            new KeyValuePair<MessageKind, bool>(MessageKind.SignInLink, true),
            new KeyValuePair<MessageKind, bool>(MessageKind.SecondStepCode, false),
            new KeyValuePair<MessageKind, bool>(MessageKind.RecoveryLink, true),
        ]);

    /// <summary>
    /// The channels one message goes out on.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>Its channels.</returns>
    public static IReadOnlyList<SendKind> Of(MessageKind message) =>
        message is MessageKind.NoAccount or MessageKind.InvitationLink or MessageKind.SignInCode
            ? MailAlone
            : Both;
}
