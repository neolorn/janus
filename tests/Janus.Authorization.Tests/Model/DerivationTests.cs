using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Authorization.Model;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Model;

/// <summary>
/// What a declared derivation carries, read from the built model
/// (AUTHZ-DERIVE-001, AUTHZ-DERIVE-003, AUTHZ-DERIVE-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class DerivationTests
{
    /// <summary>
    /// AUTHZ-DERIVE-001 AC1: a derivation is declared where containment and sensitivity
    /// are declared, on the resource type itself, so a host adds one by writing a line
    /// of its own declaration and not by changing the library.
    /// </summary>
    [Fact]
    public void AUTHZ_DERIVE_001_AC1_ADerivationIsDeclaredWhereContainmentIs()
    {
        AuthorizationDeclaration declared = HostDomain.Declared().Build();

        ResourceTypeDeclaration folder = declared.ResourceTypes.Single(
            type => type.Name == ResourceType.Parse("folder"));

        Assert.Equal(ResourceType.Parse("workspace"), folder.ContainedIn);
        Assert.Equal(
            new DerivationDeclaration("reviewer", RoleName.Parse("reader"), Materialised: false),
            folder.Derivations.Single());
    }

    /// <summary>
    /// AUTHZ-DERIVE-003 AC1: a derivation names a fact in the host's own data, and
    /// that fact names the table and the column it is read from, so a derivation that
    /// exists only to carry a permission has nothing to point at.
    /// </summary>
    [Fact]
    public void AUTHZ_DERIVE_003_AC1_EachDerivationNamesADeclaredRelationship()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());

        IReadOnlyList<DerivationDeclaration> derivations =
        [
            .. model.ResourceTypes.SelectMany(type => type.Derivations),
        ];

        Assert.NotEmpty(derivations);

        Assert.All(derivations, derivation =>
        {
            RelationshipDeclaration? named = model.Relationships.FirstOrDefault(relationship =>
                string.Equals(relationship.Name, derivation.Relationship, StringComparison.Ordinal));

            Assert.NotNull(named);
            Assert.NotEmpty(named.Relation);
            Assert.NotEmpty(named.HolderColumn);
            Assert.NotEmpty(named.ResourceColumn);
            Assert.Equal([named.HolderColumn, named.ResourceColumn], named.Columns);
        });
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC1: a derivation is evaluated per request unless the
    /// declaration says otherwise, materialisation being the last rung of the ladder
    /// and never the default (AUTHZ-DERIVE-006).
    /// </summary>
    [Fact]
    public void AUTHZ_DERIVE_005_AC1_MaterialisationIsDeclaredPerDerivation()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared()
            .Resource<HostDomain.Draft>("draft", draft => draft
                .ContainedIn("folder")
                .Purpose("collaboration", "contract")
                .Derivation("reviewer", "reader", materialised: true))
            .Build());

        Assert.All(
            model.Find(ResourceType.Parse("folder"))!.Derivations,
            derivation => Assert.False(derivation.Materialised));

        Assert.All(
            model.Find(ResourceType.Parse("draft"))!.Derivations,
            derivation => Assert.True(derivation.Materialised));
    }

    /// <summary>
    /// AUTHZ-DERIVE-005 AC2, AUTHZ-GRANT-001: where a grant came from is carried on the
    /// grant and on the explanation, so a row precomputed from a fact is never read as
    /// a row somebody wrote.
    /// </summary>
    [Fact]
    public void AUTHZ_DERIVE_005_AC2_WhereAGrantCameFromIsCarriedOnItAndOnItsExplanation()
    {
        Assert.Equal(
            [GrantKind.Stored, GrantKind.Derived, GrantKind.Materialised],
            Enum.GetValues<GrantKind>());

        Assert.Equal(
            typeof(GrantKind),
            typeof(ExplainedGrant).GetProperty(nameof(ExplainedGrant.Kind))?.PropertyType);
    }
}
