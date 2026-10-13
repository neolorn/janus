using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The ASCII form of a domain, against the checks REG-DOM-001 names and the conformance
/// file of UTS #46 at the version the tables were generated from.
/// </summary>
/// <remarks>
/// Covers REG-DOM-001. The form comes from the library's own tables, so the answers
/// below are the same on every machine; the conformance file is the standard's own
/// statement of what they are.
/// </remarks>
[Trait("kind", "unit")]
public sealed class IdnaTests
{
    private const int SourceColumn = 0;
    private const int ToUnicodeColumn = 1;
    private const int ToUnicodeStatusColumn = 2;
    private const int ToAsciiColumn = 3;
    private const int ToAsciiStatusColumn = 4;
    private const int Columns = 7;
    private const string Empty = "\"\"";
    private const string NoError = "[]";

    /// <summary>
    /// REG-DOM-001 AC13: the conversion, given its input as it stands, refuses a label
    /// with hyphens in its third and fourth places, a label under the ACE prefix that
    /// decodes to ASCII alone, a label mixing a right-to-left letter with a
    /// left-to-right one, a label that opens with a digit before a right-to-left
    /// letter, and a zero width non-joiner between two letters that do not join.
    /// </summary>
    /// <param name="domain">The domain.</param>
    [Theory]
    [InlineData("ab--c.example")]
    [InlineData("xn--a.example")]
    [InlineData("אb.example")]
    [InlineData("1א.example")]
    [InlineData("a‌b.example")]
    public void REG_DOM_001_AC13_TheConversionRefusesWhatItsChecksRefuse(string domain)
    {
        bool read = CanonicalForm.TryDomainToAscii(domain, out string ascii);

        Assert.False(read);
        Assert.Equal(string.Empty, ascii);
    }

    /// <summary>
    /// REG-DOM-001 AC13: a label under the ACE prefix is answered in lower case, in
    /// whatever case it came.
    /// </summary>
    [Fact]
    public void REG_DOM_001_AC13_ALabelUnderTheAcePrefixReadsInLowerCase()
    {
        bool read = CanonicalForm.TryDomainToAscii("xn--bcher-KVA.example", out string ascii);

        Assert.True(read);
        Assert.Equal("xn--bcher-kva.example", ascii);
    }

    /// <summary>
    /// REG-DOM-001 AC13: every line of the conformance file is answered as its
    /// nontransitional ASCII column gives it, and a line whose status marks an error is
    /// refused.
    /// </summary>
    [Fact]
    public void REG_DOM_001_AC13_EveryLineOfTheConformanceFileIsAnsweredAsItsAsciiColumnGives()
    {
        var differing = new List<string>();
        int lines = 0;

        foreach ((int number, string source, string? expected) in Lines())
        {
            bool read = CanonicalForm.TryDomainToAscii(source, out string ascii);

            lines++;

            if (read != expected is not null || (read && !string.Equals(ascii, expected, StringComparison.Ordinal)))
            {
                differing.Add("line " + number.ToString(CultureInfo.InvariantCulture));
            }
        }

        Assert.Empty(differing);
        Assert.True(lines > 0);
    }

    private static IEnumerable<(int Number, string Source, string? Expected)> Lines()
    {
        string path = Path.Combine(
            Repository.Root,
            "tools",
            "Janus.UnicodeTables",
            "ucd",
            "IdnaTestV2.txt");
        int number = 0;

        foreach (string line in File.ReadLines(path))
        {
            int comment = line.IndexOf('#', StringComparison.Ordinal);
            string[] fields = (comment < 0 ? line : line[..comment]).Split(';');

            number++;

            if (fields.Length < Columns)
            {
                continue;
            }

            // A blank column stands for the one the file's header names: the ASCII form
            // for the Unicode form, that for the source, and the ASCII status for the
            // Unicode status.
            string source = Read(fields[SourceColumn]);
            string unicode = Or(fields[ToUnicodeColumn], source);
            string ascii = Or(fields[ToAsciiColumn], unicode);
            string status = fields[ToAsciiStatusColumn].Trim() is { Length: > 0 } stated
                ? stated
                : fields[ToUnicodeStatusColumn].Trim();

            yield return (number, source, status.Length == 0 || status == NoError ? ascii : null);
        }
    }

    private static string Or(string field, string blank) =>
        field.Trim().Length == 0 ? blank : Read(field);

    private static string Read(string field)
    {
        string value = field.Trim();

        if (value == Empty)
        {
            return string.Empty;
        }

        var text = new StringBuilder(value.Length);

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\')
            {
                text.Append(value[index]);

                continue;
            }

            // The file escapes a code point as \uXXXX, a UTF-16 code unit, or as
            // \x{X...}, a code point of any length.
            int digits = value[index + 1] == 'u' ? index + 2 : index + 3;
            int end = value[index + 1] == 'u' ? digits + 4 : value.IndexOf('}', digits);
            int code = int.Parse(value[digits..end], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (code <= char.MaxValue)
            {
                text.Append((char)code);
            }
            else
            {
                text.Append(char.ConvertFromUtf32(code));
            }

            index = value[index + 1] == 'u' ? end - 1 : end;
        }

        return text.ToString();
    }
}
