using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Authentication;
using Janus.Core;

namespace Janus.Hosting.Sending;

/// <summary>
/// The catalogue the library ships, holding one text for every message it sends, on
/// every channel that message goes out on, in the languages the library carries. A
/// deployment that registers a catalogue of its own keeps it; this one is never
/// consulted then.
/// </summary>
/// <remarks>
/// Implements LIB-EXT-001, CONV-CONTENT-001 and AUTH-ABUSE-005. These are defaults, not
/// the library's opinion of what a deployment should say: a host replaces the catalogue
/// with its own words and its own languages, and a language this one does not carry is
/// the deployment's to supply or startup refuses it.
/// </remarks>
internal sealed class DefaultMessageTemplates : IMessageTemplates
{
    /// <summary>
    /// The languages the shipped texts are written in.
    /// </summary>
    public static IReadOnlyList<string> Languages { get; } = ["en", "ar"];

    // One entry per message the library sends: what it says by mail, and what it says
    // in one text message where it goes out by SMS at all. Every place a text names is
    // one the library fills, and every text is measured at startup with those places
    // at their widest, so a shipped text never costs a second message.
    private static readonly (MessageKind Message, Words English, Words Arabic)[] Shipped =
    [
        (
            MessageKind.VerificationCode,
            new Words(
                "Your verification code",
                "Your verification code is {code}. It expires shortly. If you did not ask for it, ignore this message.",
                "Your verification code is {code}."),
            new Words(
                "رمز التحقق",
                "رمز التحقق الخاص بك هو {code}. ينتهي بعد قليل. إن لم تطلبه فتجاهل هذه الرسالة.",
                "رمز التحقق هو {code}.")),
        (
            MessageKind.SignInLink,
            new Words(
                "Your sign-in link",
                "Use this to sign in: {token}. It expires shortly. If you did not ask for it, ignore this message.",
                "Sign in with this: {token}"),
            new Words(
                "رابط تسجيل الدخول",
                "استخدم هذا لتسجيل الدخول: {token}. ينتهي بعد قليل. إن لم تطلبه فتجاهل هذه الرسالة.",
                "لتسجيل الدخول: {token}")),
        (
            MessageKind.SecondStepCode,
            new Words(
                "Your sign-in code",
                "Your sign-in code is {code}. It expires shortly.",
                "Your sign-in code is {code}."),
            new Words(
                "رمز تسجيل الدخول",
                "رمز تسجيل الدخول هو {code}. ينتهي بعد قليل.",
                "رمز دخولك هو {code}.")),
        (
            MessageKind.SecurityNotice,
            new Words(
                "A change to your account",
                "Something on your account changed. If it was not you, sign in and secure your account.",
                "Something on your account changed. If it was not you, secure your account."),
            new Words(
                "تغيير في حسابك",
                "حدث تغيير في حسابك. إن لم يكن منك فسجل الدخول وأمن حسابك.",
                "حدث تغيير في حسابك. إن لم يكن منك فأمن حسابك.")),
        (
            MessageKind.EnrolmentLink,
            new Words(
                "Your enrolment link",
                "Use this to finish setting up your new way of signing in: {token}. It expires shortly.",
                "Finish setting up your sign-in method: {token}"),
            new Words(
                "رابط التسجيل",
                "استخدم هذا لإتمام إعداد وسيلة الدخول الجديدة: {token}. ينتهي بعد قليل.",
                "لإتمام الإعداد: {token}")),
        (
            MessageKind.Alert,
            new Words(
                "Alert: {condition}",
                "The condition {condition} was raised at {raisedAt}.",
                "Alert: {condition}"),
            new Words(
                "تنبيه: {condition}",
                "رُفع التنبيه {condition} في {raisedAt}.",
                "تنبيه: {condition}")),
        (
            MessageKind.NoAccount,
            new Words(
                "We could not find an account",
                "Someone asked to sign in with this address, but no account here uses it. If it was you, you can register instead.",
                null),
            new Words(
                "لا يوجد حساب بهذا العنوان",
                "طلب أحدهم تسجيل الدخول بهذا العنوان، ولا يوجد حساب يستخدمه هنا. إن كنت أنت فبإمكانك إنشاء حساب.",
                null)),
        (
            MessageKind.AccountExists,
            new Words(
                "This address is already on an account",
                "Someone tried to use this address on a new account. Your account is unchanged. If it was not you, there is nothing to do.",
                "Someone tried to use this address on a new account. Your account is unchanged."),
            new Words(
                "هذا العنوان مسجل بالفعل",
                "حاول أحدهم استخدام هذا العنوان في حساب جديد. لم يتغير حسابك. إن لم يكن منك فلا شيء عليك.",
                "حاول أحدهم استخدام هذا العنوان في حساب جديد. لم يتغير حسابك.")),
        (
            MessageKind.IdentifierAdded,
            new Words(
                "An address was added to your account",
                "An address or a number was added to your account. If it was not you, sign in and secure your account.",
                "An address or a number was added to your account."),
            new Words(
                "أُضيف عنوان إلى حسابك",
                "أُضيف عنوان أو رقم إلى حسابك. إن لم يكن منك فسجل الدخول وأمن حسابك.",
                "أُضيف عنوان أو رقم إلى حسابك.")),
        (
            MessageKind.IdentifierRemoved,
            new Words(
                "An address was removed from your account",
                "An address or a number was removed from your account. If it was not you, undo it with this: {token}",
                "An address or a number was removed. Undo it with this: {token}"),
            new Words(
                "أُزيل عنوان من حسابك",
                "أُزيل عنوان أو رقم من حسابك. إن لم يكن منك فتراجع عنه بهذا: {token}",
                "أُزيل عنوان. للتراجع: {token}")),
        (
            MessageKind.IdentifierDetached,
            new Words(
                "This address no longer reaches the account",
                "This address or number was removed from an account and no longer reaches it.",
                "This number was removed from an account and no longer reaches it."),
            new Words(
                "لم يعد هذا العنوان يصل إلى الحساب",
                "أُزيل هذا العنوان أو الرقم من حساب ولم يعد يصل إليه.",
                "أُزيل هذا الرقم من حساب ولم يعد يصل إليه.")),
        (
            MessageKind.IdentifierSettingsChanged,
            new Words(
                "The contact settings on your account changed",
                "Which address or number your account uses first, or keeps as a backup, changed. If it was not you, sign in and secure your account.",
                "The contact settings on your account changed."),
            new Words(
                "تغيرت إعدادات التواصل في حسابك",
                "تغير العنوان أو الرقم الذي يستخدمه حسابك أولا أو يحتفظ به احتياطيا. إن لم يكن منك فسجل الدخول وأمن حسابك.",
                "تغيرت إعدادات التواصل في حسابك.")),
        (
            MessageKind.CredentialEnrolled,
            new Words(
                "A new way of signing in was added",
                "A new way of signing in was added to your account. If it was not you, sign in and secure your account.",
                "A new way of signing in was added to your account."),
            new Words(
                "أُضيفت وسيلة دخول جديدة",
                "أُضيفت وسيلة جديدة لتسجيل الدخول إلى حسابك. إن لم يكن منك فسجل الدخول وأمن حسابك.",
                "أُضيفت وسيلة جديدة للدخول إلى حسابك.")),
        (
            MessageKind.IdentifierChangeConfirm,
            new Words(
                "Confirm the change to your account",
                "Confirm that this address or number may be replaced: {token}",
                "Confirm the change with this: {token}"),
            new Words(
                "أكد التغيير في حسابك",
                "أكد أن هذا العنوان أو الرقم يمكن استبداله: {token}",
                "أكد التغيير بهذا: {token}")),
        (
            MessageKind.RecoveryLink,
            new Words(
                "Your recovery link",
                "Use this to set a new password: {token}. It removes nothing else from your account.",
                "Set a new password with this: {token}"),
            new Words(
                "رابط الاستعادة",
                "استخدم هذا لتعيين كلمة مرور جديدة: {token}. لا يزيل شيئا آخر من حسابك.",
                "لكلمة مرور جديدة: {token}")),
        (
            MessageKind.PrivacyRequestReceived,
            new Words(
                "We received your request",
                "Your request reached the queue. It has not been decided yet, and you will be told when it is.",
                "Your request reached the queue. You will be told when it is decided."),
            new Words(
                "استلمنا طلبك",
                "وصل طلبك إلى قائمة الطلبات. لم يُبت فيه بعد، وسنخبرك عند البت فيه.",
                "وصل طلبك. سنخبرك عند البت فيه.")),
        (
            MessageKind.PrivacyRequestLapsed,
            new Words(
                "Your request passed its deadline",
                "Your request reached its deadline without a decision. It is still open, and you may ask again or complain to the regulator.",
                "Your request reached its deadline without a decision. It is still open."),
            new Words(
                "تجاوز طلبك موعده",
                "بلغ طلبك موعده النهائي دون بت. ما زال مفتوحا، ويمكنك السؤال مجددا أو الشكوى للجهة المختصة.",
                "بلغ طلبك موعده دون بت، وما زال مفتوحا.")),
        (
            MessageKind.DeactivationNotice,
            new Words(
                "Your account is deactivated",
                "Your account is deactivated. Stand it back up with this: {token}",
                "Your account is deactivated. Stand it back up: {token}"),
            new Words(
                "حسابك موقوف",
                "حسابك موقوف الآن. لإعادته كما كان استخدم هذا: {token}",
                "حسابك موقوف. لإعادته: {token}")),
        (
            MessageKind.DeletionNotice,
            new Words(
                "Your account is set to be deleted",
                "Your account will be deleted when its grace window ends. Cancel it with this: {token}",
                "Your account will be deleted soon. Cancel it: {token}"),
            new Words(
                "حسابك في طريقه للحذف",
                "سيُحذف حسابك بانتهاء مهلة السماح. لإلغاء ذلك استخدم هذا: {token}",
                "سيُحذف حسابك. للإلغاء: {token}")),
    ];

    private static readonly FrozenDictionary<Held, MessageTemplate> Texts = Written();

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The language is absent.</exception>
    public Result<MessageTemplate> Find(MessageKind message, SendKind kind, string language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return Texts.TryGetValue(new Held(message, kind, language), out MessageTemplate? template)
            ? Result.Success(template)
            : Result.Failure<MessageTemplate>(Error.From(
                ErrorCodes.StartupDeclarationMissing,
                "key",
                JsonSerializer.SerializeToElement(
                    WrittenName.Of(message) + "." + WrittenName.Of(kind) + "." + language)));
    }

    private static FrozenDictionary<Held, MessageTemplate> Written()
    {
        var texts = new Dictionary<Held, MessageTemplate>();

        foreach ((MessageKind message, Words english, Words arabic) in Shipped)
        {
            Take(texts, message, "en", english);
            Take(texts, message, "ar", arabic);
        }

        return FrozenDictionary.ToFrozenDictionary(texts);
    }

    private static void Take(
        Dictionary<Held, MessageTemplate> texts,
        MessageKind message,
        string language,
        Words words)
    {
        texts[new Held(message, SendKind.Email, language)] =
            new MessageTemplate(words.Subject, words.Mail);

        if (words.Text is string text)
        {
            texts[new Held(message, SendKind.Sms, language)] = new MessageTemplate(null, text);
        }
    }

    // What one message says on each channel in one language. A message that goes out on
    // mail alone carries no text (MessageChannels).
    private readonly record struct Words(string Subject, string Mail, string? Text);

    private readonly record struct Held(MessageKind Message, SendKind Kind, string Language);
}
