using System;
using Janus.Storage.Identity.Accounts;
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
    /// The case-insensitive collation the plaintext identifier columns carry.
    /// </summary>
    public const string CaseInsensitiveCollation = "janus_ci";

    /// <summary>
    /// The accounts.
    /// </summary>
    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();

    /// <summary>
    /// The wrapped per-subject data keys.
    /// </summary>
    public DbSet<SubjectKeyRecord> SubjectKeys => Set<SubjectKeyRecord>();

    /// <summary>
    /// The runtime-changeable configuration values in force.
    /// </summary>
    public DbSet<SettingRecord> Settings => Set<SettingRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        // OPS-DB-001: the collation the plaintext columns are compared under. It is
        // created here rather than by hand so that a database built from the migrations
        // alone carries it.
        modelBuilder.HasCollation(
            Schema,
            CaseInsensitiveCollation,
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false);

        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new SubjectKeyConfiguration());
        modelBuilder.ApplyConfiguration(new SettingConfiguration());
    }
}
