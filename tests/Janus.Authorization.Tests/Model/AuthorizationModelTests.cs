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
/// AUTHZ-CONCEAL-001, PRIV-RIGHT-005a, OPS-ALERT-006, INT-HOST-002, IDN-PRIN-001).
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
                nameof(HostDomain.Article.Author),
                "content",
                nameof(HostDomain.Article.Body)),
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
    /// consent basis stops the deployment with <c>model.purpose.hostingconsent</c>,
    /// naming the type and the purpose, however it is spaced, cased or punctuated, so
    /// no consent record can ever reference it. The same purpose on another basis
    /// builds, and so does another purpose on consent, so the refusal is about the two
    /// together.
    /// </summary>
    /// <param name="purpose">The purpose the deployment declares.</param>
    [Theory]
    [InlineData("hosting")]
    [InlineData("Hosting")]
    [InlineData("transfer")]
    [InlineData("hosting-transfer")]
    [InlineData("cross-border-transfer")]
    [InlineData("Cross Border Transfer")]
    [InlineData("hosting_transfer")]
    public void INT_HOST_002_AC1_AConsentPurposeForTheHostingFailsStartup(string purpose)
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Declaring(purpose, "consent")));

        Assert.Equal(ErrorCodes.StartupHostingConsent, refused.Failure?.Code);
        Assert.Equal("article." + purpose, refused.Failure!.Details["key"].GetString());

        Assert.NotNull(AuthorizationModel.Of(Declaring(purpose, "contract")));
        Assert.NotNull(AuthorizationModel.Of(Declaring("newsletter", "consent")));
    }

    /// <summary>
    /// INT-HOST-002 AC3: each of the four names of the hosting or its transfer, on
    /// consent, stops the start with <c>model.purpose.hostingconsent</c> naming the
    /// type and the purpose as declared, whatever its case and punctuation; the same
    /// purpose on another basis starts.
    /// </summary>
    /// <param name="purpose">The purpose the deployment declares.</param>
    [Theory]
    [InlineData("hosting")]
    [InlineData("transfer")]
    [InlineData("hosting-transfer")]
    [InlineData("cross-border-transfer")]
    [InlineData("HOSTING")]
    [InlineData("Cross.Border Transfer")]
    public void INT_HOST_002_AC3_AConsentPurposeNamedForTheHostingOrItsTransferFailsStartup(string purpose)
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Declaring(purpose, "consent")));

        Assert.Equal("model.purpose.hostingconsent", refused.Failure?.Code.ToString());
        Assert.Equal("article." + purpose, refused.Failure!.Details["key"].GetString());
        Assert.NotNull(AuthorizationModel.Of(Declaring(purpose, "contract")));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC1: an encrypted field naming no subject column is ciphertext
    /// no erasure could reach, so the deployment stops rather than start with it.
    /// </summary>
    [Fact]
    public void PRIV_RIGHT_005a_AC1_AnEncryptedFieldNamingNoSubjectColumnFailsStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Encrypting(new EncryptedFieldDeclaration("Body", "", "identity", "Body"))));

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
            () => AuthorizationModel.Of(Encrypting(new EncryptedFieldDeclaration("Body", column, "identity", "Body"))));

        Assert.NotNull(AuthorizationModel.Of(Encrypting(new EncryptedFieldDeclaration("Body", "Author", "identity", "Body"))));
    }

    /// <summary>
    /// PRIV-PRIN-001 AC2: an encrypted field holds a data category a purpose on its type
    /// names, so one holding a category no purpose there names stops the deployment
    /// with <c>model.startup.declarationinvalid</c>, naming the type and the field, and
    /// one whose category a purpose names starts.
    /// </summary>
    [Fact]
    public void PRIV_PRIN_001_AC2_AnEncryptedFieldWhoseCategoryNoPurposeNamesFailsStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Encrypting(new EncryptedFieldDeclaration("Body", "Author", "health", "Body"))));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("article", refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("Body", refused.Failure?.Details["field"].GetString());
        Assert.NotNull(AuthorizationModel.Of(Encrypting(new EncryptedFieldDeclaration("Body", "Author", "identity", "Body"))));
    }

    /// <summary>
    /// PRIV-RIGHT-005a AC22 (D-206): a host's value is bound to its type's and its field's
    /// declared names, so two encrypted fields of one type under one declared name, the
    /// name one is given or the member's own another is given, stop the deployment with
    /// <c>model.startup.declarationinvalid</c>, naming the type and the name; the same
    /// fields under names of their own build.
    /// </summary>
    /// <param name="first">The name the first field is bound to: its member's own, or one it is given.</param>
    /// <param name="given">The name the second field is given.</param>
    [Theory]
    [InlineData("notes", "notes")]
    [InlineData("Body", "Body")]
    public void PRIV_RIGHT_005a_AC22_TwoEncryptedFieldsOfOneTypeUnderOneNameFailStartup(string first, string given)
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Encrypting(
                new EncryptedFieldDeclaration("Body", "Author", "identity", first),
                new EncryptedFieldDeclaration("FolderId", "Author", "identity", given))));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("article", refused.Failure?.Details["declaration"].GetString());
        Assert.Equal(given, refused.Failure?.Details["field"].GetString());

        Assert.NotNull(AuthorizationModel.Of(Encrypting(
            new EncryptedFieldDeclaration("Body", "Author", "identity", first),
            new EncryptedFieldDeclaration("FolderId", "Author", "identity", "FolderId"))));
    }

    /// <summary>
    /// BFF-CAP-002 AC4 (D-202): a type declaring as a capability a permission the model
    /// does not declare stops the deployment with <c>model.startup.declarationinvalid</c>,
    /// naming the type and <c>capabilities</c>, and one whose capabilities are declared
    /// permissions starts with them.
    /// </summary>
    [Fact]
    public void BFF_CAP_002_AC4_ACapabilityTheModelDoesNotDeclareFailsStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Capable("article:read", "article:publish")));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("draft", refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("capabilities", refused.Failure?.Details["field"].GetString());
        Assert.Equal(
            [Permission.Parse("article:read"), Permission.Parse("article:edit")],
            AuthorizationModel.Of(Capable("article:read", "article:edit"))
                .Find(ResourceType.Parse("draft"))?.Capabilities);
    }

    /// <summary>
    /// BFF-CAP-002 AC4 (D-206): a type declaring as capabilities two permissions of one
    /// action stops the deployment with <c>model.startup.declarationinvalid</c>, naming
    /// the type and <c>capabilities</c>, since the browser reads a capability by its
    /// action alone; one permission named twice is still one capability.
    /// </summary>
    [Fact]
    public void BFF_CAP_002_AC4_TwoCapabilitiesOfOneActionFailStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Capable("article:read", "folder:read")));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("draft", refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("capabilities", refused.Failure?.Details["field"].GetString());
        Assert.NotNull(AuthorizationModel.Of(Capable("article:read", "article:read", "article:edit")));
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC7 (D-204): a declared role named as a role chapter 10 section 3
    /// seeds stops the deployment with <c>model.startup.declarationinvalid</c>, naming
    /// <c>role.&lt;name&gt;</c> and <c>name</c>.
    /// </summary>
    /// <param name="name">The administrative role's name.</param>
    [Theory]
    [InlineData("system-administrator")]
    [InlineData("auditor")]
    [InlineData("support")]
    public void AUTHZ_GRANT_004_AC7_ADeclaredRoleNamedAsAnAdministrativeRoleFailsStartup(string name)
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(HostDomain.Declared().Role(name, "article:read").Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("role." + name, refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("name", refused.Failure?.Details["field"].GetString());
    }

    /// <summary>
    /// AUTHZ-GRANT-004, AUTHZ-MODEL-004 (D-204): a declared role naming a permission the
    /// model does not declare, or <c>system:administer</c>, is refused naming
    /// <c>permissions</c>; one whose name breaks the rule of <c>POST /admin/roles</c>, or
    /// declared twice, naming <c>name</c>; each with <c>role.&lt;name&gt;</c>.
    /// </summary>
    /// <param name="first">The first role's name.</param>
    /// <param name="permission">The permission it grants.</param>
    /// <param name="second">A second role's name, or nothing where there is none.</param>
    /// <param name="field">The field the refusal names.</param>
    [Theory]
    [InlineData("editor", "ledger:close", null, "permissions")]
    [InlineData("editor", "system:administer", null, "permissions")]
    [InlineData("Editor", "article:read", null, "name")]
    [InlineData("editor--chief", "article:read", null, "name")]
    [InlineData("editor", "article:read", "editor", "name")]
    public void AUTHZ_GRANT_004_ADeclaredRoleOutsideItsRuleFailsStartup(
        string first,
        string permission,
        string? second,
        string field)
    {
        AuthorizationDeclarationBuilder declared = HostDomain.Declared().Role(first, "article:edit", permission);

        if (second is not null)
        {
            declared.Role(second, "article:read");
        }

        StartupException refused = Assert.Throws<StartupException>(() => AuthorizationModel.Of(declared.Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("role." + (second ?? first), refused.Failure?.Details["declaration"].GetString());
        Assert.Equal(field, refused.Failure?.Details["field"].GetString());
    }

    /// <summary>
    /// AUTHZ-GRANT-004 (D-204): a declared role may grant the host's permissions and the
    /// library's own short of system administration, and the model keeps the declared
    /// roles in the order declared.
    /// </summary>
    [Fact]
    public void AUTHZ_GRANT_004_TheModelKeepsTheDeclaredRolesInOrder()
    {
        var model = AuthorizationModel.Of(HostDomain.Declared()
            .Role("writer", "article:read", "article:edit", "article:edit")
            .Role("reviewer", "article:read", Permissions.AuditRead.ToString())
            .Build());

        Assert.Equal(
            [
                (RoleName.Parse("writer"), "article:edit article:read"),
                (RoleName.Parse("reviewer"), "article:read audit:read"),
            ],
            model.DeclaredRoles.Select(role => (
                role.Name,
                string.Join(' ', role.Permissions.Select(held => held.ToString()).Order(StringComparer.Ordinal)))));
    }

    /// <summary>
    /// AUTHZ-GRANT-004, AUTHZ-GRANT-005 (D-212): a declared role hands out the roles it
    /// names where each is declared or a role chapter 10 section 3 seeds; one naming any
    /// other stops the deployment with <c>model.startup.declarationinvalid</c>, naming
    /// <c>role.&lt;name&gt;</c> and <c>handsOut</c>.
    /// </summary>
    /// <param name="handed">The role the declared role hands out.</param>
    /// <param name="admitted">Whether the declaration starts.</param>
    [Theory]
    [InlineData("writer", true)]
    [InlineData("auditor", true)]
    [InlineData("system-administrator", true)]
    [InlineData("accountant", false)]
    [InlineData("Writer", false)]
    public void AUTHZ_GRANT_005_ADeclaredRoleHandsOutOnlyARoleTheDeploymentWillHold(string handed, bool admitted)
    {
        AuthorizationDeclaration declaration = HostDomain.Declared()
            .Role("writer", "article:read")
            .Role("lead", ["article:read"], [handed])
            .Build();

        if (admitted)
        {
            DefinedRole lead = AuthorizationModel.Of(declaration).DeclaredRoles[^1];

            Assert.Equal([RoleName.Parse(handed)], lead.HandsOut);

            return;
        }

        StartupException refused = Assert.Throws<StartupException>(() => AuthorizationModel.Of(declaration));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("role.lead", refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("handsOut", refused.Failure?.Details["field"].GetString());
    }

    /// <summary>
    /// IDN-PRIN-001 AC7, IDN-READ-004 AC3, OPS-ALERT-006 (D-202, D-218, D-221): a job
    /// declared with a permission the model does not declare, a library-owned one other
    /// than the four reads of IDN-READ-004, <c>record:read</c> and <c>record:erase</c>, one
    /// bound to a step-up gate or one whose action is <c>export</c> stops the deployment
    /// with <c>model.startup.declarationinvalid</c>, naming <c>job.&lt;name&gt;</c> and
    /// <c>permissions</c>.
    /// </summary>
    /// <param name="permission">The permission the job is declared with.</param>
    [Theory]
    [InlineData("ledger:close")]
    [InlineData("audit:read")]
    [InlineData("account:read")]
    [InlineData("account:manage")]
    [InlineData("system:administer")]
    [InlineData("record:create")]
    [InlineData("record:manage")]
    [InlineData("article:publish")]
    [InlineData("article:export")]
    public void IDN_PRIN_001_AC7_AJobDeclaredWithAPermissionItMayNotExerciseFailsStartup(string permission)
    {
        StartupException refused = Assert.Throws<StartupException>(() => AuthorizationModel.Of(Jobbed()
            .Job("nightly-digest", "article:read", permission)
            .Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("job.nightly-digest", refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("permissions", refused.Failure?.Details["field"].GetString());
    }

    /// <summary>
    /// IDN-PRIN-001 AC7 (D-206): a job declared under a principal's name of chapter 10
    /// section 5.29, or under <c>migrate</c>, stops the deployment with
    /// <c>model.startup.declarationinvalid</c>, naming <c>job.&lt;name&gt;</c> and
    /// <c>name</c>.
    /// </summary>
    /// <param name="name">The reserved name.</param>
    [Theory]
    [InlineData("expiry-sweep")]
    [InlineData("registration-sweep")]
    [InlineData("invitation-sweep")]
    [InlineData("account-deletion")]
    [InlineData("organization-erasure")]
    [InlineData("privacy-deadlines")]
    [InlineData("loss-reports")]
    [InlineData("recovery-code-reminder")]
    [InlineData("domain-reverification")]
    [InlineData("membership-ends")]
    [InlineData("outbox")]
    [InlineData("events")]
    [InlineData("sends")]
    [InlineData("mailbox-provisioning")]
    [InlineData("alert-dispatch")]
    [InlineData("mail-reconciliation")]
    [InlineData("derivation-driftcheck")]
    [InlineData("location-database")]
    [InlineData("datacenter-ranges")]
    [InlineData("read-volume-baseline")]
    [InlineData("holiday-list")]
    [InlineData("emergency-credential")]
    [InlineData("restore-test")]
    [InlineData("audit-partitions")]
    [InlineData("licence-expiry")]
    [InlineData("envelope-rotation")]
    [InlineData("sms-balance")]
    [InlineData("bootstrap")]
    [InlineData("configure")]
    [InlineData("register-client")]
    [InlineData("rotate-kek")]
    [InlineData("rotate-fingerprint-key")]
    [InlineData("replay-erasures")]
    [InlineData("role-declaration")]
    [InlineData("migrate")]
    public void IDN_PRIN_001_AC7_AJobDeclaredUnderAReservedNameFailsStartup(string name)
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Jobbed().Job(name, "article:read").Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("job." + name, refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("name", refused.Failure?.Details["field"].GetString());
    }

    /// <summary>
    /// IDN-PRIN-001, LIB-HOST-001 (D-206): a job declared twice under one name stops the
    /// deployment with <c>model.startup.declarationinvalid</c>, naming
    /// <c>job.&lt;name&gt;</c> and <c>name</c>.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_AJobDeclaredTwiceFailsStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(() => AuthorizationModel.Of(Jobbed()
            .Job("nightly-digest", "article:read")
            .Job("nightly-digest", "article:edit")
            .Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("job.nightly-digest", refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("name", refused.Failure?.Details["field"].GetString());
    }

    /// <summary>
    /// IDN-PRIN-001, LIB-HOST-001 (D-202): jobs declared with the host permissions they
    /// exercise, a reading and a modifying one among them, build.
    /// </summary>
    [Fact]
    public void IDN_PRIN_001_JobsDeclaredWithinTheirRuleBuild()
    {
        Assert.Null(Record.Exception(() => AuthorizationModel.Of(Jobbed()
            .Job("nightly-digest", "article:read", "article:edit")
            .Job("archive", "article:read")
            .Build())));
    }

    /// <summary>
    /// IDN-READ-004 AC3 (D-218, D-221): a job declares the four reads of the library,
    /// <c>record:read</c> and <c>record:erase</c> beside its host permissions, and the
    /// model then has it exercise each.
    /// </summary>
    /// <param name="permission">The library-owned permission the job declares.</param>
    [Theory]
    [InlineData("organization:read")]
    [InlineData("membership:read")]
    [InlineData("contact:read")]
    [InlineData("consent:read")]
    [InlineData("record:read")]
    [InlineData("record:erase")]
    public void IDN_READ_004_AC3_AJobDeclaresTheLibraryOwnedPermissionsAJobMayExercise(string permission)
    {
        var model = AuthorizationModel.Of(Jobbed()
            .Job("customer-letters", "article:read", permission)
            .Build());

        Assert.True(model.Exercises("customer-letters", Permission.Parse(permission)));
    }

    /// <summary>
    /// AUTHZ-GATE-005 AC6 (D-209): a host binds only its own actions, so a step-up gate
    /// bound to a permission of chapter 10 section 2.1 stops the deployment with
    /// <c>model.startup.declarationinvalid</c>, naming <c>binding.&lt;permission&gt;</c>
    /// and <c>stepUpGate</c>, whatever the gate's name.
    /// </summary>
    /// <param name="permission">The library's permission the gate is bound to.</param>
    /// <param name="gate">The gate's name.</param>
    [Theory]
    [InlineData("system:administer", "config:loosen")]
    [InlineData("grant:manage", "grant:manage")]
    [InlineData("audit:read", "reading-the-trail")]
    public void AUTHZ_GATE_005_AC6_AStepUpGateBoundToALibraryOwnedPermissionFailsStartup(string permission, string gate)
    {
        StartupException refused = Assert.Throws<StartupException>(() => AuthorizationModel.Of(HostDomain.Declared()
            .StepUpGate(permission, gate)
            .Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("binding." + permission, refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("stepUpGate", refused.Failure?.Details["field"].GetString());
    }

    /// <summary>
    /// AUTHZ-GATE-005 AC6 (D-209): a purpose bound to a permission of chapter 10 section
    /// 2.1 stops the deployment with <c>model.startup.declarationinvalid</c>, naming
    /// <c>binding.&lt;permission&gt;</c> and <c>purpose</c>, the purpose being one a type
    /// declares.
    /// </summary>
    /// <param name="permission">The library's permission the purpose is bound to.</param>
    [Theory]
    [InlineData("system:administer")]
    [InlineData("membership:manage")]
    [InlineData("grant:read")]
    public void AUTHZ_GATE_005_AC6_APurposeBoundToALibraryOwnedPermissionFailsStartup(string permission)
    {
        StartupException refused = Assert.Throws<StartupException>(() => AuthorizationModel.Of(HostDomain.Declared()
            .ServesPurpose(permission, "collaboration")
            .Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("binding." + permission, refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("purpose", refused.Failure?.Details["field"].GetString());
    }

    /// <summary>
    /// AUTHZ-MODEL-004 AC1 (D-209): the binding to a library-owned permission is its own
    /// condition, named apart from the others: a host's own action bound to a gate and a
    /// purpose builds beside it, and the refusal names the library's permission alone.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_004_AC1_ABindingToALibraryOwnedPermissionIsNamedByItsDeclarationAndField()
    {
        AuthorizationDeclarationBuilder own = HostDomain.Declared()
            .StepUpGate("article:edit", "editing")
            .ServesPurpose("article:edit", "collaboration");

        Assert.Equal("editing", AuthorizationModel.Of(own.Build()).GateOf(Permission.Parse("article:edit")));

        StartupException gated = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(own.StepUpGate("role:manage", "editing").Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, gated.Failure?.Code);
        Assert.Equal<string>(["declaration", "field"], gated.Failure!.Details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("binding.role:manage", gated.Failure.Details["declaration"].GetString());
        Assert.Equal("stepUpGate", gated.Failure.Details["field"].GetString());
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
    /// AUTHZ-MODEL-004, D-166: the library names the whole organization
    /// <c>organization</c>, so a host type by that name stops the deployment, naming
    /// the type; the same declaration under another name builds.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_004_ATypeNamedOrganizationFailsStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Naming("organization")));

        Assert.Equal(ErrorCodes.StartupTypeReserved, refused.Failure?.Code);
        Assert.Equal("organization", refused.Failure!.Details["key"].GetString());
        Assert.NotNull(AuthorizationModel.Of(Naming("draft")));

        static AuthorizationDeclaration Naming(string type) =>
            HostDomain.Declared()
                .Resource<HostDomain.Draft>(type, draft => draft
                    .ContainedIn("folder")
                    .Purpose("collaboration", "contract", data: ["identity"]))
                .Build();
    }

    /// <summary>
    /// AUTHZ-MODEL-004 AC1 (D-206): a host's encrypted value is bound to its type's
    /// declared name, so a resource type declared twice under one name, over another
    /// entity or the same one, stops the deployment with
    /// <c>model.startup.declarationinvalid</c>, naming the type and <c>name</c>; the
    /// second declaration under a name of its own builds.
    /// </summary>
    [Fact]
    public void AUTHZ_MODEL_004_AC1_AResourceTypeDeclaredTwiceUnderOneNameFailsStartup()
    {
        StartupException refused = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(Twice("article")));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused.Failure?.Code);
        Assert.Equal("article", refused.Failure?.Details["declaration"].GetString());
        Assert.Equal("name", refused.Failure?.Details["field"].GetString());

        StartupException again = Assert.Throws<StartupException>(
            () => AuthorizationModel.Of(HostDomain.Declared()
                .Resource<HostDomain.Folder>("folder", folder => folder
                    .ContainedIn("workspace")
                    .Purpose("collaboration", "contract", data: ["identity"]))
                .Build()));

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, again.Failure?.Code);
        Assert.Equal("folder", again.Failure?.Details["declaration"].GetString());
        Assert.Equal("name", again.Failure?.Details["field"].GetString());

        Assert.NotNull(AuthorizationModel.Of(Twice("draft")));

        static AuthorizationDeclaration Twice(string type) =>
            HostDomain.Declared()
                .Resource<HostDomain.Draft>(type, draft => draft
                    .ContainedIn("folder")
                    .Purpose("collaboration", "contract", data: ["identity"]))
                .Build();
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

    // The domain with an action bound to a step-up gate and an export beside its two
    // permissions, which a job may not be declared with.
    private static AuthorizationDeclarationBuilder Jobbed() =>
        HostDomain.Declared()
            .Permission("article:publish")
            .StepUpGate("article:publish", "publishing")
            .Permission("article:export");

    // The builder takes an expression for each side of an encrypted field, so what it
    // produces always names members that exist. A host writing the declaration itself
    // does not, which is what the startup check is there for.
    private static AuthorizationDeclaration Capable(params string[] capabilities) =>
        HostDomain.Declared()
            .Permission("folder:read")
            .Resource<HostDomain.Draft>("draft", draft => draft
                .ContainedIn("folder")
                .Purpose("collaboration", "contract", data: ["identity"])
                .Capabilities(capabilities))
            .Build();

    private static AuthorizationDeclaration Encrypting(params EncryptedFieldDeclaration[] fields)
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
            ResourceTypes = [declared.ResourceTypes[0] with { EncryptedFields = fields }],
        };
    }

    // One purpose on the basis named, over a type whose encrypted field names its
    // subject, so a consent the purpose rests on has a subject to be read from and
    // nothing but the purpose and its basis is at issue.
    private static AuthorizationDeclaration Declaring(string purpose, string basis) =>
        new AuthorizationDeclarationBuilder()
            .RetentionFloor("identity", TimeSpan.FromDays(365))
            .LawfulBasis(Contract())
            .LawfulBasis(new LawfulBasisDeclaration("consent", "Consent", true, true, false, false))
            .Resource<HostDomain.Article>("article", article => article
                .BelongsToOrganization()
                .Purpose(purpose, basis, data: ["identity"])
                .Encrypted(item => item.Body, item => item.Author, "identity"))
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
            .LawfulBasis(new LawfulBasisDeclaration("interest", "Interest", false, false, true, true))
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

    /// <summary>
    /// PRIV-BASIS-001 (D-183): a lawful basis list that names one key twice, or a basis
    /// with an empty key or label, fails startup naming the list and the member.
    /// </summary>
    /// <param name="key">The key the second basis is declared under.</param>
    /// <param name="label">Its label.</param>
    /// <param name="field">The member the refusal names.</param>
    [Theory]
    [InlineData("contract", "Another contract", "key")]
    [InlineData("", "No key", "key")]
    [InlineData(" ", "No key", "key")]
    [InlineData("interest", "", "label")]
    [InlineData("interest", "  ", "label")]
    public void PRIV_BASIS_001_AListNamingAKeyTwiceOrAnEmptyKeyOrLabelFailsStartup(
        string key,
        string label,
        string field)
    {
        AuthorizationDeclaration declaration = new AuthorizationDeclarationBuilder()
            .LawfulBasis(Contract())
            .LawfulBasis(new LawfulBasisDeclaration(key, label, false, false, false, false))
            .Build();

        Error? refused = Assert.Throws<StartupException>(() => AuthorizationModel.Of(declaration)).Failure;

        Assert.Equal(ErrorCodes.StartupDeclarationInvalid, refused?.Code);
        Assert.Equal("lawfulBases", refused?.Details["declaration"].GetString());
        Assert.Equal(field, refused?.Details["field"].GetString());
    }

    private static LawfulBasisDeclaration Contract() => new("contract", "Contract", false, false, false, false);

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
