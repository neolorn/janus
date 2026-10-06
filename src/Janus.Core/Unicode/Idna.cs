using System;
using System.Collections.Generic;
using System.Text;

namespace Janus.Core.Unicode;

/// <summary>
/// The ASCII form of a domain: the ToASCII operation of UTS #46 at the version the
/// tables were generated from.
/// </summary>
/// <remarks>
/// Implements REG-DOM-001. The processing is nontransitional, with UseSTD3ASCIIRules,
/// CheckHyphens, CheckBidi, CheckJoiners and VerifyDnsLength set and
/// IgnoreInvalidPunycode not. It reads the library's own tables and never the
/// machine's, so one domain has one ASCII form wherever the library runs. The mapping
/// lowers ASCII letters, so the form is in lower case.
/// </remarks>
internal static class Idna
{
    private const int FullStop = 0x002E;
    private const int Hyphen = 0x002D;
    private const int ZeroWidthNonJoiner = 0x200C;
    private const int ZeroWidthJoiner = 0x200D;
    private const int FirstNonAscii = 0x80;
    private const int MaximumLabelLength = 63;
    private const int MaximumNameLength = 253;
    private const string AcePrefix = "xn--";

    /// <summary>
    /// Converts a domain to its ASCII form.
    /// </summary>
    /// <param name="domain">The domain, as it stands.</param>
    /// <param name="ascii">Its ASCII form, or empty.</param>
    /// <returns>Whether the domain has an ASCII form under the checks above.</returns>
    internal static bool TryToAscii(string domain, out string ascii)
    {
        ascii = string.Empty;

        // UTS #46 section 4, steps 1 to 3: map, normalize, break. A disallowed code
        // point is left standing through the first two, because normalization may yet
        // compose it into one that is valid; the validity criteria judge what is left.
        List<List<int>> labels = Break(Normalizer.Nfc(Map(Text.Read(domain))));

        for (int index = 0; index < labels.Count; index++)
        {
            if (HasAcePrefix(labels[index]))
            {
                if (!TryDecode(labels[index], out List<int> decoded))
                {
                    return false;
                }

                labels[index] = decoded;
            }

            if (!IsValid(labels[index]))
            {
                return false;
            }
        }

        return SatisfiesBidi(labels) && TryWrite(labels, out ascii);
    }

    private static List<int> Map(List<int> text)
    {
        var mapped = new List<int>(text.Count);

        foreach (int code in text)
        {
            switch (StatusOf(code))
            {
                case IdnaStatus.Ignored:
                    break;
                case IdnaStatus.Mapped:
                    AddMapping(mapped, code);

                    break;
                default:
                    mapped.Add(code);

                    break;
            }
        }

        return mapped;
    }

    private static void AddMapping(List<int> mapped, int code)
    {
        if (!Mappings.TryFind(
            IdnaTables.MappedKeys,
            IdnaTables.MappedOffsets,
            IdnaTables.MappedData,
            code,
            out ReadOnlySpan<int> mapping))
        {
            throw new InvalidOperationException("The IDNA tables give a mapped code point no mapping.");
        }

        foreach (int part in mapping)
        {
            mapped.Add(part);
        }
    }

    private static List<List<int>> Break(List<int> text)
    {
        var labels = new List<List<int>> { new() };

        foreach (int code in text)
        {
            if (code == FullStop)
            {
                labels.Add([]);

                continue;
            }

            labels[^1].Add(code);
        }

        return labels;
    }

    private static bool HasAcePrefix(List<int> label)
    {
        if (label.Count < AcePrefix.Length)
        {
            return false;
        }

        for (int index = 0; index < AcePrefix.Length; index++)
        {
            if (label[index] != AcePrefix[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryDecode(List<int> label, out List<int> decoded)
    {
        decoded = [];

        // Section 4, step 4.1: a label under the prefix is Punycode throughout, decodes,
        // and decodes to something the prefix was needed for.
        return IsAscii(label)
            && Punycode.TryDecode(label.GetRange(AcePrefix.Length, label.Count - AcePrefix.Length), out decoded)
            && decoded.Count > 0
            && !IsAscii(decoded);
    }

    private static bool IsValid(List<int> label)
    {
        // Section 4.1, criteria 1 to 3 and 6. Criterion 4 is for CheckHyphens unset, and
        // criterion 5 cannot fail: a label was broken at every full stop, and a decoded
        // one gains only code points Punycode had to encode.
        if (!IsNormalized(label)
            || (label.Count > 3 && label[2] == Hyphen && label[3] == Hyphen)
            || (label.Count > 0 && (label[0] == Hyphen || label[^1] == Hyphen))
            || (label.Count > 0 && IsMark(label[0])))
        {
            return false;
        }

        for (int index = 0; index < label.Count; index++)
        {
            // Criteria 7 and 8.
            if (!IsAllowed(label[index])
                || (label[index] is ZeroWidthNonJoiner or ZeroWidthJoiner && !ContextualRules.Allows(label, index)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNormalized(List<int> label)
    {
        List<int> normalized = Normalizer.Nfc(label);

        if (normalized.Count != label.Count)
        {
            return false;
        }

        for (int index = 0; index < label.Count; index++)
        {
            if (normalized[index] != label[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowed(int code)
    {
        // The STD3 rules leave an ASCII label its lower-case letters, digits and hyphen;
        // an upper-case letter here came out of Punycode and was never mapped.
        if (code < FirstNonAscii)
        {
            return code is (>= 'a' and <= 'z') or (>= '0' and <= '9') or Hyphen;
        }

        return StatusOf(code) is IdnaStatus.Valid or IdnaStatus.Deviation;
    }

    private static bool SatisfiesBidi(List<List<int>> labels)
    {
        // Section 4.1, criterion 9: the Bidi Rule binds every label of a name once any
        // label of it holds a right-to-left code point (RFC 5893 section 1.4).
        if (!labels.Exists(BidiRule.Applies))
        {
            return true;
        }

        return labels.TrueForAll(BidiRule.IsSatisfied);
    }

    private static bool TryWrite(List<List<int>> labels, out string ascii)
    {
        var name = new StringBuilder();

        ascii = string.Empty;

        for (int index = 0; index < labels.Count; index++)
        {
            if (index > 0)
            {
                name.Append((char)FullStop);
            }

            int start = name.Length;

            if (IsAscii(labels[index]))
            {
                foreach (int code in labels[index])
                {
                    name.Append((char)code);
                }
            }
            else if (Punycode.TryEncode(labels[index], out string encoded))
            {
                name.Append(AcePrefix).Append(encoded);
            }
            else
            {
                return false;
            }

            // Section 4.2, step 4. No label is empty, the root's included, so a name
            // with a trailing full stop is refused.
            if (name.Length == start || name.Length - start > MaximumLabelLength)
            {
                return false;
            }
        }

        if (name.Length > MaximumNameLength)
        {
            return false;
        }

        ascii = name.ToString();

        return true;
    }

    private static bool IsAscii(List<int> label) => label.TrueForAll(code => code < FirstNonAscii);

    private static bool IsMark(int code) =>
        StringClass.CategoryOf(code) is GeneralCategory.Mn or GeneralCategory.Mc or GeneralCategory.Me;

    private static IdnaStatus StatusOf(int code) =>
        (IdnaStatus)Ranges.Value(IdnaTables.StatusStarts, IdnaTables.StatusValues, code);
}
