using System;
using Janus.Storage.Authentication.Alerting;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Authentication.Passwords;
using Janus.Storage.Authentication.Sending;
using Janus.Storage.Authentication.Sessions;
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
using Janus.Storage.Privacy.Erasures;
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
    }
}
