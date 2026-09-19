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
/// its extension table, whose members cost two units each.
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
    public static int Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return InDefaultAlphabet(text) ? DefaultAlphabet : WideAlphabet;
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
    public static bool Exceeds(string text) => Units(text) > Of(text);

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
