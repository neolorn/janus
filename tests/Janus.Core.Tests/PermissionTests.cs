using System;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The permission string and what it admits (CONV-NAME-002, CONV-DESIGN-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class PermissionTests
{
    /// <summary>
    /// The halves are what the two words of the sentence name, so a caller reads them
    /// rather than splitting the string again.
    /// </summary>
    [Fact]
    public void Parse_AWellFormedPermission_CarriesItsResourceAndAction()
    {
        var permission = Permission.Parse("document:read");

        Assert.Equal("document", permission.Resource);
        Assert.Equal("read", permission.Action);
        Assert.Equal("document:read", permission.ToString());
    }

    /// <summary>
    /// A hyphen joins the words of one action, which is how the library's own
    /// <c>session:revoke-account</c> is spelled.
    /// </summary>
    [Fact]
    public void Parse_AHyphenatedAction_IsAdmitted()
    {
        var permission = Permission.Parse("session:revoke-account");

        Assert.Equal("session", permission.Resource);
        Assert.Equal("revoke-account", permission.Action);
    }

    /// <summary>
    /// Anything outside the format is refused where it is written, so an invalid
    /// permission is never a value the model carries.
    /// </summary>
    /// <param name="value">The value offered.</param>
    [Theory]
    [InlineData("Document:read")]
    [InlineData("document:Read")]
    [InlineData("document")]
    [InlineData("document:")]
    [InlineData(":read")]
    [InlineData("document:read:branch")]
    [InlineData("document read")]
    [InlineData("document.read")]
    [InlineData("")]
    [InlineData("-document:read")]
    public void Parse_AStringOutsideTheFormat_IsRefused(string value)
    {
        Assert.Throws<ArgumentException>(() => Permission.Parse(value));
        Assert.False(Permission.TryParse(value, out _));
    }

    /// <summary>
    /// An unset permission is neither a resource nor an action, and says so rather
    /// than throwing where it is read.
    /// </summary>
    [Fact]
    public void Resource_AnUnsetPermission_IsEmpty()
    {
        Permission permission = default;

        Assert.Equal(string.Empty, permission.Resource);
        Assert.Equal(string.Empty, permission.Action);
        Assert.Equal(string.Empty, permission.ToString());
    }
}
