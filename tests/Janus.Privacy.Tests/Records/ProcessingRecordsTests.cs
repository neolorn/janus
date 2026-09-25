using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Policies;
using Janus.Privacy.Records;
using Xunit;

namespace Janus.Privacy.Tests.Records;

/// <summary>
/// The records of processing: generated from the declaration, the configuration and
/// the roles, with what nobody can derive reported rather than left blank
/// (PRIV-PRIN-002, PRIV-ROPA-001, PRIV-ROPA-002, PRIV-ROPA-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class ProcessingRecordsTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Mona =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private static readonly OrganizationId Company =
        new(Guid.Parse("55555555-5555-4555-8555-555555555555"));

    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly ComplianceStoreInMemory _compliance = new();
    private readonly RegisterRolesInMemory _roles = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly FixedClock _clock = new(Noon);

    /// <summary>
    /// A caller who may read the register, in a deployment that has named where it is
    /// hosted.
    /// </summary>
    public ProcessingRecordsTests()
    {
        _administrative.Organization = Company;
        _gate.Grant(Mona, Company, Permissions.RecordsOfProcessingRead);
        _gate.Grant(Mona, Company, Permissions.ComplianceManage);

        _configuration.Set(Settings.HostingEnvironment, "a rented virtual machine");
        _configuration.Set(Settings.HostingLocation, HostingLocation.Inside);
    }

    /// <summary>
    /// PRIV-PRIN-002 AC1, PRIV-ROPA-001: a purpose added to the declaration is in the
    /// next register, with no separate edit anywhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_PRIN_002_AC1_AddingAPurposeChangesTheRegisterWithNoSeparateEditAsync()
    {
        ProcessingRegister before = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.DoesNotContain(
            "analytics",
            before.Records.Select(record => record.Purpose),
            StringComparer.Ordinal);

        AuthorizationDeclaration widened = Declaration.Declared()
            .Resource<Declaration.Mailing>("reading", reading => reading
                .BelongsToOrganization()
                .Purpose("analytics", "contract", data: ["identity"], subjects: ["customers"]))
            .Build();

        ProcessingRegister after = Generated(await Records(widened)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        ProcessingRecord added = Assert.Single(
            after.Records,
            record => record.Purpose is "analytics");

        Assert.Equal("contract", added.LawfulBasis);
        Assert.Equal(["identity"], added.DataCategories);
        Assert.Equal(["customers"], added.SubjectCategories);
        Assert.True(added.NonSensitive);
        Assert.False(added.Sensitive);
    }

    /// <summary>
    /// PRIV-SENS-002 AC4, PRIV-ROPA-001: the technical security measures column
    /// names one measure a threat, the controls a sensitive declaration derives and
    /// the keys no deployment can turn off through the application, rather than one
    /// phrase standing for all of them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC4_TheMeasuresAreNamedOneAThreatAsync()
    {
        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        IReadOnlyList<string> measures = register.Records[0].TechnicalSecurityMeasures;

        Assert.Equal(
            ["per-subject-field-encryption", "backup-encryption", "disk-encryption"],
            measures.Take(3));

        Assert.Equal(
            Settings.All
                .Where(setting => setting.Scope is SettingScope.Protected)
                .Select(setting => setting.Key.ToString())
                .Order(StringComparer.Ordinal),
            measures.Skip(3));

        Assert.NotEmpty(measures.Skip(3));

        Assert.All(register.Records, record => Assert.Equal(measures, record.TechnicalSecurityMeasures));
    }

    /// <summary>
    /// PRIV-PRIN-002 AC2: nothing of the inventory is kept. A purpose the declaration
    /// no longer carries leaves no row behind, and what survives a change of
    /// declaration is only the three fields no query can answer.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_PRIN_002_AC2_NoInventoryOutlivesTheDeclarationAsync()
    {
        AuthorizationDeclaration widened = Declaration.Declared()
            .Resource<Declaration.Mailing>("reading", reading => reading
                .BelongsToOrganization()
                .Purpose("analytics", "contract", data: ["identity"], subjects: ["customers"]))
            .Build();

        _ = Generated(await Records(widened)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.True((await Records(widened)
            .DeclareAsync(
                AccessContext.Of(Mona),
                new ComplianceRecord("the data protection officer", "annual training", []),
                TestContext.Current.CancellationToken))
            .Match(() => true, _ => false));

        ProcessingRegister narrowed = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.DoesNotContain(
            "analytics",
            narrowed.Records.Select(record => record.Purpose),
            StringComparer.Ordinal);

        Assert.Equal("the data protection officer", narrowed.DataOwner);
        Assert.Equal("annual training", narrowed.OrganisationalSecurityMeasures);
    }

    /// <summary>
    /// PRIV-SENS-001 AC2, PRIV-ROPA-001: sensitivity is a column of its own, so a
    /// purpose over a sensitive type is reported apart from one that is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_SENS_001_AC2_SensitivityIsAColumnOfItsOwnAsync()
    {
        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        ProcessingRecord performance = Row(register, "performance");
        ProcessingRecord marketing = Row(register, "marketing");

        Assert.True(performance.Sensitive);
        Assert.False(performance.NonSensitive);
        Assert.Equal(["financial"], performance.SensitiveCategories);

        Assert.False(marketing.Sensitive);
        Assert.True(marketing.NonSensitive);
        Assert.Empty(marketing.SensitiveCategories);

        // PRIV-SENS-001: no type here declares the children's category, so no row is
        // in the children's column.
        Assert.All(register.Records, record => Assert.False(record.Children));
    }

    /// <summary>
    /// PRIV-SENS-001 AC2, PRIV-ROPA-001: the children's column is a sensitivity
    /// category like any other, so a purpose is in it exactly where a type it is
    /// declared on declares that category, and the rest of the register is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_SENS_001_AC2_TheChildrensColumnFollowsTheDeclaredCategoryAsync()
    {
        ProcessingRegister register = Generated(await Records(Childrens())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.True(Row(register, "schooling").Children);
        Assert.False(Row(register, "marketing").Children);
        Assert.Equal(["children"], Row(register, "schooling").SensitiveCategories);
    }

    /// <summary>
    /// PRIV-ROPA-001, PRIV-MINOR-001: a deployment that admits minors and declares no
    /// type as children's data is told so on the register, because a register showing
    /// no children's processing at all is what a regulator would object to.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_ADeploymentAdmittingMinorsAndDeclaringNoneIsFlaggedAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Off);

        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Contains(
            register.Flags,
            flag => flag.Finding is RegisterFinding.ChildrenUndeclared);
        Assert.All(register.Records, record => Assert.False(record.Children));
    }

    /// <summary>
    /// PRIV-ROPA-001: the flag reports an absence, so a deployment admitting minors
    /// that does declare a children's type carries none, and so does one that admits
    /// no minors at all.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_ADeploymentThatDeclaredOneIsNotFlaggedAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Off);

        ProcessingRegister declared = Generated(await Records(Childrens())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Required);

        ProcessingRegister adults = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.DoesNotContain(
            declared.Flags,
            flag => flag.Finding is RegisterFinding.ChildrenUndeclared);
        Assert.DoesNotContain(
            adults.Flags,
            flag => flag.Finding is RegisterFinding.ChildrenUndeclared);
    }

    /// <summary>
    /// PRIV-ROPA-001 AC2: the three fields a person supplies are reported missing
    /// while they are missing, and carried once they are supplied.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_AC2_TheThreeSuppliedFieldsAreFlaggedWhileAbsentAsync()
    {
        ProcessingRecordsService records = Records(Declaration.Declared().Build());

        ProcessingRegister missing = Generated(await records
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(
            [
                RegisterFinding.DataOwnerMissing,
                RegisterFinding.OrganisationalMeasuresMissing,
                RegisterFinding.AssessmentLinksMissing,
            ],
            missing.Flags
                .Where(flag => flag.Subject.Length is 0)
                .Select(flag => flag.Finding));

        Assert.Equal(
            Result.Success(),
            await records.DeclareAsync(
                AccessContext.Of(Mona),
                new ComplianceRecord("the operations lead", "annual access review", ["LIA-1"]),
                TestContext.Current.CancellationToken));

        ProcessingRegister supplied = Generated(await records
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal("the operations lead", supplied.DataOwner);
        Assert.Equal("annual access review", supplied.OrganisationalSecurityMeasures);
        Assert.Equal(["LIA-1"], supplied.AssessmentLinks);
        Assert.DoesNotContain(supplied.Flags, flag => flag.Subject.Length is 0);
    }

    /// <summary>
    /// PRIV-ROPA-001 AC3: a purpose whose basis requires an assessment and names none
    /// is reported, and one that names its assessment carries it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_AC3_APurposeLackingItsAssessmentIsReportedAsync()
    {
        AuthorizationDeclaration widened = Declaration.Declared()
            .Resource<Declaration.Mailing>("signal", signal => signal
                .BelongsToOrganization()
                .Purpose("fraud", "interest", data: ["identity"], subjects: ["customers"]))
            .Build();

        ProcessingRegister register = Generated(await Records(widened)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(
            ["fraud"],
            register.Flags
                .Where(flag => flag.Finding is RegisterFinding.AssessmentMissing)
                .Select(flag => flag.Subject));

        Assert.Equal(
            "The abuse controls are assessed annually.",
            Row(register, "security").Assessment);
    }

    /// <summary>
    /// PRIV-ROPA-002 AC1, AC2: every recipient appears with its characterisation, and
    /// a processor without an agreement reference is reported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_002_AC1_EveryRecipientAppearsAndAProcessorWithoutAnAgreementIsFlaggedAsync()
    {
        AuthorizationDeclaration declared = Declaration.Declared()
            .Recipient(new RecipientDeclaration(
                "sms gateway",
                RecipientCharacterisation.Processor,
                ["phone number", "message text"],
                Location: null,
                "DPA-7",
                Callback: true))
            .Recipient(new RecipientDeclaration(
                "archive service",
                RecipientCharacterisation.Processor,
                ["name", "phone"],
                Location: null,
                AgreementReference: null,
                Callback: false))
            .Build();

        ProcessingRegister register = Generated(await Records(declared)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        // The hosting provider is applied beside the two declared rows because every
        // deployment is hosted somewhere; the screening service is applied because
        // the shipped default screens passwords online; the declared gateway stands
        // in place of the shipped one.
        Assert.Equal(
            ["sms gateway", "archive service", "hosting provider", "password screening"],
            register.Recipients.Select(recipient => recipient.Name));

        Assert.All(
            register.Recipients.Where(recipient => recipient.Name is not "password screening"),
            recipient => Assert.Equal(
                RecipientCharacterisation.Processor,
                recipient.Characterisation));

        Assert.Equal("DPA-7", register.Recipients[0].AgreementReference);

        Assert.Equal(
            ["archive service", "hosting provider"],
            register.Flags
                .Where(flag => flag.Finding is RegisterFinding.AgreementMissing)
                .Select(flag => flag.Subject));

        Assert.All(
            register.Records,
            record => Assert.Equal(
                ["sms gateway", "archive service", "hosting provider", "password screening"],
                record.Recipients));
    }

    /// <summary>
    /// INT-GEN-004 AC1: a provider the deployment adds without an agreement reference
    /// is flagged in the generated records, and one added with a reference is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_004_AC1_AProviderAddedWithoutAnAgreementReferenceIsFlaggedAsync()
    {
        AuthorizationDeclaration declared = Declaration.Declared()
            .Recipient(Processor("archive service", agreement: null))
            .Recipient(Processor("translation service", agreement: "  "))
            .Recipient(Processor("scanning service", agreement: "DPA-2026-11"))
            .Build();

        ProcessingRegister register = Generated(await Records(declared)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        IReadOnlyList<string> flagged =
        [
            .. register.Flags
                .Where(flag => flag.Finding is RegisterFinding.AgreementMissing)
                .Select(flag => flag.Subject),
        ];

        Assert.Contains("archive service", flagged);
        Assert.Contains("translation service", flagged);
        Assert.DoesNotContain("scanning service", flagged);
    }

    /// <summary>
    /// PRIV-ROPA-002: the rows the library itself makes true are in the register of a
    /// deployment that declared no recipient at all, because the library calls the
    /// mail server, the SMS gateway and the screening service and the deployment is
    /// hosted somewhere.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_002_TheRowsTheLibraryMakesTrueAreAppliedWithoutADeclarationAsync()
    {
        _configuration.Set(Settings.IntegrationMailEndpoint, "https://mail.example.test/api");

        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(
            ["mail server", "sms gateway", "hosting provider", "password screening"],
            register.Recipients.Select(recipient => recipient.Name));

        // Each ships with no agreement reference, so each processor among them is
        // flagged until the deployment gives it one.
        Assert.Equal(
            ["mail server", "sms gateway", "hosting provider"],
            register.Flags
                .Where(flag => flag.Finding is RegisterFinding.AgreementMissing)
                .Select(flag => flag.Subject));
    }

    /// <summary>
    /// PRIV-ROPA-002, D-165: the shipped register is the four rows the library's own
    /// processing makes true, in the order chapter 05 section 6 gives them, and no row
    /// of a host's business.
    /// </summary>
    [Fact]
    public void PRIV_ROPA_002_TheShippedRegisterIsTheFourRowsTheLibraryMakesTrue() =>
        Assert.Equal(
            ["mail server", "sms gateway", "hosting provider", "password screening"],
            ProviderRegister.Default.Select(row => row.Name));

    /// <summary>
    /// PRIV-ROPA-002: a deployment that calls neither a mail server nor the online
    /// screening service reports neither of them, so the register states what is true
    /// of that deployment and not of a shipped list.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_002_AnUncalledProviderIsNotInTheRegisterAsync()
    {
        _configuration.Set(Settings.PasswordBlocklistSource, BlocklistSource.SelfHosted);

        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(
            ["sms gateway", "hosting provider"],
            register.Recipients.Select(recipient => recipient.Name));
    }

    /// <summary>
    /// PRIV-ROPA-002: the shipped rows are defaults the host edits, so a deployment
    /// that declared one of the applied rows reports its own row in place of the
    /// default and not beside it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_002_AC2_AnEditedRowStandsInPlaceOfTheShippedDefaultAsync()
    {
        _configuration.Set(Settings.IntegrationMailEndpoint, "https://mail.example.test/api");

        AuthorizationDeclaration declared = Declaration.Declared()
            .Recipient(new RecipientDeclaration(
                "Mail server",
                RecipientCharacterisation.Processor,
                ["mailbox contents", "account identifiers"],
                Location: null,
                "DPA-3",
                Callback: false))
            .Build();

        ProcessingRegister register = Generated(await Records(declared)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(
            ["Mail server", "sms gateway", "hosting provider", "password screening"],
            register.Recipients.Select(recipient => recipient.Name));

        Assert.Equal(
            ["sms gateway", "hosting provider"],
            register.Flags
                .Where(flag => flag.Finding is RegisterFinding.AgreementMissing)
                .Select(flag => flag.Subject));
    }

    /// <summary>
    /// PRIV-ROPA-003 AC1: the compromised-password screening call is a recipient
    /// outside the country, and it appears with the basis the transfer stands on.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_003_AC1_ThePasswordScreeningCallAppearsAsACrossBorderTransferAsync()
    {
        _configuration.Set(Settings.HostingCrossBorderBasis, "the regulator's permit");

        AuthorizationDeclaration declared = Declaration.Declared()
            .Recipient(ProviderRegister.Default.Single(row => row.Name is "password screening"))
            .Recipient(ProviderRegister.Default.Single(row => row.Name is "hosting provider"))
            .Build();

        ProcessingRegister register = Generated(await Records(declared)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        RecipientRecord screening = Assert.Single(
            register.Recipients,
            recipient => recipient.Name is "password screening");

        Assert.Equal(RecipientCharacterisation.Recipient, screening.Characterisation);
        Assert.Equal(HostingLocation.Outside, screening.Location);
        Assert.Equal("the regulator's permit", screening.CrossBorderBasis);
        Assert.Equal(["hash prefix"], screening.DataReceived);

        // A recipient that follows the hosting is where the deployment is, and no
        // border is crossed to reach it.
        RecipientRecord hosting = Assert.Single(
            register.Recipients,
            recipient => recipient.Name is "hosting provider");

        Assert.Equal(HostingLocation.Inside, hosting.Location);
        Assert.Null(hosting.CrossBorderBasis);
    }

    /// <summary>
    /// PRIV-RET-001 AC3, PRIV-ROPA-001: the retention of each category the purpose
    /// is over is on the row, longest first, and a category the deployment declares
    /// none for is reported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_001_AC3_TheRetentionOfEachCategoryIsOnTheRowAsync()
    {
        _configuration.Set(Settings.HostCategoryRetention, "identity", TimeSpan.FromDays(365));
        _configuration.Set(Settings.HostCategoryRetention, "statement", TimeSpan.FromDays(1826));

        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(["statement P1826D", "identity P365D"], Row(register, "performance").Retention);
        Assert.DoesNotContain(
            register.Flags,
            flag => flag.Finding is RegisterFinding.RetentionMissing);

        _configuration.Clear(Settings.HostCategoryRetention.For("statement"));

        ProcessingRegister missing = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Contains(
            new RegisterFlag(RegisterFinding.RetentionMissing, "statement"),
            missing.Flags);
    }

    /// <summary>
    /// PRIV-ROPA-001: the roles with access to a purpose are the roles holding a
    /// permission declared to serve it, read as they stand.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_AC1_TheRolesWithAccessAreTheOnesHoldingAServingPermissionAsync()
    {
        _roles.Allows("support", "statement:read");
        _roles.Allows("auditor", "audit:read");

        AuthorizationDeclaration declared = Declaration.Declared()
            .ServesPurpose("statement:read", "performance")
            .Build();

        ProcessingRegister register = Generated(await Records(declared)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(["support"], Row(register, "performance").RolesWithAccess);
        Assert.Empty(Row(register, "marketing").RolesWithAccess);
    }

    /// <summary>
    /// The register is the whole of what the deployment processes about everyone, so
    /// it answers the permission and nobody else, and neither does a statement of the
    /// three supplied fields.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task GenerateAsync_WithoutThePermission_RefusesAndWritesNothingAsync()
    {
        var elsewhere = new SubjectId(Guid.Parse("66666666-6666-4666-8666-666666666666"));

        Assert.Equal(
            ErrorCodes.Denied,
            (await Records(Declaration.Declared().Build())
                .GenerateAsync(AccessContext.Of(elsewhere), TestContext.Current.CancellationToken))
                .Match(_ => default!, error => error.Code));

        Assert.Equal(
            ErrorCodes.Denied,
            (await Records(Declaration.Declared().Build())
                .DeclareAsync(
                    AccessContext.Of(elsewhere),
                    new ComplianceRecord("someone", "something", []),
                    TestContext.Current.CancellationToken))
                .Match(() => default!, error => error.Code));

        Assert.Null(_compliance.Held.DataOwner);
    }

    /// <summary>
    /// PRIV-BASIS-001 AC2: the lawful basis column is the label the deployment
    /// declared, so a deployment whose list reads differently emits its own words and
    /// the library contributes none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BASIS_001_AC2_TheBasisColumnCarriesTheDeclaredLabelAsync()
    {
        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal("contract", Row(register, "performance").LawfulBasis);
        Assert.Equal("agreement", Row(register, "marketing").LawfulBasis);

        AuthorizationDeclaration elsewhere = new AuthorizationDeclarationBuilder()
            .LawfulBasis(new LawfulBasisDeclaration("art-6-1-b", false, false, false, false))
            .Resource<Declaration.Mailing>("mailing", mailing => mailing
                .BelongsToOrganization()
                .Purpose("marketing", "art-6-1-b", data: ["identity"], subjects: ["customers"]))
            .Build();

        ProcessingRegister other = Generated(await Records(elsewhere)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal("art-6-1-b", Row(other, "marketing").LawfulBasis);
    }

    /// <summary>
    /// PRIV-BASIS-002 AC2: the assessment a purpose names is on its row, and a
    /// purpose whose basis requires none carries none rather than an empty reference
    /// nobody wrote.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BASIS_002_AC2_TheAssessmentReferenceIsOnTheRowAsync()
    {
        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(
            "The abuse controls are assessed annually.",
            Row(register, "security").Assessment);

        Assert.Null(Row(register, "performance").Assessment);
    }

    /// <summary>
    /// PRIV-RET-005 AC3: the session location and the sending-restriction record are
    /// in the register under the purposes the host declares for them, with the
    /// retention it named, because the library keeps no inventory of its own.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RET_005_AC3_TheTwoRecordsAppearUnderTheDeclaredPurposesAsync()
    {
        _configuration.Set(Settings.HostCategoryRetention, "session-location", TimeSpan.FromDays(30));
        _configuration.Set(Settings.HostCategoryRetention, "sending-restriction", TimeSpan.FromDays(1));

        AuthorizationDeclaration declared = Declaration.Declared()
            .Resource<Declaration.Mailing>("signing-in", signing => signing
                .BelongsToOrganization()
                .Purpose(
                    "session-management",
                    "contract",
                    data: ["session-location"],
                    subjects: ["customers"])
                .Purpose(
                    "abuse-prevention",
                    "interest",
                    assessment: "The abuse controls are assessed annually.",
                    data: ["sending-restriction"],
                    subjects: ["customers"]))
            .Build();

        ProcessingRegister register = Generated(await Records(declared)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(["session-location"], Row(register, "session-management").DataCategories);
        Assert.Equal(["session-location P30D"], Row(register, "session-management").Retention);
        Assert.Equal(["sending-restriction"], Row(register, "abuse-prevention").DataCategories);
        Assert.Equal(["sending-restriction P1D"], Row(register, "abuse-prevention").Retention);
    }

    private static ProcessingRegister Generated(Result<ProcessingRegister> outcome) =>
        outcome.Match(
            register => register,
            error => throw new InvalidOperationException(error.Code.ToString()));

    // A deployment that declares one of its types as children's data, which is the
    // category the register's children's column reports (PRIV-SENS-001).
    private static AuthorizationDeclaration Childrens() =>
        Declaration.Declared()
            .SensitiveCategory("children")
            .Resource<Declaration.Enrolment>("enrolment", enrolment => enrolment
                .BelongsToOrganization()
                .Sensitive("children")
                .Purpose("schooling", "contract", data: ["identity"], subjects: ["pupils"]))
            .Build();

    private static ProcessingRecord Row(ProcessingRegister register, string purpose) =>
        Assert.Single(register.Records, record => record.Purpose == purpose);

    private static RecipientDeclaration Processor(string name, string? agreement) =>
        new(
            name,
            RecipientCharacterisation.Processor,
            ["name"],
            Location: null,
            agreement,
            Callback: false);

    private ProcessingRecordsService Records(AuthorizationDeclaration declaration) =>
        new(
            new AdministrativeScope(_gate, _administrative),
            DeclaredProcessing.Of(declaration),
            declaration,
            _compliance,
            _roles,
            _configuration,
            _clock);
}
