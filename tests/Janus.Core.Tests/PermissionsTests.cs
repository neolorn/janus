using System;
using System.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The permissions the library ships (chapter 10 section 2.1, CONV-NAME-002).
/// </summary>
[Trait("kind", "contract")]
public sealed class PermissionsTests
{
    private static readonly string[] Catalogue =
    [
        "account:manage",
        "account:read",
        "audit:read",
        "compliance:manage",
        "config:manage",
        "config:read",
        "consent:read",
        "contact:read",
        "domain:manage",
        "grant:manage",
        "grant:read",
        "group:manage",
        "membership:manage",
        "membership:read",
        "notice:publish",
        "organization:manage",
        "organization:read",
        "privacyrequest:manage",
        "recovery:approve",
        "restriction:edit",
        "restriction:grant",
        "role:manage",
        "ropa:read",
        "session:revoke",
        "session:revoke-account",
        "system:administer",
        "takedown:execute",
    ];

    /// <summary>
    /// CONV-NAME-002 AC2: the library ships exactly the permissions chapter 10 section
    /// 2.1 names, and each is a resource and an action with nothing else in it, so
    /// none carries a scope.
    /// </summary>
    [Fact]
    public void CONV_NAME_002_AC2_NoLibraryOwnedPermissionCarriesAScopeQualifier()
    {
        Assert.Equal(
            Catalogue,
            Permissions.All.Select(permission => permission.ToString()).ToArray());

        Assert.All(
            Permissions.All,
            permission => Assert.Equal(
                permission.Resource + ":" + permission.Action,
                permission.ToString()));
    }

    /// <summary>
    /// REF-001 AC4: a permission the library declares that is no row of chapter 10
    /// section 2.1 fails here. The test reads chapter 10 alone.
    /// </summary>
    [Fact]
    public void REF_001_AC4_EveryPermissionTheLibraryDeclaresIsARowOfTheReference()
    {
        string[] declared = [.. Permissions.All.Select(permission => permission.ToString())];

        Assert.NotEmpty(declared);
        Assert.Empty(declared.Except(ReferenceRows.ChapterPermissions, StringComparer.Ordinal));
    }

    /// <summary>
    /// The catalogue is the list, so a permission added to the library appears in both
    /// or in neither.
    /// </summary>
    [Fact]
    public void All_TheCatalogue_HoldsEveryDeclaredPermission()
    {
        string[] declared = typeof(Permissions)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(Permission))
            .Select(property => ((Permission)property.GetValue(null)!).ToString())
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Catalogue, declared);
    }
}
