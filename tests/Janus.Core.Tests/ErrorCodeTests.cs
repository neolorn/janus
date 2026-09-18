using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The shape and equality of an error code (CONV-NAME-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class ErrorCodeTests
{
    /// <summary>
    /// A hierarchical, lowercase, dot-separated code is read back unchanged.
    /// </summary>
    /// <param name="value">The code.</param>
    [Theory]
    [InlineData("auth.session.expired")]
    [InlineData("authz.grant.notfound")]
    [InlineData("config.value.belowfloor")]
    [InlineData("model.startup.governinglanguage")]
    public void Parse_HierarchicalLowercaseCode_Accepted(string value)
    {
        var code = ErrorCode.Parse(value);

        Assert.Equal(value, code.ToString());
    }

    /// <summary>
    /// Anything outside the format of CONV-NAME-003 is refused rather than stored.
    /// </summary>
    /// <param name="value">The malformed code.</param>
    [Theory]
    [InlineData("Auth.Session.Expired")]
    [InlineData("authsessionexpired")]
    [InlineData("auth..expired")]
    [InlineData("auth.session.")]
    [InlineData(".auth.session")]
    [InlineData("auth session expired")]
    [InlineData("auth_session_expired")]
    [InlineData("auth.session.expired!")]
    [InlineData("1auth.session")]
    [InlineData("")]
    public void Parse_OutsideTheFormat_Refused(string value) =>
        Assert.Throws<ArgumentException>(() => ErrorCode.Parse(value));

    /// <summary>
    /// An absent code is refused on the same argument as a malformed one.
    /// </summary>
    [Fact]
    public void Parse_Absent_Refused() =>
        Assert.Throws<ArgumentException>(() => ErrorCode.Parse(null!));

    /// <summary>
    /// Two readings of the same code are the same value.
    /// </summary>
    [Fact]
    public void Equals_SameCode_Equal()
    {
        var one = ErrorCode.Parse("auth.code.expired");
        var other = ErrorCode.Parse("auth.code.expired");

        Assert.Equal(one, other);
    }

    /// <summary>
    /// Readings of different codes are different values.
    /// </summary>
    [Fact]
    public void Equals_DifferentCodes_NotEqual()
    {
        var one = ErrorCode.Parse("auth.code.expired");
        var other = ErrorCode.Parse("auth.code.used");

        Assert.NotEqual(one, other);
    }
}
