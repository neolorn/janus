using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Preferences;

/// <summary>
/// How an account's preferences are stored.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-001, REG-PREF-001, PRIV-RIGHT-005a and CONV-DESIGN-003.
/// </remarks>
internal sealed class PreferenceConfiguration : IEntityTypeConfiguration<PreferenceRecord>
{
    /// <summary>
    /// The table the encrypted column names as its location.
    /// </summary>
    public const string Table = "account_preferences";

    /// <summary>
    /// The column the declared values are written to.
    /// </summary>
    public const string ValuesColumn = "enc_values";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PreferenceRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(preferences => preferences.Subject).HasName("pk_account_preferences");

        builder.Property(preferences => preferences.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(preferences => preferences.Language).HasColumnName("language");
        builder.Property(preferences => preferences.TimeZone).HasColumnName("time_zone");
        builder.Property(preferences => preferences.Values).HasColumnName(ValuesColumn);

        builder.HasOne<AccountRecord>()
            .WithOne()
            .HasForeignKey<PreferenceRecord>(preferences => preferences.Subject)
            .HasConstraintName("fk_account_preferences_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
