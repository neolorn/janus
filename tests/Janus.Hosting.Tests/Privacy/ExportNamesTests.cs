using Janus.Hosting.Privacy;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The rule the names an export contributor answers are held to, as this project
/// answers it for the assembly (PRIV-RIGHT-003, INT-SMS-003, D-206).
/// </summary>
[Trait("kind", "unit")]
public sealed class ExportNamesTests
{
    /// <summary>
    /// PRIV-RIGHT-003 AC7: a name of 1 to 64 lower-case letters and digits separated by
    /// single <c>-</c> or <c>_</c> keeps the rule.
    /// </summary>
    /// <param name="name">The name a contributor answers.</param>
    [Theory]
    [InlineData("parcels")]
    [InlineData("tracking-number")]
    [InlineData("delivered_at")]
    [InlineData("v2")]
    [InlineData("a")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz01")]
    public void PRIV_RIGHT_003_AC7_ANameInsideTheRuleIsTaken(string name) =>
        Assert.True(new ExportNames().Holds(name));

    /// <summary>
    /// PRIV-RIGHT-003 AC7: a name holding a <c>.</c>, which the rule of a message place
    /// admits and the machine format's keys would read as a separator, is refused, and so
    /// is every name outside the rule of a message place.
    /// </summary>
    /// <param name="name">The name a contributor answers.</param>
    [Theory]
    [InlineData("parcels.v2")]
    [InlineData("tracking.number")]
    [InlineData("Parcels")]
    [InlineData("trackingNumber")]
    [InlineData("tracking number")]
    [InlineData("export/parcels")]
    [InlineData("-parcels")]
    [InlineData("parcels_")]
    [InlineData("parcels--v2")]
    [InlineData("")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456789abcdefghijklmnopqrstuvwxyz012")]
    [InlineData(null)]
    public void PRIV_RIGHT_003_AC7_ANameOutsideTheRuleIsRefused(string? name) =>
        Assert.False(new ExportNames().Holds(name));
}
