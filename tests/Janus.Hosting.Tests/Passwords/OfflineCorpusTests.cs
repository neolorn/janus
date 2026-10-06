using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Janus.Hosting.Passwords;
using Xunit;

namespace Janus.Hosting.Tests.Passwords;

/// <summary>
/// The offline leaked-password list the package carries (AUTH-PASS-004, INT-PWD-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class OfflineCorpusTests
{
    /// <summary>
    /// AUTH-PASS-004: the list the package carries is dated on its first line and holds
    /// exactly the 100,000 hashes the item names, distinct, each 40 upper-case
    /// hexadecimal characters, one to a line.
    /// </summary>
    [Fact]
    public void AUTH_PASS_004_TheShippedListIsTheHundredThousandItNames()
    {
        using Stream shipped = typeof(OfflineCorpus).Assembly.GetManifestResourceStream(OfflineCorpus.Resource)
            ?? throw new InvalidOperationException("The package carries no offline list.");
        using var reading = new StreamReader(shipped);

        string[] lines = reading.ReadToEnd().Split('\n');
        string[] hashes = lines[1..^1];

        Assert.StartsWith("# ", lines[0], StringComparison.Ordinal);
        Assert.True(DateOnly.TryParseExact(
            lines[0][2..],
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _));
        Assert.Equal(string.Empty, lines[^1]);
        Assert.Equal(100_000, hashes.Length);
        Assert.Equal(hashes.Length, hashes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(hashes, hash => Assert.Matches("^[0-9A-F]{40}$", hash));
    }
}
