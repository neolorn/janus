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
/// AUTHZ-CONCEAL-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class AuthorizationModelTests
{
    private static readonly string[] HostTypeNames =
    [
        nameof(HostDomain.Workspace),
        nameof(HostDomain.Folder),
        nameof(HostDomain.Document),
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
            ["workspace", "folder", "document"],
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
        ResourceTypeDeclaration document = declared.ResourceTypes.Single(
            type => type.Name == ResourceType.Parse("document"));

        Assert.Equal(
            new EncryptedFieldDeclaration(
                nameof(HostDomain.Document.Body),
                nameof(HostDomain.Document.Author)),
            document.EncryptedFields.Single());
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
        HostDomain.Document document = HostDomain.NewDocument(folder.Id, default);
        OtherDomain.Journey journey = OtherDomain.NewJourney(
            OtherDomain.NewVehicle(OtherDomain.NewDepot(Guid.CreateVersion7()).Id, default).Id);

        Assert.All(
            new object[] { workspace, folder, document },
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
            model.Find(ResourceType.Parse("document"))?.Concealment);
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
            ["document", "folder", "workspace"],
            model.Containment(ResourceType.Parse("document")).Select(type => type.ToString()));
    }

    /// <summary>
    /// A permission the model does not declare is one no role may grant, and the
    /// library's own are declared without the host repeating them.
    /// </summary>
    [Fact]
    public void Declares_APermissionNoOneDeclared_IsNotDeclared()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared().Build());

        Assert.True(model.Declares(Permission.Parse("document:read")));
        Assert.True(model.Declares(Permissions.AuditRead));
        Assert.False(model.Declares(Permission.Parse("document:destroy")));
    }

    /// <summary>
    /// An action reads when it is named read, list or export or when the host declared
    /// it reading; every other action modifies, one nobody classified included.
    /// </summary>
    [Fact]
    public void IsReading_AnActionNobodyClassified_Modifies()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared()
            .Permission("document:preview", reading: true)
            .Build());

        Assert.True(model.IsReading(Permission.Parse("document:read")));
        Assert.True(model.IsReading(Permission.Parse("folder:list")));
        Assert.True(model.IsReading(Permission.Parse("document:export")));
        Assert.True(model.IsReading(Permission.Parse("document:preview")));
        Assert.False(model.IsReading(Permission.Parse("document:edit")));
        Assert.False(model.IsReading(Permission.Parse("document:archive")));
    }

    /// <summary>
    /// An action the host bound to a step-up gate names the gate; one bound to none
    /// names nothing, which is the whole of what the residual is read from.
    /// </summary>
    [Fact]
    public void GateOf_ABoundAction_NamesTheGateItIsBoundTo()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared()
            .Permission("document:publish")
            .StepUpGate("document:publish", "document:publish")
            .Build());

        Assert.Equal("document:publish", model.GateOf(Permission.Parse("document:publish")));
        Assert.Null(model.GateOf(Permission.Parse("document:read")));
    }

    /// <summary>
    /// CONV-NAME-002 AC1: a permission outside the pattern never reaches the model,
    /// because the declaration that names it is refused where it is written.
    /// </summary>
    /// <param name="permission">A string that is not a permission.</param>
    [Theory]
    [InlineData("Document:read")]
    [InlineData("document:Read")]
    [InlineData("document")]
    [InlineData("document:read:draft")]
    public void CONV_NAME_002_AC1_APermissionOutsideThePatternFailsModelValidation(string permission) =>
        Assert.Throws<ArgumentException>(
            () => new AuthorizationDeclarationBuilder().Permission(permission));

    private static AuthorizationDeclaration Malformed(int index) => index switch
    {
        0 => new AuthorizationDeclarationBuilder()
            .LawfulBasis(Contract())
            .Resource<HostDomain.Folder>("folder", folder => folder
                .ContainedIn("document")
                .BelongsToOrganization()
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Resource<HostDomain.Document>("document", document => document
                .ContainedIn("folder")
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Build(),
        1 => new AuthorizationDeclarationBuilder()
            .LawfulBasis(Contract())
            .Resource<HostDomain.Document>("document", document => document
                .ContainedIn("folder")
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Build(),
        2 => new AuthorizationDeclarationBuilder()
            .LawfulBasis(Contract())
            .Resource<HostDomain.Document>("document", document => document
                .Purpose("collaboration", "contract", data: ["identity"]))
            .Build(),
        3 => new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("interest", false, false, true, true))
            .Resource<HostDomain.Document>("document", document => document
                .BelongsToOrganization()
                .Purpose("fraud-prevention", "interest"))
            .Build(),
        _ => new AuthorizationDeclarationBuilder()
            .LawfulBasis(Contract())
            .Resource<HostDomain.Document>("document", document => document
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
