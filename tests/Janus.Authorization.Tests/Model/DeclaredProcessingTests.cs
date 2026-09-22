using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Janus.Authorization.Model;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Model;

/// <summary>
/// What a host declares it processes, on what basis, and over what
/// (PRIV-PRIN-001, PRIV-BASIS-001 to PRIV-BASIS-004, PRIV-SENS-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeclaredProcessingTests
{
    // Chapter 10 section 5.7: the keys of the default declaration. No library source
    // may name one, which is what makes another jurisdiction's list a configuration
    // change rather than a code change.
    private static readonly string[] DefaultBases =
    [
        "consent",
        "contractual-obligation",
        "legal-obligation",
        "legitimate-interest",
        "legal-right-claim-or-defence",
        "court-judgment-or-order",
    ];

    /// <summary>
    /// PRIV-PRIN-001 AC1: a purpose states the categories of data it requires, so the
    /// declaration says what is collected rather than what happens to be held.
    /// </summary>
    [Fact]
    public void PRIV_PRIN_001_AC1_EachPurposeNamesTheCategoriesItRequires()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());

        Assert.All(
            model.ResourceTypes.SelectMany(type => type.Purposes),
            purpose => Assert.NotEmpty(purpose.DataCategories));
        Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(
                Declaring(document => document.Purpose("collaboration", "contract"))));
    }

    /// <summary>
    /// PRIV-PRIN-001 AC2: a field is held for a purpose or not at all, so a type
    /// carrying one and declaring no purpose stops the deployment.
    /// </summary>
    [Fact]
    public void PRIV_PRIN_001_AC2_AFieldHeldByNoDeclaredPurposeFailsValidation()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(
                Declaring(document => document.Encrypted(item => item.Body, item => item.Author))));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
    }

    /// <summary>
    /// PRIV-BASIS-001 AC1: the list is closed, so a purpose resting on a basis the
    /// host did not declare stops the deployment rather than processing on a basis
    /// nobody wrote down.
    /// </summary>
    [Fact]
    public void PRIV_BASIS_001_AC1_APurposeOnAnUndeclaredBasisFailsStartup() =>
        Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(
                Declaring(document => document.Purpose("collaboration", "consent", data: ["identity"]))));

    /// <summary>
    /// PRIV-BASIS-001 AC3: a purpose resting on nothing is refused where it is
    /// written, which is the only path by which one reaches the model.
    /// </summary>
    [Fact]
    public void PRIV_BASIS_001_AC3_APurposeWithoutABasisIsRefusedWhereItIsDeclared() =>
        Assert.Throws<ArgumentException>(
            () => new AuthorizationDeclarationBuilder().Resource<HostDomain.Document>(
                "document",
                document => document.Purpose("collaboration", " ")));

    /// <summary>
    /// PRIV-BASIS-001 AC4: no library source carries a basis of the default
    /// declaration as a value, which is the only form a conditional on one could
    /// take. One file carries the word, as the wire name of a capability residual of
    /// chapter 10 section 5.20, which is not a basis and is asserted to be what it is.
    /// </summary>
    [Fact]
    public void PRIV_BASIS_001_AC4_NoLibrarySourceNamesABasis()
    {
        Assert.Equal(["CapabilityResidual.cs"], Naming(DefaultBases));
        Assert.Equal("consent", Wire(CapabilityResidual.Consent));
    }

    /// <summary>
    /// PRIV-BASIS-004 AC1: the same search, stated for the two bases that exist for
    /// record completeness alone and are cited rather than implemented.
    /// </summary>
    [Fact]
    public void PRIV_BASIS_004_AC1_NoLibrarySourceNamesABasisWhosePropertiesAreAllUnset() =>
        Assert.Empty(Naming(["legal-right-claim-or-defence", "court-judgment-or-order"]));

    /// <summary>
    /// PRIV-BASIS-001 AC5: a jurisdiction whose bases carry other properties declares
    /// them and the library changes not at all, being the same binary reading the
    /// flags it is handed.
    /// </summary>
    [Fact]
    public void PRIV_BASIS_001_AC5_AnotherListWithOtherFlagsNeedsNoLibraryChange()
    {
        var model = AuthorizationModel.Of(
            new AuthorizationDeclarationBuilder()
                .LawfulBasis(new LawfulBasisDeclaration("vital-interests", false, false, false, false))
                .LawfulBasis(new LawfulBasisDeclaration("public-task", false, false, true, true))
                .Resource<HostDomain.Workspace>("workspace", workspace => workspace
                    .BelongsToOrganization()
                    .Purpose("registry", "public-task", "LIA-2", data: ["identity"]))
                .Build());

        Assert.Equal("public-task", model.ResourceTypes.Single().Purposes.Single().Basis);
    }

    /// <summary>
    /// PRIV-BASIS-002 AC1: a basis carrying the assessment property admits no purpose
    /// that names no assessment, so the reliance is documented rather than implicit.
    /// </summary>
    [Fact]
    public void PRIV_BASIS_002_AC1_APurposeOnAnAssessedBasisWithoutOneFailsStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(
                new AuthorizationDeclarationBuilder()
                    .LawfulBasis(new LawfulBasisDeclaration("interest", false, false, true, true))
                    .Resource<HostDomain.Workspace>("workspace", workspace => workspace
                        .BelongsToOrganization()
                        .Purpose("fraud-prevention", "interest", data: ["identity"]))
                    .Build()));

        Assert.Equal(ErrorCodes.StartupMissingAssessment, refused.Failure?.Code);
    }

    /// <summary>
    /// PRIV-BASIS-003 AC1: a purpose over sensitive data that states the ordinary
    /// path, on a basis requiring the written one there, stops the deployment rather
    /// than capturing a consent that would not stand.
    /// </summary>
    [Fact]
    public void PRIV_BASIS_003_AC1_TheOrdinaryPathOverSensitiveDataFailsValidation() =>
        Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Consenting(sensitive: true, ConsentKind.Ordinary)));

    /// <summary>
    /// AUTHZ-MODEL-003 AC2: the capture path follows from the sensitivity of the type
    /// and the properties of the basis, so declaring a type sensitive is the whole of
    /// the change.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_003_AC2_DeclaringATypeSensitiveChangesWhatItsConsentAsksFor()
    {
        Assert.Equal(
            ConsentKind.Ordinary,
            AuthorizationModel.Of(Consenting(sensitive: false, null))
                .Processing.Find("recommendations")!.Consent);
        Assert.Equal(
            ConsentKind.Written,
            AuthorizationModel.Of(Consenting(sensitive: true, null))
                .Processing.Find("recommendations")!.Consent);
    }

    /// <summary>
    /// A purpose on a basis that is not consent runs through no capture path at all,
    /// which is what makes the path a property of the basis rather than of the
    /// dashboard.
    /// </summary>
    [Fact]
    public void Of_APurposeOnANonConsentBasis_RunsThroughNoCapturePath() =>
        Assert.Null(
            AuthorizationModel.Of(HostDomain.Declared().Build())
                .Processing.Find("collaboration")!.Consent);

    /// <summary>
    /// PRIV-SENS-001 AC1: a type is declared sensitive by naming a category of the
    /// declared list and nothing further, and a category outside that list stops the
    /// deployment.
    /// </summary>
    [Fact]
    public void PRIV_SENS_001_AC1_SensitivityIsACategoryOfTheDeclaredList()
    {
        var model = AuthorizationModel.Of(
            Declaring(document => document
                .Sensitive("financial")
                .Purpose("collaboration", "contract", data: ["identity"])));

        Assert.Equal(
            ["financial"],
            model.Find(ResourceType.Parse("document"))!.SensitiveCategories);
        Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(
                Declaring(document => document
                    .Sensitive("health")
                    .Purpose("collaboration", "contract", data: ["identity"]))));
    }

    // The wire name chapter 10 gives a residual, read from where the library declares
    // it rather than from a second copy of the list.
    private static string Wire(CapabilityResidual residual) =>
        typeof(CapabilityResidual)
            .GetField(residual.ToString())!
            .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!
            .Name;

    // Every library source carrying one of the keys as a value, comments aside. A
    // basis is read by its properties, so its key reaches the library only as data it
    // was handed, and prose naming one decides nothing.
    private static IReadOnlyList<string> Naming(IReadOnlyList<string> keys) =>
    [
        .. Directory
            .EnumerateFiles(Source(), "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadLines(file).Any(line =>
                !line.TrimStart().StartsWith('/')
                && keys.Any(key => line.Contains('"' + key + '"', StringComparison.Ordinal))))
            .Select(file => Path.GetFileName(file)!)
            .Order(StringComparer.Ordinal),
    ];

    // A deployment whose one purpose rests on consent over a basis that requires the
    // written path for sensitive data, the type being sensitive or not as the test
    // asks.
    private static AuthorizationDeclaration Consenting(bool sensitive, ConsentKind? consent) =>
        new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("agreement", true, true, false, false))
            .SensitiveCategory("financial")
            .Resource<HostDomain.Workspace>("workspace", workspace =>
            {
                // PRIV-SENS-002 AC1: a consent-based purpose needs a type whose
                // encrypted fields name the column the data subject is read from.
                _ = workspace
                    .BelongsToOrganization()
                    .Encrypted(held => held.Title, held => held.Owner)
                    .Purpose("recommendations", "agreement", data: ["history"], consent: consent);

                if (sensitive)
                {
                    _ = workspace.Sensitive("financial");
                }
            })
            .Build();

    // One type of the host's, declared as the test needs it, with everything else the
    // model requires already in place.
    private static AuthorizationDeclaration Declaring(
        Action<ResourceTypeDeclarationBuilder<HostDomain.Document>> declared) =>
        new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("contract", false, false, false, false))
            .SensitiveCategory("financial")
            .Resource<HostDomain.Document>("document", document =>
            {
                _ = document.BelongsToOrganization();
                declared(document);
            })
            .Build();

    private static string Source()
    {
        var at = new DirectoryInfo(AppContext.BaseDirectory);

        while (at is not null && !Directory.Exists(Path.Combine(at.FullName, "src")))
        {
            at = at.Parent;
        }

        return Path.Combine(at!.FullName, "src");
    }
}
