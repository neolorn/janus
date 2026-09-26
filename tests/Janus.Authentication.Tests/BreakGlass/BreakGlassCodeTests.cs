using System;
using System.Linq;
using System.Security.Cryptography;
using Janus.Authentication.BreakGlass;
using Xunit;

namespace Janus.Authentication.Tests.BreakGlass;

/// <summary>
/// The break-glass credential as it is printed and read back (OPS-BOOT-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class BreakGlassCodeTests : IDisposable
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <inheritdoc/>
    public void Dispose() => _randomness.Dispose();

    /// <summary>
    /// OPS-BOOT-004 AC6: the code carries at least 128 bits from a typeable alphabet,
    /// printed as 36 symbols in nine groups of four.
    /// </summary>
    [Fact]
    public void OPS_BOOT_004_AC6_TheCodeCarriesAtLeast128BitsFromATypeableAlphabet()
    {
        string code = BreakGlassCode.Draw(_randomness);
        string[] groups = code.Split('-');

        Assert.True(BreakGlassCode.DataSymbols * Math.Log2(Alphabet.Length) >= 128);
        Assert.Equal(9, groups.Length);
        Assert.All(groups, group => Assert.Equal(BreakGlassCode.GroupSize, group.Length));
        Assert.All(string.Concat(groups), symbol => Assert.Contains(symbol, Alphabet));
        Assert.DoesNotContain(code, symbol => symbol is 'I' or 'L' or 'O' or 'U');
    }

    /// <summary>
    /// OPS-BOOT-004 AC6: a code read back as it was printed holds every check, whatever
    /// the case it was typed in and with the confusable letters folded.
    /// </summary>
    [Fact]
    public void OPS_BOOT_004_AC6_ACodeTypedAsPrintedIsRead()
    {
        string code = BreakGlassCode.Draw(_randomness);
        string canonical = code.Replace("-", string.Empty, StringComparison.Ordinal);

        Assert.Equal(canonical, BreakGlassCode.Checked(code));
        Assert.Equal(canonical, BreakGlassCode.Checked(new string([.. code.Select(char.ToLowerInvariant)])));
        Assert.Equal(canonical, BreakGlassCode.Checked(code.Replace('-', ' ')));
        Assert.Equal(canonical, BreakGlassCode.Checked(canonical));
    }

    /// <summary>
    /// OPS-BOOT-004 AC6: each group's fourth symbol is the weighted sum of its three
    /// others modulo 32, and a confusable letter reads as the digit it resembles.
    /// </summary>
    [Fact]
    public void OPS_BOOT_004_AC6_TheCheckIsTheWeightedSumOfTheGroup()
    {
        // 1*1 + 2*2 + 3*3 = 14, which is E; 1*31 + 2*31 + 3*31 = 186, 26 modulo 32,
        // which is T.
        string code = string.Join('-', Enumerable.Repeat("123E", 8)) + "-ZZZT";

        Assert.NotNull(BreakGlassCode.Checked(code));
        Assert.NotNull(BreakGlassCode.Checked(code.Replace('1', 'l')));
        Assert.Equal(
            BreakGlassCode.Checked(code),
            BreakGlassCode.Checked(code.Replace('1', 'I')));
    }

    /// <summary>
    /// OPS-BOOT-004 AC6: a group with a wrong check symbol is refused, and so is a code
    /// with a symbol missing or one outside the alphabet.
    /// </summary>
    [Fact]
    public void OPS_BOOT_004_AC6_AGroupWithAWrongCheckIsRefused()
    {
        string code = BreakGlassCode.Draw(_randomness);
        char[] copied = code.ToCharArray();
        int check = code.IndexOf('-', StringComparison.Ordinal) - 1;

        copied[check] = Alphabet[(Alphabet.IndexOf(copied[check], StringComparison.Ordinal) + 1) % Alphabet.Length];

        Assert.Null(BreakGlassCode.Checked(new string(copied)));
        Assert.Null(BreakGlassCode.Checked(code[..^1]));
        Assert.Null(BreakGlassCode.Checked(code + "0"));
        Assert.Null(BreakGlassCode.Checked("U" + code[1..]));
        Assert.Null(BreakGlassCode.Checked(string.Empty));
    }

    /// <summary>
    /// OPS-BOOT-004 AC6: a symbol mistyped in the first or third place of a group is
    /// always caught, since the weights there are odd and every change moves the sum.
    /// </summary>
    [Fact]
    public void OPS_BOOT_004_AC6_ASymbolMistypedInTheFirstOrThirdPlaceIsCaught()
    {
        string code = BreakGlassCode.Draw(_randomness);

        foreach (int place in new[] { 0, 2 })
        {
            char[] copied = code.ToCharArray();
            int value = Alphabet.IndexOf(copied[place], StringComparison.Ordinal);

            for (int shift = 1; shift < Alphabet.Length; shift++)
            {
                copied[place] = Alphabet[(value + shift) % Alphabet.Length];

                Assert.Null(BreakGlassCode.Checked(new string(copied)));
            }
        }
    }
}
