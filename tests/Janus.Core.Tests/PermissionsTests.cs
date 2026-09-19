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
        "audit:read",
        "compliance:manage",
        "config:manage",
        "config:read",
        "domain:manage",
        "grant:manage",
        "grant:read",
        "group:manage",
        "membership:manage",
        "notice:publish",
        "organization:manage",
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
