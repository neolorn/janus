using System;
using Janus.Core.Unicode;

namespace Janus.Core;

/// <summary>
/// Mixed-script detection over an identifier, word by word.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-005. Single script is as UTS #39 section 5.1 defines it, over
/// <c>Script_Extensions</c>: characters of Common or Inherited script, which is every
/// digit, every combining mark and every piece of punctuation, are ignored when
/// deciding, so a digit or an apostrophe is never a foreign script. Whole-word Arabic
/// and whole-word Latin are both accepted; a word holding both is not.
/// </remarks>
public static class ScriptMixing
{
    /// <summary>
    /// Whether no word of a value mixes scripts.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether every word is single-script.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool IsSingleScriptPerWord(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Scripts.IsSingleScriptPerWord(value);
    }
}
