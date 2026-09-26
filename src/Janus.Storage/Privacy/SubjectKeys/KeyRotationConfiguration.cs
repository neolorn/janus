using System;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// How the rotations' progress is kept.
/// </summary>
/// <remarks>
/// Implements OPS-SEC-003 and D-153: the row holds the key version, the last subject
/// identifier processed, the processed count, and the started, completed and retired
/// instants.
/// </remarks>
internal sealed class KeyRotationConfiguration : IEntityTypeConfiguration<KeyRotationRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "key_rotations";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KeyRotationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_key_rotations_kind", Vocabulary.Admits<KeyRotationKind>("kind"));
            table.HasCheckConstraint("ck_key_rotations_version", "version >= 1");
            table.HasCheckConstraint("ck_key_rotations_processed", "processed >= 0");

            // A rotation retires the versions before it only once it has completed.
            table.HasCheckConstraint("ck_key_rotations_retired", "retired_at IS NULL OR completed_at IS NOT NULL");
        });

        builder.HasKey(rotation => new { rotation.Kind, rotation.Version }).HasName("pk_key_rotations");

        builder.Property(rotation => rotation.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<KeyRotationKind>());

        builder.Property(rotation => rotation.Version).HasColumnName("version");

        builder.Property(rotation => rotation.LastSubject)
            .HasColumnName("last_subject")
            .HasConversion(subject => subject!.Value.Value, value => new SubjectId(value));

        builder.Property(rotation => rotation.Processed).HasColumnName("processed");
        builder.Property(rotation => rotation.StartedAt).HasColumnName("started_at");
        builder.Property(rotation => rotation.CompletedAt).HasColumnName("completed_at");
        builder.Property(rotation => rotation.RetiredAt).HasColumnName("retired_at");
    }
}
