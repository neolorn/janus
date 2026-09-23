using Janus.Core;
using Xunit;

namespace Janus.Privacy.Tests;

/// <summary>
/// What the declaration has to agree about before a deployment starts: one purpose is
/// one thing, whichever of the host's types declare it (PRIV-CONS-002, PRIV-CONS-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeclaredProcessingTests
{
    /// <summary>
    /// PRIV-CONS-007: the document a consent is recorded against is the document a
    /// material revision of ends it, so a purpose declared on two types against two
    /// documents is a deployment that cannot say which revision ends the consent.
    /// </summary>
    [Fact]
    public void PRIV_CONS_007_APurposeDeclaredAgainstTwoDocumentsIsRefused()
    {
        AuthorizationDeclaration declaration = Declaration.Declared()
            .Resource<Second>("second", second => second
                .BelongsToOrganization()
                .Purpose(
                    "newsletter",
                    "agreement",
                    data: ["identity"],
                    subjects: ["customers"],
                    document: "another-consent"))
            .Build();

        StartupException refused =
            Assert.Throws<StartupException>(() => DeclaredProcessing.Of(declaration));

        Assert.Contains("newsletter", refused.Message, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// PRIV-CONS-007: a purpose declared on two types against the same document is
    /// one purpose and one document, so it stands.
    /// </summary>
    [Fact]
    public void PRIV_CONS_007_APurposeDeclaredAgainstOneDocumentTwiceStands()
    {
        AuthorizationDeclaration declaration = Declaration.Declared()
            .Resource<Second>("second", second => second
                .BelongsToOrganization()
                .Purpose(
                    "newsletter",
                    "agreement",
                    data: ["identity"],
                    subjects: ["customers"],
                    document: Declaration.Newsletter))
            .Build();

        Assert.Equal(
            Declaration.Newsletter,
            DeclaredProcessing.Of(declaration).Find("newsletter")?.Document);
    }

    // A second type the same purpose is declared on, which is the only way two
    // declarations of one purpose can disagree.
    private sealed class Second
    {
        public string Id { get; init; } = string.Empty;
    }
}
