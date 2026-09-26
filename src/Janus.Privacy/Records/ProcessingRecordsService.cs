using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Policies;

namespace Janus.Privacy.Records;

/// <summary>
/// The records of processing, generated from the declaration, the configuration and
/// the roles. Nothing about the deployment's processing is stored: what a person
/// supplies is three fields, and the rest is a query.
/// </summary>
/// <param name="scope">Whether the caller may ask.</param>
/// <param name="processing">What the deployment declared it processes.</param>
/// <param name="declaration">What the deployment declared about its own domain.</param>
/// <param name="compliance">Where the three supplied fields are.</param>
/// <param name="roles">Where the roles holding a permission are read.</param>
/// <param name="configuration">Where the hosting and retention keys are read.</param>
/// <param name="work">The one transaction what a person supplies is written in.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements PRIV-PRIN-002, PRIV-ROPA-001, PRIV-ROPA-002, PRIV-ROPA-003 and
/// LIB-API-005. A purpose added to the declaration appears in the next register with
/// no separate edit, which is the whole point of generating it.
/// </remarks>
internal sealed class ProcessingRecordsService(
    AdministrativeScope scope,
    DeclaredProcessing processing,
    AuthorizationDeclaration declaration,
    IComplianceStore compliance,
    IRegisterRoles roles,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time) : IProcessingRecords
{
    // The four rows of the shipped register, named as ProviderRegister ships them
    // (PRIV-ROPA-002, chapter 05 section 6).
    private const string MailServer = "mail server";

    private const string SmsGateway = "sms gateway";

    private const string HostingProvider = "hosting provider";

    private const string PasswordScreening = "password screening";

    /// <summary>
    /// How the personal data is disposed of, which is one measure for the whole
    /// deployment because every personal field stands under one key (PRIV-RIGHT-005).
    /// </summary>
    internal static readonly IReadOnlyList<string> Disposal =
        ["subject-key-destruction", "fingerprint-neutralisation"];

    /// <summary>
    /// The controls a sensitive declaration derives, which stand over everything the
    /// deployment holds and not only over what is declared sensitive (PRIV-SENS-002).
    /// </summary>
    internal static readonly IReadOnlyList<string> Controls =
        ["per-subject-field-encryption", "backup-encryption", "disk-encryption"];

    /// <summary>
    /// What the technical security measures column reports: those controls, and the
    /// keys no deployment can turn off through the application (10 section 4.8).
    /// </summary>
    internal static readonly IReadOnlyList<string> Measures =
    [
        .. Controls,
        .. Settings.All
            .Where(setting => setting.Scope is SettingScope.Protected)
            .Select(setting => setting.Key.ToString())
            .Order(StringComparer.Ordinal),
    ];

    /// <inheritdoc/>
    public async ValueTask<Result<ProcessingRegister>> GenerateAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (await scope
                .RefusedAsync(context, Permissions.RecordsOfProcessingRead, cancellationToken)
                .ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure<ProcessingRegister>(denied);
        }

        ComplianceRecord supplied = await compliance.ReadAsync(cancellationToken)
            .ConfigureAwait(false);

        string environment = await ReadAsync(
                Settings.HostingEnvironment, string.Empty, cancellationToken)
            .ConfigureAwait(false);

        HostingLocation location = await ReadAsync(
                Settings.HostingLocation, HostingLocation.Inside, cancellationToken)
            .ConfigureAwait(false);

        string basis = await ReadAsync(
                Settings.HostingCrossBorderBasis, string.Empty, cancellationToken)
            .ConfigureAwait(false);

        // PRIV-MINOR-001: where the affirmation is required the service is adults
        // only, so a deployment that declares no children's type is reporting the
        // truth; where it is off and none is declared, the register says so rather
        // than reporting no children's processing at all.
        bool minors = await ReadAsync(
                Settings.RegistrationAdultAffirmation,
                Settings.RegistrationAdultAffirmation.Default,
                cancellationToken)
            .ConfigureAwait(false) is AttributeRequirement.Off;

        // PRIV-ROPA-002: whether the library itself calls a mail server and the
        // screening service, which is what makes those two rows of the shipped
        // register true of this deployment.
        bool mail = (await ReadAsync(
                Settings.IntegrationMailEndpoint, string.Empty, cancellationToken)
            .ConfigureAwait(false)).Length is not 0;

        bool screening = await ReadAsync(
                Settings.PasswordBlocklistSource,
                Settings.PasswordBlocklistSource.Default,
                cancellationToken)
            .ConfigureAwait(false) is BlocklistSource.RangeApi;

        IReadOnlyList<RecipientRecord> recipients = Reached(location, basis, mail, screening);
        var flags = new List<RegisterFlag>();

        Missing(flags, supplied, recipients);

        if (minors && !declaration.ResourceTypes.Any(Childrens))
        {
            flags.Add(new RegisterFlag(RegisterFinding.ChildrenUndeclared, string.Empty));
        }

        var records = new List<ProcessingRecord>(processing.Purposes.Count);

        foreach (DeclaredPurpose purpose in processing.Purposes)
        {
            records.Add(await RecordedAsync(purpose, recipients, flags, cancellationToken)
                .ConfigureAwait(false));
        }

        return Result.Success(new ProcessingRegister(
            time.GetUtcNow(),
            environment,
            location,
            Stated(basis),
            supplied.DataOwner,
            supplied.OrganisationalSecurityMeasures,
            supplied.AssessmentLinks,
            records,
            recipients,
            flags));
    }

    /// <inheritdoc/>
    public async ValueTask<Result> DeclareAsync(
        AccessContext context,
        ComplianceRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(record);

        if (await scope
                .RefusedAsync(context, Permissions.ComplianceManage, cancellationToken)
                .ConfigureAwait(false)
            is Error denied)
        {
            return Result.Failure(denied);
        }

        await work.BeginAsync(cancellationToken).ConfigureAwait(false);
        await compliance.RecordAsync(record, cancellationToken).ConfigureAwait(false);
        await work.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static string? Stated(string basis) => basis.Length is 0 ? null : basis;

    // PRIV-SENS-001: children's data is one of the declared sensitivity categories,
    // and the register's children's column is that category and no other property of
    // the deployment.
    private static bool Childrens(ResourceTypeDeclaration type) =>
        type.SensitiveCategories.Contains(SensitiveCategories.Children, StringComparer.Ordinal);

    // PRIV-SENS-001 AC2: sensitivity is a column and not a verdict on the purpose, so
    // a purpose declared on an ordinary type and a sensitive one is in both columns.
    private bool Ordinary(DeclaredPurpose purpose) =>
        declaration.ResourceTypes.Any(type =>
            type.SensitiveCategories.Count is 0
            && type.Purposes.Any(declared =>
                string.Equals(declared.Name, purpose.Name, StringComparison.Ordinal)));

    // PRIV-ROPA-001 AC2: the three fields nobody can derive are reported missing
    // rather than generated blank, which is what makes the register submittable.
    private static void Missing(
        List<RegisterFlag> flags,
        ComplianceRecord supplied,
        IReadOnlyList<RecipientRecord> recipients)
    {
        if (string.IsNullOrWhiteSpace(supplied.DataOwner))
        {
            flags.Add(new RegisterFlag(RegisterFinding.DataOwnerMissing, string.Empty));
        }

        if (string.IsNullOrWhiteSpace(supplied.OrganisationalSecurityMeasures))
        {
            flags.Add(new RegisterFlag(RegisterFinding.OrganisationalMeasuresMissing, string.Empty));
        }

        if (supplied.AssessmentLinks.Count is 0)
        {
            flags.Add(new RegisterFlag(RegisterFinding.AssessmentLinksMissing, string.Empty));
        }

        // PRIV-ROPA-002 AC2: a processor acts on instructions, which is what the
        // agreement records; one without an agreement is the finding, not an omission.
        foreach (RecipientRecord recipient in recipients)
        {
            if (recipient.Characterisation is RecipientCharacterisation.Processor
                && string.IsNullOrWhiteSpace(recipient.AgreementReference))
            {
                flags.Add(new RegisterFlag(RegisterFinding.AgreementMissing, recipient.Name));
            }
        }
    }

    // PRIV-ROPA-003: a recipient outside the country is a cross-border transfer, and
    // the basis it stands on is the deployment's declared one and never a consent.
    private IReadOnlyList<RecipientRecord> Reached(
        HostingLocation hosting,
        string basis,
        bool mail,
        bool screening) =>
    [
        .. Recipients(mail, screening).Select(recipient =>
        {
            HostingLocation where = recipient.Location ?? hosting;

            return new RecipientRecord(
                recipient.Name,
                recipient.Characterisation,
                recipient.DataReceived,
                where,
                recipient.AgreementReference,
                where is HostingLocation.Outside ? Stated(basis) : null,
                recipient.Callback);
        }),
    ];

    // PRIV-ROPA-002: each shipped row is applied while the integration it describes is
    // configured, because the library itself makes it true. A deployment that edited
    // one of them has declared it, and its own row stands in place of the shipped
    // default rather than beside it.
    private IEnumerable<RecipientDeclaration> Recipients(bool mail, bool screening) =>
    [
        .. declaration.Recipients,
        .. ProviderRegister.Default.Where(row =>
            Made(row.Name, mail, screening) && !Declares(row.Name)),
    ];

    private static bool Made(string row, bool mail, bool screening) => row switch
    {
        HostingProvider => true,

        // Every deployment registers the transport its text messages leave through.
        SmsGateway => true,
        MailServer => mail,
        PasswordScreening => screening,
        _ => false,
    };

    private bool Declares(string row) =>
        declaration.Recipients.Any(recipient =>
            string.Equals(recipient.Name, row, StringComparison.OrdinalIgnoreCase));

    // A key the deployment names has no default to fall back on, so the fallback is
    // the caller's: an unnamed one leaves the cell empty rather than stopping the
    // register a compliance officer is trying to read.
    private async ValueTask<TValue> ReadAsync<TValue>(
        Setting<TValue> setting,
        TValue fallback,
        CancellationToken cancellationToken)
        where TValue : notnull =>
        (await configuration.ReadAsync(setting, cancellationToken).ConfigureAwait(false))
            .Match(read => read, _ => fallback);

    // PRIV-ROPA-001: every cell of a row is derived. What a purpose is over decides
    // the sensitivity columns, what the deployment retains decides the retention, and
    // the roles holding a permission bound to it decide who has access.
    private async ValueTask<ProcessingRecord> RecordedAsync(
        DeclaredPurpose purpose,
        IReadOnlyList<RecipientRecord> recipients,
        List<RegisterFlag> flags,
        CancellationToken cancellationToken)
    {
        if (purpose.Basis.RequiresAssessment && string.IsNullOrWhiteSpace(purpose.Assessment))
        {
            flags.Add(new RegisterFlag(RegisterFinding.AssessmentMissing, purpose.Name));
        }

        return new ProcessingRecord(
            purpose.Name,
            purpose.DataCategories,
            purpose.SubjectCategories,
            purpose.Basis.Key,
            Ordinary(purpose),
            purpose.SensitiveCategories.Count > 0,
            purpose.SensitiveCategories.Contains(SensitiveCategories.Children, StringComparer.Ordinal),
            purpose.SensitiveCategories,
            await KeptAsync(purpose, flags, cancellationToken).ConfigureAwait(false),
            [.. recipients.Select(recipient => recipient.Name)],
            Disposal,
            await AccessibleAsync(purpose, cancellationToken).ConfigureAwait(false),
            Measures,
            purpose.Assessment);
    }

    // PRIV-RET-001: retention is per category, so the row carries one entry a
    // category and the reader sees which of them governs storage.
    private async ValueTask<IReadOnlyList<string>> KeptAsync(
        DeclaredPurpose purpose,
        List<RegisterFlag> flags,
        CancellationToken cancellationToken)
    {
        var kept = new List<(string Category, TimeSpan For)>(purpose.DataCategories.Count);

        foreach (string category in purpose.DataCategories)
        {
            Result<TimeSpan> declared = await configuration
                .ReadAsync(Settings.HostCategoryRetention, category, cancellationToken)
                .ConfigureAwait(false);

            declared.Switch(
                read => kept.Add((category, read)),
                _ => flags.Add(new RegisterFlag(RegisterFinding.RetentionMissing, category)));
        }

        return
        [
            .. kept
                .OrderByDescending(entry => entry.For)
                .Select(entry => entry.Category
                    + " "
                    + XmlConvert.ToString(entry.For)),
        ];
    }

    private async ValueTask<IReadOnlyList<string>> AccessibleAsync(
        DeclaredPurpose purpose,
        CancellationToken cancellationToken)
    {
        var serving = new HashSet<Permission>(
            declaration.ActionPurposes
                .Where(bound => string.Equals(bound.Value, purpose.Name, StringComparison.Ordinal))
                .Select(bound => bound.Key));

        if (serving.Count is 0)
        {
            return [];
        }

        IReadOnlyDictionary<string, IReadOnlyList<Permission>> allowed =
            await roles.AllowedAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. allowed
                .Where(role => role.Value.Any(serving.Contains))
                .Select(role => role.Key)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }
}
