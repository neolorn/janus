using System;
using Janus.Authorization.Groups;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Groups;

/// <summary>
/// The set of subjects that holds grants on their behalf (AUTHZ-GROUP-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class GroupTests
{
    /// <summary>
    /// A group belongs to an organization and carries the name it was given.
    /// </summary>
    [Fact]
    public void Create_WithANameAndAnOrganization_CarriesBoth()
    {
        OrganizationId organization = Identifiers.Organization();

        var group = Group.Create(Identifiers.Group(), organization, "Underwriting");

        Assert.Equal(organization, group.Organization);
        Assert.Equal("Underwriting", group.Name);
    }

    /// <summary>
    /// A name is stored as it reads, not as it was typed.
    /// </summary>
    [Fact]
    public void Create_WithANameInsideWhitespace_TrimsIt()
    {
        var group = Group.Create(Identifiers.Group(), Identifiers.Organization(), "  Underwriting  ");

        Assert.Equal("Underwriting", group.Name);
    }

    /// <summary>
    /// A group with no name is a fault in the caller.
    /// </summary>
    /// <param name="name">A name that names nothing.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutAName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(
            () => Group.Create(Identifiers.Group(), Identifiers.Organization(), name));
    }

    /// <summary>
    /// A group with no name at all is the same fault.
    /// </summary>
    [Fact]
    public void Create_WithANullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => Group.Create(Identifiers.Group(), Identifiers.Organization(), null!));
    }

    /// <summary>
    /// A row read back carries what was written to it.
    /// </summary>
    [Fact]
    public void Existing_ARow_CarriesWhatWasWritten()
    {
        GroupId id = Identifiers.Group();
        OrganizationId organization = Identifiers.Organization();

        var group = Group.Existing(id, organization, "Underwriting");

        Assert.Equal(id, group.Id);
        Assert.Equal(organization, group.Organization);
        Assert.Equal("Underwriting", group.Name);
    }
}
