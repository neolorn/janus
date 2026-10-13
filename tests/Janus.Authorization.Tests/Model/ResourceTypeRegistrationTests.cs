using System.Linq;
using Janus.Authorization.Model;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Model;

/// <summary>
/// What registering a kind of thing the library has never seen comes to (LIB-EXT-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class ResourceTypeRegistrationTests
{
    /// <summary>
    /// LIB-EXT-002 AC2: a resource type the host adds is one more declaration over the
    /// same library. The model built without it does not know it, the model built with
    /// it finds it by its name and by the host's own type, and every type declared
    /// before stands as it stood.
    /// </summary>
    [Fact]
    public void LIB_EXT_002_AC2_ANewResourceTypeIsOneMoreDeclarationOverTheSameLibrary()
    {
        var added = ResourceType.Parse("draft");

        var before = AuthorizationModel.Of(HostDomain.Declared().Build());
        var after = AuthorizationModel.Of(
            HostDomain.Declared()
                .Resource<HostDomain.Draft>("draft", draft => draft
                    .ContainedIn("folder")
                    .Purpose("collaboration", "contract", data: ["identity"]))
                .Build());

        Assert.Null(before.Find(added));
        Assert.NotNull(after.Find(added));
        Assert.Same(after.Find(added), after.Find(typeof(HostDomain.Draft)));
        Assert.Equal(
            [.. before.ResourceTypes.Select(type => type.Name), added],
            after.ResourceTypes.Select(type => type.Name));
    }
}
