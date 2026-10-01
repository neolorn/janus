using System;
using System.Collections.Frozen;

namespace Janus.Authentication.Sending;

/// <summary>
/// How much of one text message a piece of text uses. A message outside the default
/// alphabet is carried as UCS-2 and costs a second message one character past
/// seventy, which is why the budget is per language and not per template.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-005 and INT-SMS-003. The default alphabet is GSM 03.38 with
/// its extension table, whose members cost two units each. A text that carries a link
/// is budgeted at two segments of its alphabet, each segment giving up the header that
/// joins the two, and every other text at one (chapter 10 section 5.26).
/// </remarks>
internal static class MessageBudget
{
    /// <summary>
    /// What one message holds in the default alphabet.
    /// </summary>
    public const int DefaultAlphabet = 160;

    /// <summary>
    /// What one message holds outside it.
    /// </summary>
    public const int WideAlphabet = 70;

    /// <summary>
    /// What two joined messages hold in the default alphabet.
    /// </summary>
    public const int LinkedDefaultAlphabet = 306;

    /// <summary>
    /// What two joined messages hold outside it.
    /// </summary>
    public const int LinkedWideAlphabet = 134;

    private static readonly FrozenSet<char> Basic = FrozenSet.ToFrozenSet(
        "@£$¥èéùìòÇ\nØø\rÅå"
        + "Δ_ΦΓΛΩΠΨΣΘΞÆæßÉ"
        + " !\"#¤%&'()*+,-./0123456789:;<=>?"
        + "¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§"
        + "¿abcdefghijklmnopqrstuvwxyzäöñüà");

    private static readonly FrozenSet<char> Extended =
        FrozenSet.ToFrozenSet("\f^{}\\[~]|€");

    /// <summary>
    /// What one message of this text holds.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <returns>The budget in units.</returns>
    /// <exception cref="ArgumentNullException">The text is absent.</exception>
    public static int Of(string text) => Of(text, linked: false);

    /// <summary>
    /// What this text is budgeted at, carrying a link or not.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="linked">Whether its template carries a link.</param>
    /// <returns>The budget in units.</returns>
    /// <exception cref="ArgumentNullException">The text is absent.</exception>
    public static int Of(string text, bool linked)
    {
        ArgumentNullException.ThrowIfNull(text);

        return InDefaultAlphabet(text)
            ? linked ? LinkedDefaultAlphabet : DefaultAlphabet
            : linked ? LinkedWideAlphabet : WideAlphabet;
    }

    /// <summary>
    /// What this text costs against its budget.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <returns>The units it uses.</returns>
    /// <exception cref="ArgumentNullException">The text is absent.</exception>
    public static int Units(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!InDefaultAlphabet(text))
        {
            return text.Length;
        }

        int units = 0;

        foreach (char character in text)
        {
            units += Extended.Contains(character) ? 2 : 1;
        }

        return units;
    }

    /// <summary>
    /// Whether this text costs a second message.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <returns>Whether it is over budget.</returns>
    /// <exception cref="ArgumentNullException">The text is absent.</exception>
    public static bool Exceeds(string text) => Exceeds(text, linked: false);

    /// <summary>
    /// Whether this text costs more than its budget, carrying a link or not.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="linked">Whether its template carries a link.</param>
    /// <returns>Whether it is over budget.</returns>
    /// <exception cref="ArgumentNullException">The text is absent.</exception>
    public static bool Exceeds(string text, bool linked) => Units(text) > Of(text, linked);

    private static bool InDefaultAlphabet(string text)
    {
        foreach (char character in text)
        {
            if (!Basic.Contains(character) && !Extended.Contains(character))
            {
                return false;
            }
        }

        return true;
    }
}
