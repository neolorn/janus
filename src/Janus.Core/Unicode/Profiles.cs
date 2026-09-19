using System;
using System.Collections.Generic;

namespace Janus.Core.Unicode;

/// <summary>
/// The two PRECIS profiles the identifier rules name: UsernameCaseMapped for a username
/// (RFC 8265 section 3.3) and Nickname for a display name (RFC 8266 section 2).
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004 and REG-PROF-001. Applying a profile is not idempotent for
/// every code point, so RFC 8264 section 7 has the rules applied again until the result
/// stops changing and the string refused if it has not stopped after three further
/// passes.
/// </remarks>
internal static class Profiles
{
    private const int Space = 0x0020;
    private const int FurtherPasses = 3;

    /// <summary>
    /// Enforces the UsernameCaseMapped profile.
    /// </summary>
    /// <param name="value">The username as it was entered.</param>
    /// <param name="enforced">The username in the profile's form.</param>
    /// <returns>Whether the username conforms.</returns>
    internal static bool TryUsername(string value, out string enforced)
    {
        enforced = string.Empty;

        // Preparation, RFC 8265 section 3.3.2: the width mapping comes first, because
        // the HasCompat category would otherwise refuse every fullwidth code point.
        List<int> codes = MapWidth(Text.Read(value));

        if (!StringClass.IsIdentifier(codes))
        {
            return false;
        }

        // Enforcement, RFC 8265 section 3.3.3.
        if (!Settle(codes, username: true, out List<int> settled))
        {
            return false;
        }

        if (BidiRule.Applies(settled) && !BidiRule.IsSatisfied(settled))
        {
            return false;
        }

        if (settled.Count == 0)
        {
            return false;
        }

        enforced = Text.Write(settled);

        return true;
    }

    /// <summary>
    /// Enforces the Nickname profile. The case mapping rule is not applied: RFC 8266
    /// section 2.3 leaves it to comparison, and the comparison key of a display name is
    /// the canonical form of IDN-ACCT-004 rather than this profile's.
    /// </summary>
    /// <param name="value">The display name as it was entered.</param>
    /// <param name="enforced">The display name in the profile's form.</param>
    /// <returns>Whether the display name conforms.</returns>
    internal static bool TryNickname(string value, out string enforced)
    {
        enforced = string.Empty;

        List<int> codes = Text.Read(value);

        if (!StringClass.IsFreeform(codes))
        {
            return false;
        }

        if (!Settle(codes, username: false, out List<int> settled) || settled.Count == 0)
        {
            return false;
        }

        enforced = Text.Write(settled);

        return true;
    }

    private static bool Settle(List<int> codes, bool username, out List<int> settled)
    {
        settled = codes;

        for (int pass = 0; pass <= FurtherPasses; pass++)
        {
            List<int> next = username
                ? Normalizer.Nfc(CaseMapper.ToLower(settled))
                : Normalizer.Nfkc(MapSpaces(settled));

            if (Same(next, settled))
            {
                return true;
            }

            settled = next;
        }

        return false;
    }

    private static List<int> MapWidth(List<int> codes)
    {
        var mapped = new List<int>(codes.Count);

        foreach (int code in codes)
        {
            if (Mappings.TryFind(
                NormalizationTables.WidthKeys,
                NormalizationTables.WidthOffsets,
                NormalizationTables.WidthData,
                code,
                out ReadOnlySpan<int> mapping))
            {
                foreach (int part in mapping)
                {
                    mapped.Add(part);
                }

                continue;
            }

            mapped.Add(code);
        }

        return mapped;
    }

    private static List<int> MapSpaces(List<int> codes)
    {
        // RFC 8266 section 2.1 rule 2: a space of any kind becomes an ASCII space, the
        // spaces at either end go, and a run inside becomes one.
        var mapped = new List<int>(codes.Count);

        foreach (int code in codes)
        {
            bool space = code == Space || StringClass.CategoryOf(code) == GeneralCategory.Zs;

            if (space && (mapped.Count == 0 || mapped[^1] == Space))
            {
                continue;
            }

            mapped.Add(space ? Space : code);
        }

        if (mapped.Count > 0 && mapped[^1] == Space)
        {
            mapped.RemoveAt(mapped.Count - 1);
        }

        return mapped;
    }

    private static bool Same(List<int> left, List<int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }
}
