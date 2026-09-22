using System;
using Janus.Storage.Authentication.Accounts;
using Janus.Storage.Authentication.Alerting;
using Janus.Storage.Authentication.Credentials;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Identifiers;
using Janus.Storage.Authentication.Oidc;
using Janus.Storage.Authentication.Passwords;
using Janus.Storage.Authentication.Policies;
using Janus.Storage.Authentication.Recovery;
using Janus.Storage.Authentication.Registration;
using Janus.Storage.Authentication.Sending;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Authentication.SignIn;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Authorization.Groups;
using Janus.Storage.Authorization.Resources;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Audit;
using Janus.Storage.Identity.Identifiers;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Identity.Preferences;
using Janus.Storage.Identity.Profiles;
using Janus.Storage.Privacy.Consents;
using Janus.Storage.Privacy.Documents;
using Janus.Storage.Privacy.Erasures;
using Janus.Storage.Privacy.Exports;
using Janus.Storage.Privacy.Outbox;
using Janus.Storage.Privacy.Records;
using Janus.Storage.Privacy.Requests;
using Janus.Storage.Privacy.SubjectKeys;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage;

/// <summary>
/// The one context over the library's own schema. The library never reads or writes a
/// host table, and the host's migrations never collide with these.
/// </summary>
/// <param name="options">How the context reaches the database.</param>
/// <remarks>
/// Implements OPS-DB-002 and CONV-DESIGN-003. What the context maps is a persistence
/// record per table and never a domain entity; the ports translate between the two.
/// </remarks>
internal sealed class JanusDbContext(DbContextOptions<JanusDbContext> options) : DbContext(options)
{
    /// <summary>
    /// The schema the library owns. Nothing of the host's lives in it.
    /// </summary>
    public const string Schema = "janus";

    /// <summary>
    /// The library's own migration history, separate from the host's.
    /// </summary>
    public const string MigrationsHistoryTable = "__janus_migrations_history";

    /// <summary>
    /// The case-insensitive collation the plaintext columns a person spells carry.
    /// </summary>
    public const string CaseInsensitiveCollation = "janus_ci";

    /// <summary>
    /// The schema the collation is created in. A column names a collation by one
    /// identifier and never by a schema and a name, so the collation has to be
    /// reachable from the search path; the library's own schema is not.
    /// </summary>
    public const string CollationSchema = "public";

    /// <summary>
    /// The accounts.
    /// </summary>
    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();

    /// <summary>
    /// The organizations.
    /// </summary>
    public DbSet<OrganizationRecord> Organizations => Set<OrganizationRecord>();

    /// <summary>
    /// The memberships linking an account to an organization.
    /// </summary>
    public DbSet<MembershipRecord> Memberships => Set<MembershipRecord>();

    /// <summary>
    /// The accounts' identifiers.
    /// </summary>
    public DbSet<IdentifierRecord> Identifiers => Set<IdentifierRecord>();

    /// <summary>
    /// The backup setting each account has put in force for a kind.
    /// </summary>
    public DbSet<BackupSettingRecord> BackupSettings => Set<BackupSettingRecord>();

    /// <summary>
    /// The identifiers the accounts have given up, held while their undo lasts.
    /// </summary>
    public DbSet<IdentifierRemovalRecord> IdentifierRemovals => Set<IdentifierRemovalRecord>();

    /// <summary>
    /// The usernames held after the erasure of the accounts that bore them.
    /// </summary>
    public DbSet<UsernameHoldRecord> UsernameHolds => Set<UsernameHoldRecord>();

    /// <summary>
    /// The accounts' profiles.
    /// </summary>
    public DbSet<ProfileRecord> Profiles => Set<ProfileRecord>();

    /// <summary>
    /// The images the accounts show for themselves.
    /// </summary>
    public DbSet<ProfilePhotoRecord> ProfilePhotos => Set<ProfilePhotoRecord>();

    /// <summary>
    /// What each account has settled about language, time zone and the keys the host
    /// declared.
    /// </summary>
    public DbSet<PreferenceRecord> AccountPreferences => Set<PreferenceRecord>();

    /// <summary>
    /// The erasures, each carrying the host-side work outstanding for one subject.
    /// </summary>
    public DbSet<ErasureRecord> Erasures => Set<ErasureRecord>();

    /// <summary>
    /// The consents the subjects gave, withdrew or had superseded.
    /// </summary>
    public DbSet<ConsentRecordRow> Consents => Set<ConsentRecordRow>();

    /// <summary>
    /// The objections the subjects recorded.
    /// </summary>
    public DbSet<ObjectionRecordRow> Objections => Set<ObjectionRecordRow>();

    /// <summary>
    /// The facts about a subject the host has its own half of, one row a delivery.
    /// </summary>
    public DbSet<DeliveryRecord> Outbox => Set<DeliveryRecord>();

    /// <summary>
    /// One subscriber's confirmation of one delivery.
    /// </summary>
    public DbSet<DeliveryConfirmationRecord> OutboxConfirmations =>
        Set<DeliveryConfirmationRecord>();

    /// <summary>
    /// The data subject requests on the queue, decided ones included.
    /// </summary>
    public DbSet<PrivacyRequestRecord> PrivacyRequests => Set<PrivacyRequestRecord>();

    /// <summary>
    /// The exports the accounts have taken, one row an export.
    /// </summary>
    public DbSet<ExportRecordRow> PrivacyExports => Set<ExportRecordRow>();

    /// <summary>
    /// The supplied fields of the records of processing, at one row.
    /// </summary>
    public DbSet<ComplianceRow> ComplianceRecords => Set<ComplianceRow>();

    /// <summary>
    /// The published versions of the deployment's legal documents.
    /// </summary>
    public DbSet<DocumentVersionRecord> LegalDocumentVersions => Set<DocumentVersionRecord>();

    /// <summary>
    /// The translations attached to those versions.
    /// </summary>
    public DbSet<DocumentTranslationRecord> LegalDocumentTranslations =>
        Set<DocumentTranslationRecord>();

    /// <summary>
    /// The wrapped per-subject data keys.
    /// </summary>
    public DbSet<SubjectKeyRecord> SubjectKeys => Set<SubjectKeyRecord>();

    /// <summary>
    /// The audit trail. It is appended to and read; nothing changes or removes a row.
    /// </summary>
    public DbSet<AuditRowRecord> AuditRecords => Set<AuditRowRecord>();

    /// <summary>
    /// The runtime-changeable configuration values in force.
    /// </summary>
    public DbSet<SettingRecord> Settings => Set<SettingRecord>();

    /// <summary>
    /// The roles a grant may name.
    /// </summary>
    public DbSet<RoleRecord> Roles => Set<RoleRecord>();

    /// <summary>
    /// What each role allows.
    /// </summary>
    public DbSet<RolePermissionRecord> RolePermissions => Set<RolePermissionRecord>();

    /// <summary>
    /// The grants: subject, role, resource or organization.
    /// </summary>
    public DbSet<GrantRecord> Grants => Set<GrantRecord>();

    /// <summary>
    /// How many times what an account may do has changed.
    /// </summary>
    public DbSet<GrantVersionRecord> GrantVersions => Set<GrantVersionRecord>();

    /// <summary>
    /// The groups that hold grants on their members' behalf.
    /// </summary>
    public DbSet<GroupRecord> Groups => Set<GroupRecord>();

    /// <summary>
    /// What each group holds directly.
    /// </summary>
    public DbSet<GroupMemberRecord> GroupMembers => Set<GroupMemberRecord>();

    /// <summary>
    /// Every group a subject belongs to, at any depth.
    /// </summary>
    public DbSet<GroupClosureRecord> GroupClosure => Set<GroupClosureRecord>();

    /// <summary>
    /// The host's records the library knows of, and what contains each.
    /// </summary>
    public DbSet<ResourceRecord> Resources => Set<ResourceRecord>();

    /// <summary>
    /// Every record beside everything containing it.
    /// </summary>
    public DbSet<AncestryRecord> Ancestry => Set<AncestryRecord>();

    /// <summary>
    /// The sessions, which are the spine every credential derives from.
    /// </summary>
    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();

    /// <summary>
    /// The passwords, one to an account.
    /// </summary>
    public DbSet<PasswordRecord> Passwords => Set<PasswordRecord>();

    /// <summary>
    /// The enrolled credentials.
    /// </summary>
    public DbSet<AuthenticatorRecord> Authenticators => Set<AuthenticatorRecord>();

    /// <summary>
    /// The recovery-code sets, one to an account.
    /// </summary>
    public DbSet<RecoveryCodeSetRecord> RecoveryCodeSets => Set<RecoveryCodeSetRecord>();

    /// <summary>
    /// The recovery codes of those sets.
    /// </summary>
    public DbSet<RecoveryCodeRecord> RecoveryCodes => Set<RecoveryCodeRecord>();

    /// <summary>
    /// The browsers the accounts know.
    /// </summary>
    public DbSet<DeviceRecord> Devices => Set<DeviceRecord>();

    /// <summary>
    /// What each restriction key has had counted against it.
    /// </summary>
    public DbSet<SendCounterRecord> SendCounters => Set<SendCounterRecord>();

    /// <summary>
    /// The credit support has added to a restriction key.
    /// </summary>
    public DbSet<SendGrantRecord> SendGrants => Set<SendGrantRecord>();

    /// <summary>
    /// The messages a transport took, until a delivery report can no longer change
    /// what they counted.
    /// </summary>
    public DbSet<SendRecord> Sends => Set<SendRecord>();

    /// <summary>
    /// What each throttle scope has accumulated.
    /// </summary>
    public DbSet<ThrottleRecord> ThrottleCounters => Set<ThrottleRecord>();

    /// <summary>
    /// What the messaging gateway said its account stood at.
    /// </summary>
    public DbSet<BalanceReadingRecord> SmsBalanceReadings => Set<BalanceReadingRecord>();

    /// <summary>
    /// The addresses told that no account holds them.
    /// </summary>
    public DbSet<NoticeRecord> NonexistenceNotices => Set<NoticeRecord>();

    /// <summary>
    /// The inbound callbacks, counted per source.
    /// </summary>
    public DbSet<CallbackRecord> Callbacks => Set<CallbackRecord>();

    /// <summary>
    /// The registration sessions, counted per source.
    /// </summary>
    public DbSet<RegistrationSourceRecord> RegistrationSources =>
        Set<RegistrationSourceRecord>();

    /// <summary>
    /// The conditions already raised, within their deduplication window.
    /// </summary>
    public DbSet<AlertRecord> Alerts => Set<AlertRecord>();

    /// <summary>
    /// The registrations in progress, each staging what its steps collected.
    /// </summary>
    public DbSet<RegistrationSessionRecord> RegistrationSessions =>
        Set<RegistrationSessionRecord>();

    /// <summary>
    /// The verification links a registration in progress has outstanding.
    /// </summary>
    public DbSet<RegistrationLinkRecord> RegistrationLinks =>
        Set<RegistrationLinkRecord>();

    /// <summary>
    /// What browsers carry before they hold a session.
    /// </summary>
    public DbSet<PreAuthenticationRecord> PreAuthenticationSessions =>
        Set<PreAuthenticationRecord>();

    /// <summary>
    /// The identifiers of live accounts waiting to be proved.
    /// </summary>
    public DbSet<PendingVerificationRecord> IdentifierVerifications =>
        Set<PendingVerificationRecord>();

    /// <summary>
    /// The sign-ins in progress.
    /// </summary>
    public DbSet<ChallengeRecord> SignInChallenges => Set<ChallengeRecord>();

    /// <summary>
    /// The verification codes outstanding, which are no credential of anyone's
    /// (AUTH-FACT-004).
    /// </summary>
    public DbSet<VerificationCodeRecord> VerificationCodes => Set<VerificationCodeRecord>();

    /// <summary>
    /// The messages undertaken and not yet carried.
    /// </summary>
    public DbSet<SendDeliveryRecord> SendOutbox => Set<SendDeliveryRecord>();

    /// <summary>
    /// The credential creation ceremonies accounts have open.
    /// </summary>
    public DbSet<KeyCeremonyRecord> KeyCeremonies => Set<KeyCeremonyRecord>();

    /// <summary>
    /// The sign-in links and codes that have gone out.
    /// </summary>
    public DbSet<PendingSignInRecord> SignInLinks => Set<PendingSignInRecord>();

    /// <summary>
    /// The links the deactivation and deletion notices carried.
    /// </summary>
    public DbSet<LifecycleLinkRecord> LifecycleLinks => Set<LifecycleLinkRecord>();

    /// <summary>
    /// The requirements the policies in force have raised.
    /// </summary>
    public DbSet<PolicyRaiseRecord> PolicyRaises => Set<PolicyRaiseRecord>();

    /// <summary>
    /// The recovery links that have gone out.
    /// </summary>
    public DbSet<RecoveryLinkRecord> RecoveryLinks => Set<RecoveryLinkRecord>();

    /// <summary>
    /// The approvals standing behind a re-enrolment.
    /// </summary>
    public DbSet<RecoveryApprovalRecord> RecoveryApprovals => Set<RecoveryApprovalRecord>();

    /// <summary>
    /// The loss reports that are running.
    /// </summary>
    public DbSet<LossReportRecord> LossReports => Set<LossReportRecord>();

    /// <summary>
    /// The clients the deployment registered with the provider.
    /// </summary>
    public DbSet<OidcClientRecord> OidcClients => Set<OidcClientRecord>();

    /// <summary>
    /// The authorization codes waiting to be exchanged.
    /// </summary>
    public DbSet<AuthorizationCodeRecord> AuthorizationCodes => Set<AuthorizationCodeRecord>();

    /// <summary>
    /// The refresh tokens, by family.
    /// </summary>
    public DbSet<RefreshTokenRecord> RefreshTokens => Set<RefreshTokenRecord>();

    /// <summary>
    /// The keys the provider signs tokens with.
    /// </summary>
    public DbSet<SigningKeyRecord> SigningKeys => Set<SigningKeyRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        // OPS-DB-001: the collation the plaintext columns are compared under. It is
        // created here rather than by hand so that a database built from the migrations
        // alone carries it.
        modelBuilder.HasCollation(
            CollationSchema,
            CaseInsensitiveCollation,
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false);

        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new MembershipConfiguration());
        modelBuilder.ApplyConfiguration(new IdentifierConfiguration());
        modelBuilder.ApplyConfiguration(new BackupSettingConfiguration());
        modelBuilder.ApplyConfiguration(new IdentifierRemovalConfiguration());
        modelBuilder.ApplyConfiguration(new UsernameHoldConfiguration());
        modelBuilder.ApplyConfiguration(new ProfileConfiguration());
        modelBuilder.ApplyConfiguration(new ProfilePhotoConfiguration());
        modelBuilder.ApplyConfiguration(new PreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new ErasureConfiguration());
        modelBuilder.ApplyConfiguration(new SubjectKeyConfiguration());
        modelBuilder.ApplyConfiguration(new AuditConfiguration());
        modelBuilder.ApplyConfiguration(new SettingConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
        modelBuilder.ApplyConfiguration(new GrantConfiguration());
        modelBuilder.ApplyConfiguration(new GrantVersionConfiguration());
        modelBuilder.ApplyConfiguration(new GroupConfiguration());
        modelBuilder.ApplyConfiguration(new GroupMemberConfiguration());
        modelBuilder.ApplyConfiguration(new GroupClosureConfiguration());
        modelBuilder.ApplyConfiguration(new ResourceConfiguration());
        modelBuilder.ApplyConfiguration(new AncestryConfiguration());
        modelBuilder.ApplyConfiguration(new SessionConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordConfiguration());
        modelBuilder.ApplyConfiguration(new AuthenticatorConfiguration());
        modelBuilder.ApplyConfiguration<RecoveryCodeSetRecord>(new RecoveryCodeConfiguration());
        modelBuilder.ApplyConfiguration<RecoveryCodeRecord>(new RecoveryCodeConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceConfiguration());
        modelBuilder.ApplyConfiguration(new SendCounterConfiguration());
        modelBuilder.ApplyConfiguration(new SendGrantConfiguration());
        modelBuilder.ApplyConfiguration(new SendConfiguration());
        modelBuilder.ApplyConfiguration(new ThrottleConfiguration());
        modelBuilder.ApplyConfiguration(new BalanceReadingConfiguration());
        modelBuilder.ApplyConfiguration(new NoticeConfiguration());
        modelBuilder.ApplyConfiguration(new CallbackConfiguration());
        modelBuilder.ApplyConfiguration(new RegistrationSourceConfiguration());
        modelBuilder.ApplyConfiguration(new AlertConfiguration());
        modelBuilder.ApplyConfiguration(new RegistrationSessionConfiguration());
        modelBuilder.ApplyConfiguration(new RegistrationLinkConfiguration());
        modelBuilder.ApplyConfiguration(new PreAuthenticationConfiguration());
        modelBuilder.ApplyConfiguration(new PendingVerificationConfiguration());
        modelBuilder.ApplyConfiguration(new ChallengeConfiguration());
        modelBuilder.ApplyConfiguration(new VerificationCodeConfiguration());
        modelBuilder.ApplyConfiguration(new SendDeliveryConfiguration());
        modelBuilder.ApplyConfiguration(new KeyCeremonyConfiguration());
        modelBuilder.ApplyConfiguration(new PendingSignInConfiguration());
        modelBuilder.ApplyConfiguration(new PolicyRaiseConfiguration());
        modelBuilder.ApplyConfiguration(new RecoveryLinkConfiguration());
        modelBuilder.ApplyConfiguration(new RecoveryApprovalConfiguration());
        modelBuilder.ApplyConfiguration(new LossReportConfiguration());
        modelBuilder.ApplyConfiguration(new LifecycleLinkConfiguration());
        modelBuilder.ApplyConfiguration(new OidcClientConfiguration());
        modelBuilder.ApplyConfiguration(new AuthorizationCodeConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new SigningKeyConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentTranslationConfiguration());
        modelBuilder.ApplyConfiguration(new ConsentConfiguration());
        modelBuilder.ApplyConfiguration(new ObjectionConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryConfiguration());
        modelBuilder.ApplyConfiguration(new DeliveryConfirmationConfiguration());
        modelBuilder.ApplyConfiguration(new PrivacyRequestConfiguration());
        modelBuilder.ApplyConfiguration(new ExportConfiguration());
        modelBuilder.ApplyConfiguration(new ComplianceConfiguration());
    }
}
