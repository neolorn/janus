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
    private readonly MembershipLookupInMemory _memberships = new();
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
        _memberships.Add(Mona, Company);
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
    /// PRIV-ROPA-001: the technical security measures column is the controls a
    /// sensitive declaration derives and the keys no deployment can turn off through
    /// the application, so it cannot report a control the deployment does not have.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_TheTechnicalMeasuresAreTheControlsAndTheProtectedKeysAsync()
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
    public async Task PRIV_ROPA_001_SensitivityIsAColumnOfItsOwnAsync()
    {
        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        ProcessingRecord fulfilment = Row(register, "fulfilment");
        ProcessingRecord marketing = Row(register, "marketing");

        Assert.True(fulfilment.Sensitive);
        Assert.False(fulfilment.NonSensitive);
        Assert.Equal(["financial"], fulfilment.SensitiveCategories);

        Assert.False(marketing.Sensitive);
        Assert.True(marketing.NonSensitive);
        Assert.Empty(marketing.SensitiveCategories);

        // PRIV-MINOR-001: the affirmation is required by default, so the service is
        // 18+ only and no row is in the children's column.
        Assert.All(register.Records, record => Assert.False(record.Children));
    }

    /// <summary>
    /// PRIV-MINOR-001: a deployment that admits minors is in the children's column,
    /// because any purpose may then be over a child's data.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_ADeploymentAdmittingMinorsIsInTheChildrensColumnAsync()
    {
        _configuration.Set(Settings.RegistrationAdultAffirmation, AttributeRequirement.Off);

        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.All(register.Records, record => Assert.True(record.Children));
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
                "shipping provider",
                RecipientCharacterisation.Processor,
                ["name", "phone", "address"],
                Location: null,
                AgreementReference: null,
                Callback: true))
            .Build();

        ProcessingRegister register = Generated(await Records(declared)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(
            ["sms gateway", "shipping provider"],
            register.Recipients.Select(recipient => recipient.Name));

        Assert.All(
            register.Recipients,
            recipient => Assert.Equal(
                RecipientCharacterisation.Processor,
                recipient.Characterisation));

        Assert.Equal("DPA-7", register.Recipients[0].AgreementReference);

        Assert.Equal(
            ["shipping provider"],
            register.Flags
                .Where(flag => flag.Finding is RegisterFinding.AgreementMissing)
                .Select(flag => flag.Subject));

        Assert.All(
            register.Records,
            record => Assert.Equal(
                ["sms gateway", "shipping provider"],
                record.Recipients));
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
    /// PRIV-RET-001, PRIV-SENS-002: the retention of each category the purpose is
    /// over is on the row, longest first, and a category the deployment declares none
    /// for is reported.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_TheRetentionOfEachCategoryIsOnTheRowAsync()
    {
        _configuration.Set(Settings.HostCategoryRetention, "identity", TimeSpan.FromDays(365));
        _configuration.Set(Settings.HostCategoryRetention, "order", TimeSpan.FromDays(1826));

        ProcessingRegister register = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(["order P1826D", "identity P365D"], Row(register, "fulfilment").Retention);
        Assert.DoesNotContain(
            register.Flags,
            flag => flag.Finding is RegisterFinding.RetentionMissing);

        _configuration.Clear(Settings.HostCategoryRetention.For("order"));

        ProcessingRegister missing = Generated(await Records(Declaration.Declared().Build())
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Contains(
            new RegisterFlag(RegisterFinding.RetentionMissing, "order"),
            missing.Flags);
    }

    /// <summary>
    /// PRIV-ROPA-001: the roles with access to a purpose are the roles holding a
    /// permission declared to serve it, read as they stand.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_TheRolesWithAccessAreTheOnesHoldingAServingPermissionAsync()
    {
        _roles.Allows("support", "order:read");
        _roles.Allows("auditor", "audit:read");

        AuthorizationDeclaration declared = Declaration.Declared()
            .ServesPurpose("order:read", "fulfilment")
            .Build();

        ProcessingRegister register = Generated(await Records(declared)
            .GenerateAsync(AccessContext.Of(Mona), TestContext.Current.CancellationToken));

        Assert.Equal(["support"], Row(register, "fulfilment").RolesWithAccess);
        Assert.Empty(Row(register, "marketing").RolesWithAccess);
    }

    /// <summary>
    /// PRIV-ROPA-001: the register answers the permission and nobody else, because it
    /// is the whole of what the deployment processes about everyone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_TheRegisterAnswersThePermissionAndNobodyElseAsync()
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

    private static ProcessingRegister Generated(Result<ProcessingRegister> outcome) =>
        outcome.Match(
            register => register,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ProcessingRecord Row(ProcessingRegister register, string purpose) =>
        Assert.Single(register.Records, record => record.Purpose == purpose);

    private ProcessingRecordsService Records(AuthorizationDeclaration declaration) =>
        new(
            new AdministrativeScope(_gate, _memberships),
            DeclaredProcessing.Of(declaration),
            declaration,
            _compliance,
            _roles,
            _configuration,
            _clock);
}
