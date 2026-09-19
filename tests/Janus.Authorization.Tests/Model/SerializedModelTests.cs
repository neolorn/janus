using System;
using System.IO;
using Janus.Authorization.Model;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Model;

/// <summary>
/// The built model as it is committed and reviewed (AUTHZ-MODEL-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class SerializedModelTests
{
    /// <summary>
    /// AUTHZ-MODEL-005 AC1: two runs of one configuration write the same bytes, so a
    /// diff in review is a change to the model and never to the order of a list.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_005_AC1_OneConfigurationSerializesToTheSameBytes()
    {
        var one = AuthorizationModel.Of(HostDomain.Declared().Build());
        var again = AuthorizationModel.Of(Reordered().Build());

        Assert.Equal(one.Serialize(), again.Serialize());
    }

    /// <summary>
    /// AUTHZ-MODEL-005 AC2: a change to what the host declared is a difference in the
    /// file, on the line the change was made.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_005_AC2_APermissionModelChangeProducesADiff()
    {
        var before = AuthorizationModel.Of(HostDomain.Declared().Build());
        var after = AuthorizationModel.Of(
            HostDomain.Declared().Permission("document:publish").Build());

        Assert.NotEqual(before.Serialize(), after.Serialize());
        Assert.DoesNotContain("document:publish", before.Serialize(), StringComparison.Ordinal);
        Assert.Contains("document:publish", after.Serialize(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The file is written where the build's other outputs are, under the one name
    /// chapter 08 gives it.
    /// </summary>
    [Fact]
    public void WriteTo_ADirectory_WritesTheModelBesideTheBuildsOutputs()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        string path = model.WriteTo(directory);

        Assert.Equal(Path.Combine(directory, "model.json"), path);
        Assert.Equal(model.Serialize(), File.ReadAllText(path));
        Directory.Delete(directory, recursive: true);
    }

    // AUTHZ-MODEL-005 AC1: the same declaration written in another order, which is the
    // difference a deterministic serialization has to absorb.
    private static AuthorizationDeclarationBuilder Reordered() =>
        new AuthorizationDeclarationBuilder()
            .Permission("document:edit")
            .Permission("document:read")
            .LawfulBasis(new LawfulBasisDeclaration("interest", false, false, true, true))
            .LawfulBasis(new LawfulBasisDeclaration("contract", false, false, false, false))
            .Relationship<HostDomain.Folder>("reviewer", "folder", "folders", folder => folder.Reviewer)
            .SensitiveCategory("financial")
            .Resource<HostDomain.Document>("document", document => document
                .ContainedIn("folder")
                .Purpose("collaboration", "contract")
                .Encrypted(item => item.Body, item => item.Author))
            .Resource<HostDomain.Folder>("folder", folder => folder
                .ContainedIn("workspace")
                .Derivation("reviewer", "reader")
                .Purpose("collaboration", "contract"))
            .Resource<HostDomain.Workspace>("workspace", workspace => workspace
                .BelongsToOrganization()
                .Purpose("collaboration", "contract"));
}
