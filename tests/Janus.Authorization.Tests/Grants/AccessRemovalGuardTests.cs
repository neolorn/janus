using System;
using System.Linq;
using System.Reflection;
using Janus.Authorization.Grants;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Grants;

/// <summary>
/// The prefixes no host's code of a refused removal may take (AUTHZ-GRANT-005, D-281).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccessRemovalGuardTests
{
    /// <summary>
    /// AUTHZ-GRANT-005 (D-281): the library's prefixes are exactly the first segments its
    /// catalogue of codes uses, and <c>session</c>, so a code added under a new segment
    /// fails here until the list holds it.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_005_TheLibrarysPrefixesAreTheFirstSegmentsOfItsCatalogue()
    {
        string[] catalogued =
        [
            .. typeof(ErrorCodes)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(property => property.PropertyType == typeof(ErrorCode))
                .Select(property => property.GetValue(null)!.ToString()!.Split('.')[0])
                .Append("session")
                .Distinct()
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(catalogued, AccessRemovalGuard.LibraryPrefixes.Order(StringComparer.Ordinal));
    }
}
