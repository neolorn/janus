using System;
using Janus.Authorization.Resources;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Resources;

/// <summary>
/// One of the host's records as the library knows it (AUTHZ-INHERIT-001,
/// AUTHZ-SCOPE-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class RegisteredResourceTests
{
    /// <summary>
    /// A record the host registers carries which record it is, whose organization owns
    /// it, and what contains it.
    /// </summary>
    [Fact]
    public void Create_InsideAContainer_CarriesTheContainer()
    {
        ResourceReference folder = Identifiers.Resource("folder");
        ResourceReference document = Identifiers.Resource("document");
        OrganizationId organization = Identifiers.Organization();

        var resource = RegisteredResource.Create(document, organization, subject: null, folder);

        Assert.Equal(document, resource.Reference);
        Assert.Equal(organization, resource.Organization);
        Assert.Equal(folder, resource.ContainedIn);
    }

    /// <summary>
    /// A record contained in nothing is the top of its own ancestry.
    /// </summary>
    [Fact]
    public void Create_InsideNothing_HasNoContainer()
    {
        var resource = RegisteredResource.Create(
            Identifiers.Resource("workspace"),
            Identifiers.Organization(),
            subject: null,
            containedIn: null);

        Assert.Null(resource.ContainedIn);
    }

    /// <summary>
    /// A move carries the record under another container.
    /// </summary>
    [Fact]
    public void MoveTo_AnotherContainer_CarriesTheNewOne()
    {
        ResourceReference elsewhere = Identifiers.Resource("folder");
        var resource = RegisteredResource.Create(
            Identifiers.Resource("document"),
            Identifiers.Organization(),
            subject: null,
            Identifiers.Resource("folder"));

        resource.MoveTo(elsewhere);

        Assert.Equal(elsewhere, resource.ContainedIn);
    }

    /// <summary>
    /// A move out of every container leaves the record contained in nothing.
    /// </summary>
    [Fact]
    public void MoveTo_OutOfEveryContainer_HasNoContainer()
    {
        var resource = RegisteredResource.Create(
            Identifiers.Resource("document"),
            Identifiers.Organization(),
            subject: null,
            Identifiers.Resource("folder"));

        resource.MoveTo(containedIn: null);

        Assert.Null(resource.ContainedIn);
    }

    /// <summary>
    /// A row read back carries what was written to it.
    /// </summary>
    [Fact]
    public void Existing_ARow_CarriesWhatWasWritten()
    {
        ResourceReference document = Identifiers.Resource("document");
        ResourceReference folder = Identifiers.Resource("folder");
        OrganizationId organization = Identifiers.Organization();
        var whose = new SubjectId(Guid.Parse("22222222-2222-4222-8222-222222222222"));

        var resource = RegisteredResource.Existing(document, organization, whose, folder);

        Assert.Equal(document, resource.Reference);
        Assert.Equal(organization, resource.Organization);
        Assert.Equal(whose, resource.Subject);
        Assert.Equal(folder, resource.ContainedIn);
    }
}
