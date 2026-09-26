using System;
using Janus.Authentication.Sending;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How a progressive delay is stored.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-001 and CONV-ENUM-001.</remarks>
internal sealed class ThrottleConfiguration : IEntityTypeConfiguration<ThrottleRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ThrottleRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("throttle_counters", table => table.HasCheckConstraint(
            "ck_throttle_counters_scope",
            Vocabulary.Admits<ThrottleScope>("scope")));

        builder.HasKey(counter => new { counter.Scope, counter.Key })
            .HasName("pk_throttle_counters");

        builder.Property(counter => counter.Scope)
            .HasColumnName("scope")
            .HasConversion(new VocabularyConverter<ThrottleScope>());

        builder.Property(counter => counter.Key)
            .HasColumnName("key")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(counter => counter.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(counter => counter.Failures).HasColumnName("failures");
        builder.Property(counter => counter.At).HasColumnName("at");
    }
}
