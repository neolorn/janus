using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Janus.Authorization.Model;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Model;

/// <summary>
/// The model a host declares and what building it refuses
/// (AUTHZ-MODEL-001 to AUTHZ-MODEL-004, AUTHZ-MODEL-006, AUTHZ-GATE-001,
/// AUTHZ-CONCEAL-001, PRIV-RIGHT-005a, OPS-ALERT-006, INT-HOST-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class AuthorizationModelTests
{
    private static readonly string[] HostTypeNames =
    [
        nameof(HostDomain.Workspace),
        nameof(HostDomain.Folder),
        nameof(HostDomain.Article),
        nameof(HostDomain.Draft),
        nameof(OtherDomain.Depot),
        nameof(OtherDomain.Vehicle),
        nameof(OtherDomain.Journey),
    ];

    /// <summary>
    /// AUTHZ-MODEL-001 AC1: a search of the library's source finds no name belonging
    /// to a host's domain, because the library is told about them and never holds one.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_001_AC1_NoLibrarySourceNamesAHostDomainType()
    {
        foreach (string file in Directory.EnumerateFiles(Source(), "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);

            Assert.All(
                HostTypeNames,
                name => Assert.DoesNotMatch("\\b" + name + "\\b", text));
        }
    }

    /// <summary>
    /// AUTHZ-MODEL-001 AC2: two hosts whose domains share nothing declare against the
    /// same library binary and each gets the model it declared.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_001_AC2_TwoDomainsSharingNothingUseTheSameBinary()
    {
        var one = AuthorizationModel.Of(HostDomain.Declared().Build());
        var other = AuthorizationModel.Of(OtherDomain.Declared().Build());

        Assert.Equal(
            ["workspace", "folder", "article"],
            one.ResourceTypes.Select(type => type.Name.ToString()));
        Assert.Equal(
            ["depot", "vehicle", "journey"],
            other.ResourceTypes.Select(type => type.Name.ToString()));
        Assert.False(typeof(AuthorizationModel).IsGenericType);
    }

    /// <summary>
    /// AUTHZ-MODEL-001 AC3: a field is named by a reference the compiler checks, so a
    /// renamed property is a build error rather than a rule that stops matching, and
    /// no method of the declaration takes a field's name as text.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_001_AC3_AFieldIsNamedByACompilerCheckedReference()
    {
        AuthorizationDeclaration declared = HostDomain.Declared().Build();
        ResourceTypeDeclaration article = declared.ResourceTypes.Single(
            type => type.Name == ResourceType.Parse("article"));

        Assert.Equal(
            new EncryptedFieldDeclaration(
                nameof(HostDomain.Article.Body),
                nameof(HostDomain.Article.Author)),
            article.EncryptedFields.Single());
        Assert.Equal(
            nameof(HostDomain.Folder.Reviewer),
            Assert.IsAssignableFrom<MemberExpression>(
                declared.Relationships.Single().Holder.Body).Member.Name);
    }

    /// <summary>
    /// AUTHZ-MODEL-002 AC1: a host adds a kind of thing by naming it, and the library
    /// ships no enumeration a new one would have to be added to.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_002_AC1_AddingAResourceTypeNeedsNoLibraryChange()
    {
        var model = AuthorizationModel.Of(
            HostDomain.Declared()
                .Resource<HostDomain.Draft>("draft", draft => draft
                    .ContainedIn("folder")
                    .Purpose("collaboration", "contract", data: ["identity"]))
                .Build());

        Assert.NotNull(model.Find(ResourceType.Parse("draft")));
        Assert.DoesNotContain(
            typeof(ResourceType).Assembly.GetTypes(),
            type => type.IsEnum && type.Name.Contains("Resource", StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTHZ-MODEL-003 AC1: a type declared without a purpose stops the deployment,
    /// because a purpose is what the records of processing are generated from.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_003_AC1_ATypeDeclaredWithoutAPurposeFailsStartup()
    {
        AuthorizationDeclaration declared = HostDomain.Declared()
            .Resource<HostDomain.Draft>("note", note => note.ContainedIn("folder"))
            .Build();

        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(declared));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
    }

    /// <summary>
    /// PRIV-RET-001 AC1: a purpose over a category the host declares no retention
    /// floor for stops the deployment, naming the key that would hold its period.
    /// </summary>
    [Fact]
    public void PRIV_RET_001_AC1_ACategoryWithNoRetentionFloorFailsStartup()
    {
        AuthorizationDeclaration declared = HostDomain.Declared()
            .Resource<HostDomain.Draft>("draft", draft => draft
                .ContainedIn("folder")
                .Purpose("collaboration", "contract", data: ["drafts"]))
            .Build();

        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(declared));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("retention.drafts", refused.Failure!.Details["key"].GetString());
    }

    /// <summary>
    /// PRIV-RET-001 AC1: a floor for a category no purpose is over governs nothing,
    /// so the declaration is refused rather than started with a period nobody reads.
    /// </summary>
    [Fact]
    public void PRIV_RET_001_AC1_AFloorForACategoryNoPurposeIsOverFailsStartup() =>
        Assert.Throws<StartupException>(() => AuthorizationModel.Of(
            HostDomain.Declared().RetentionFloor("drafts", TimeSpan.FromDays(30)).Build()));

    /// <summary>
    /// PRIV-RET-001 AC2: a floor is a positive period, declared once, for a category
    /// whose name can be the last segment of its key.
    /// </summary>
    [Fact]
    public void PRIV_RET_001_AC2_AFloorIsAPositivePeriodDeclaredOnce()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AuthorizationDeclarationBuilder().RetentionFloor("drafts", TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AuthorizationDeclarationBuilder().RetentionFloor("drafts", TimeSpan.FromDays(-1)));
        Assert.Throws<ArgumentException>(
            () => new AuthorizationDeclarationBuilder().RetentionFloor("Drafts and notes", TimeSpan.FromDays(30)));
        Assert.Throws<ArgumentException>(
            () => HostDomain.Declared().RetentionFloor("identity", TimeSpan.FromDays(30)));
    }

    /// <summary>
    /// INT-HOST-002 AC1: a purpose for the hosting or its transfer declared on a
    /// consent basis stops the deployment, naming the type and the purpose, so no
    /// consent record can ever reference it. The same purpose on another basis builds,
    /// and so does another purpose on consent, so the refusal is about the two
    /// together.
    /// </summary>
    /// <param name="purpose">The purpose the deployment declares.</param>
    [Theory]
    [InlineData("hosting")]
    [InlineData("Hosting")]
    [InlineData("transfer")]
    [InlineData("hosting-transfer")]
    [InlineData("cross-border-transfer")]
    public void INT_HOST_002_AC1_AConsentPurposeForTheHostingFailsStartup(string purpose)
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Declaring(purpose, "consent")));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
        Assert.Equal("article." + purpose, refused.Failure!.Details["key"].GetString());

        Assert.NotNull(AuthorizationModel.Of(Declaring(purpose, "contract")));
        Assert.NotNull(AuthorizationModel.Of(Declaring("newsletter", "consent")));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC1: an encrypted field naming no subject column is ciphertext
    /// no erasure could reach, so the deployment stops rather than start with it.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC1_AnEncryptedFieldNamingNoSubjectColumnFailsStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Encrypting(new EncryptedFieldDeclaration("Body", ""))));

        Assert.Equal(ErrorCodes.StartupDeclarationMissing, refused.Failure?.Code);
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC2: a subject column the type does not hold, and one holding
    /// something that is not a subject, both stop the deployment; the same declaration
    /// naming the subject builds, so the column is what the refusal is about.
    /// </summary>
    /// <param name="column">The column the encrypted field names as its subject.</param>
    [Theory]
    [InlineData("Owner")]
    [InlineData("FolderId")]
    public void PRIV_RIGHT_005a_AC2_ASubjectColumnNamingNoSubjectFailsStartup(string column)
    {
        Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Encrypting(new EncryptedFieldDeclaration("Body", column))));

        Assert.NotNull(AuthorizationModel.Of(Encrypting(new EncryptedFieldDeclaration("Body", "Author"))));
    }

    /// <summary>
    /// AUTHZ-MODEL-004 AC1: each condition the chapter lists produces its own named
    /// error, identifying the declaration that has to change.
    /// </summary>
    /// <param name="code">The code the refusal carries.</param>
    /// <param name="index">Which of the malformed declarations to build.</param>
    [Theory]
    [InlineData("model.containment.cycle", 0)]
    [InlineData("model.type.undeclaredreference", 1)]
    [InlineData("model.type.noorganizationpath", 2)]
    [InlineData("model.purpose.missingassessment", 3)]
    [InlineData("model.derivation.undeclaredreference", 4)]
    public void AUTHZ_MODEL_004_AC1_EachRefusedDeclarationCarriesItsOwnCode(string code, int index)
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Malformed(index)));

        Assert.Equal(ErrorCode.Parse(code), refused.Failure?.Code);
        Assert.NotEmpty(refused.Failure!.Details);
    }

    /// <summary>
    /// AUTHZ-MODEL-004 AC3: an entity the host queries and never declared has no
    /// policy, which a test enumerating the host's entities reads as a red build.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_004_AC3_AnEntityWithoutAPolicyIsFoundByEnumeration()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());
        HostDomain.Workspace workspace = HostDomain.NewWorkspace(Guid.CreateVersion7());
        HostDomain.Folder folder = HostDomain.NewFolder(workspace.Id, default);
        HostDomain.Article article = HostDomain.NewArticle(folder.Id, default);
        OtherDomain.Journey journey = OtherDomain.NewJourney(
            OtherDomain.NewVehicle(OtherDomain.NewDepot(Guid.CreateVersion7()).Id, default).Id);

        Assert.All(
            new object[] { workspace, folder, article },
            entity => Assert.NotNull(model.Find(entity.GetType())));
        Assert.Null(model.Find(journey.GetType()));
    }

    /// <summary>
    /// AUTHZ-GATE-001 AC3: the enumeration is over every type the gate would be asked
    /// about, and one the declaration does not name has no policy for the gate to read,
    /// so it fails here rather than at the first request.
    /// </summary>
    [Fact]
    public void AUTHZ_GATE_001_AC3_EveryQueryableEntityIsEnumeratedAgainstItsPolicy()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());

        Assert.NotEmpty(model.ResourceTypes);

        Assert.All(
            model.ResourceTypes,
            type =>
            {
                Assert.NotNull(model.Find(type.Name));
                Assert.NotNull(model.Find(type.Entity));
            });

        Assert.Null(model.Find(typeof(OtherDomain.Journey)));
        Assert.Null(model.Find(ResourceType.Parse("journey")));
    }

    /// <summary>
    /// AUTHZ-MODEL-006 AC1: nothing on the model changes it, so the declaration is
    /// what the deployment shipped and roles and grants are what change at runtime.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_006_AC1_NoRuntimeApiModifiesTheModel()
    {
        Assert.DoesNotContain(
            typeof(AuthorizationModel).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => method.ReturnType == typeof(void));

        Assert.All(
            typeof(AuthorizationDeclaration).GetProperties(),
            property => Assert.DoesNotContain(
                property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers() ?? [],
                modifier => !string.Equals(modifier.Name, "IsExternalInit", StringComparison.Ordinal)));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-001 AC1: a type that says nothing about what its refusals
    /// disclose conceals, because whatever a declaration omits is what most types will
    /// carry.
    /// </summary>
    [Fact]
    public void AUTHZ_CONCEAL_001_AC1_ATypeDeclaringNothingConceals()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());

        Assert.All(
            model.ResourceTypes,
            type => Assert.Equal(ConcealmentBehaviour.Conceal, type.Concealment));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-001 AC2: a type whose refusals say the record is there and is
    /// forbidden says so itself, in one place, for every record of that type
    /// (AUTHZ-CONCEAL-003).
    /// </summary>
    [Fact]
    public void AUTHZ_CONCEAL_001_AC2_DisclosingIsDeclaredAndNotInferred()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared()
            .Resource<HostDomain.Draft>("draft", draft => draft
                .ContainedIn("folder")
                .Discloses()
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Build());

        Assert.Equal(
            ConcealmentBehaviour.Disclose,
            model.Find(ResourceType.Parse("draft"))?.Concealment);
        Assert.Equal(
            ConcealmentBehaviour.Conceal,
            model.Find(ResourceType.Parse("article"))?.Concealment);
    }

    /// <summary>
    /// Containment is read from the model, outermost last, which is what inheritance
    /// walks and the only place it is written down.
    /// </summary>
    [Fact]
    public void Containment_ANestedType_CarriesItselfAndItsContainers()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());

        Assert.Equal(
            ["article", "folder", "workspace"],
            model.Containment(ResourceType.Parse("article")).Select(type => type.ToString()));
    }

    /// <summary>
    /// A permission the model does not declare is one no role may grant, and the
    /// library's own are declared without the host repeating them.
    /// </summary>
    [Fact]
    public void Declares_APermissionNoOneDeclared_IsNotDeclared()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());

        Assert.True(model.Declares(Permission.Parse("article:read")));
        Assert.True(model.Declares(Permissions.AuditRead));
        Assert.False(model.Declares(Permission.Parse("article:destroy")));
    }

    /// <summary>
    /// An action reads when it is named read, list or export or when the host declared
    /// it reading; every other action modifies, one nobody classified included.
    /// </summary>
    [Fact]
    public void IsReading_AnActionNobodyClassified_Modifies()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared()
            .Permission("article:preview", reading: true)
            .Build());

        Assert.True(model.IsReading(Permission.Parse("article:read")));
        Assert.True(model.IsReading(Permission.Parse("folder:list")));
        Assert.True(model.IsReading(Permission.Parse("article:export")));
        Assert.True(model.IsReading(Permission.Parse("article:preview")));
        Assert.False(model.IsReading(Permission.Parse("article:edit")));
        Assert.False(model.IsReading(Permission.Parse("article:archive")));
    }

    /// <summary>
    /// An action the host bound to a step-up gate names the gate; one bound to none
    /// names nothing, which is the whole of what the residual is read from.
    /// </summary>
    [Fact]
    public void GateOf_ABoundAction_NamesTheGateItIsBoundTo()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared()
            .Permission("article:publish")
            .StepUpGate("article:publish", "article:publish")
            .Build());

        Assert.Equal("article:publish", model.GateOf(Permission.Parse("article:publish")));
        Assert.Null(model.GateOf(Permission.Parse("article:read")));
    }

    /// <summary>
    /// OPS-ALERT-006 AC1: an export is an action the host declares by that name, so the
    /// set is read from the declaration and no action is one by what it returns.
    /// </summary>
    [Fact]
    public void OPS_ALERT_006_AC1_ExportIsAnEnumeratedSetOfOperations()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared()
            .Permission("article:export")
            .Permission("folder:export")
            .Permission("article:bulkread", reading: true)
            .Build());

        Assert.Equal(
            ["article:export", "folder:export"],
            model.Exports.Select(permission => permission.ToString()).Order(StringComparer.Ordinal));
        Assert.True(model.IsExport(Permission.Parse("article:export")));
        Assert.False(model.IsExport(Permission.Parse("article:read")));
        Assert.False(model.IsExport(Permission.Parse("article:bulkread")));
        Assert.False(model.IsExport(Permission.Parse("workspace:export")));
        Assert.DoesNotContain(model.Exports, Permissions.All.Contains);
    }

    /// <summary>
    /// CONV-NAME-002 AC1: a permission outside the pattern never reaches the model,
    /// because the declaration that names it is refused where it is written.
    /// </summary>
    /// <param name="permission">A string that is not a permission.</param>
    [Theory]
    [InlineData("Article:read")]
    [InlineData("article:Read")]
    [InlineData("article")]
    [InlineData("article:read:draft")]
    public void CONV_NAME_002_AC1_APermissionOutsideThePatternFailsModelValidation(string permission) =>
        Assert.Throws<ArgumentException>(
            () => new AuthorizationDeclarationBuilder().Permission(permission));

    // The builder takes an expression for each side of an encrypted field, so what it
    // produces always names members that exist. A host writing the declaration itself
    // does not, which is what the startup check is there for.
    private static AuthorizationDeclaration Encrypting(EncryptedFieldDeclaration field)
    {
        AuthorizationDeclaration declared = new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(Contract())
            .Resource<HostDomain.Article>("article", article => article
                .BelongsToOrganization()
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Build();

        return declared with
        {
            ResourceTypes = [declared.ResourceTypes[0] with { EncryptedFields = [field] }],
        };
    }

    // One purpose on the basis named, over a type whose encrypted field names its
    // subject, so a consent the purpose rests on has a subject to be read from and
    // nothing but the purpose and its basis is at issue.
    private static AuthorizationDeclaration Declaring(string purpose, string basis) =>
        new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(Contract())
            .LawfulBasis(new LawfulBasisDeclaration("consent", true, true, false, false))
            .Resource<HostDomain.Article>("article", article => article
                .BelongsToOrganization()
                .Purpose(purpose, basis, data: ["identity"])
                .Encrypted(item => item.Body, item => item.Author))
            .Build();

    private static AuthorizationDeclaration Malformed(int index) => index switch
    {
        0 => new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(Contract())
            .Resource<HostDomain.Folder>("folder", folder => folder
                .ContainedIn("article")
                .BelongsToOrganization()
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Resource<HostDomain.Article>("article", article => article
                .ContainedIn("folder")
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Build(),
        1 => new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(Contract())
            .Resource<HostDomain.Article>("article", article => article
                .ContainedIn("folder")
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Build(),
        2 => new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(Contract())
            .Resource<HostDomain.Article>("article", article => article
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Build(),
        3 => new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("interest", false, false, true, true))
            .Resource<HostDomain.Article>("article", article => article
                .BelongsToOrganization()
                .Purpose("fraud-prevention", "interest"))
            .Build(),
        _ => new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(Contract())
            .Resource<HostDomain.Article>("article", article => article
                .BelongsToOrganization()
                .Purpose("collaboration", "contract", data: ["identity"])
                .Derivation("reviewer", "reader"))
            .Build(),
    };

    private static LawfulBasisDeclaration Contract() => new("contract", false, false, false, false);

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
