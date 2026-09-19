using System;
using System.Collections.Generic;

namespace Janus.Core.Unicode;

/// <summary>
/// The default full lowercase operation of the Unicode Standard, section 3.13.
/// </summary>
/// <remarks>
/// This is the case mapping rule the two PRECIS profiles take (RFC 8265 section 3.3.1
/// and RFC 8266 section 2.1), which is <c>toLowerCase</c> and not case folding: the
/// framework says so in RFC 8264 section 5.2.3, because folding surprises a reader who
/// only expected capitals to become small letters. The language-sensitive mappings are
/// not applied, the default operation being language-independent; the one condition
/// that is not, the final form of sigma, is.
/// </remarks>
internal static class CaseMapper
{
    private const int CapitalSigma = 0x03A3;
    private const int FinalSigma = 0x03C2;

    /// <summary>
    /// Lowercases a sequence of code points.
    /// </summary>
    /// <param name="text">The code points.</param>
    /// <returns>The lowercased code points.</returns>
    internal static List<int> ToLower(List<int> text)
    {
        var lowered = new List<int>(text.Count + 2);

        for (int index = 0; index < text.Count; index++)
        {
            int code = text[index];

            if (code == CapitalSigma && IsFinal(text, index))
            {
                lowered.Add(FinalSigma);

                continue;
            }

            if (Mappings.TryFind(
                CaseTables.LowercaseKeys,
                CaseTables.LowercaseOffsets,
                CaseTables.LowercaseData,
                code,
                out ReadOnlySpan<int> mapping))
            {
                foreach (int part in mapping)
                {
                    lowered.Add(part);
                }

                continue;
            }

            lowered.Add(code);
        }

        return lowered;
    }

    // Final_Sigma: a cased letter stands before, possibly behind case-ignorable code
    // points, and no cased letter stands after in the same way.
    private static bool IsFinal(List<int> text, int index) =>
        Cased(text, index - 1, -1) && !Cased(text, index + 1, 1);

    private static bool Cased(List<int> text, int from, int step)
    {
        for (int index = from; index >= 0 && index < text.Count; index += step)
        {
            if (IsCaseIgnorable(text[index]))
            {
                continue;
            }

            return IsCased(text[index]);
        }

        return false;
    }

    private static bool IsCased(int code) =>
        Ranges.Value(CaseTables.CasedStarts, CaseTables.CasedValues, code) != 0;

    private static bool IsCaseIgnorable(int code) =>
        Ranges.Value(CaseTables.CaseIgnorableStarts, CaseTables.CaseIgnorableValues, code) != 0;
}
