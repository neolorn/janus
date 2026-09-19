using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The library's own normalization against the conformance file of the Unicode
/// Character Database it was generated from.
/// </summary>
/// <remarks>
/// Covers IDN-ACCT-004. The canonical form is only worth pinning if it is right, and
/// the only authority on that is the test file the standard ships. Every line gives one
/// string in its five forms; the canonical form of all five is one value, and the
/// Nickname profile, whose normalization rule is Form KC, turns the first into the
/// fourth wherever the profile's other rule, the one about spaces, has nothing to do.
/// </remarks>
[Trait("kind", "unit")]
public sealed class UnicodeConformanceTests
{
    /// <summary>
    /// The canonical form removes the differences between normalization forms, so the
    /// five columns of a conformance line share one canonical form.
    /// </summary>
    [Fact]
    public void TheCanonicalFormIsTheSameForEveryNormalizationFormOfAString()
    {
        var differing = new List<string>();

        foreach (string[] forms in Lines())
        {
            string canonical = CanonicalForm.Of(forms[0]);

            for (int column = 1; column < 5; column++)
            {
                if (!string.Equals(CanonicalForm.Of(forms[column]), canonical, StringComparison.Ordinal))
                {
                    differing.Add(Describe(forms[0]) + " column " + column.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        Assert.Empty(differing);
    }

    /// <summary>
    /// The Nickname profile normalizes to Form KC (RFC 8266 section 2.1), so enforcing
    /// it on a conformance line's source yields that line's Form KC column.
    /// </summary>
    /// <remarks>
    /// The profile also maps spaces, strips the ones at either end and collapses the
    /// runs inside, and that rule runs before normalization and again after it. Lines
    /// whose Form KC column that rule would itself change are therefore not lines on
    /// which the two forms can agree, and the profile's own treatment of spaces is
    /// tested against the vectors of RFC 8266 instead.
    /// </remarks>
    [Fact]
    public void TheNicknameProfileNormalizesToTheConformanceFormKc()
    {
        var differing = new List<string>();

        foreach (string[] forms in Lines())
        {
            if (!Precis.TryEnforceNickname(forms[3], out string settled)
                || !string.Equals(settled, forms[3], StringComparison.Ordinal))
            {
                continue;
            }

            if (!Precis.TryEnforceNickname(forms[0], out string enforced))
            {
                differing.Add(Describe(forms[0]) + " was refused");

                continue;
            }

            if (!string.Equals(enforced, forms[3], StringComparison.Ordinal))
            {
                differing.Add(Describe(forms[0]) + " gave " + Describe(enforced));
            }
        }

        Assert.Empty(differing);
    }

    private static IEnumerable<string[]> Lines()
    {
        string path = Path.Combine(
            Repository.Root,
            "tools",
            "Janus.UnicodeTables",
            "ucd",
            "NormalizationTest.txt");

        foreach (string line in File.ReadLines(path))
        {
            int comment = line.IndexOf('#', StringComparison.Ordinal);
            string data = comment < 0 ? line : line[..comment];

            if (data.Length == 0 || data[0] == '@')
            {
                continue;
            }

            string[] fields = data.Split(';', StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length < 5)
            {
                continue;
            }

            string[] forms = new string[5];

            for (int column = 0; column < 5; column++)
            {
                forms[column] = Read(fields[column]);
            }

            yield return forms;
        }
    }

    private static string Read(string field)
    {
        var text = new StringBuilder();

        foreach (string code in field.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            text.Append(char.ConvertFromUtf32(int.Parse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        }

        return text.ToString();
    }

    private static string Describe(string value)
    {
        var text = new StringBuilder();

        foreach (Rune rune in value.EnumerateRunes())
        {
            text.Append(rune.Value.ToString("X4", CultureInfo.InvariantCulture)).Append(' ');
        }

        return text.ToString().TrimEnd();
    }
}
