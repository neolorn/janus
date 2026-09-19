using System;
using System.Globalization;
using Janus.Core;
using Janus.Privacy.SubjectKeys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.SubjectKeys;

/// <summary>
/// How a subject's wrapped data key is stored.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-005a and OPS-SEC-003. The key-encryption key itself is never
/// here: it is fetched from the secrets manager at startup, so a dump of this table
/// yields nothing.
/// </remarks>
internal sealed class SubjectKeyConfiguration : IEntityTypeConfiguration<SubjectKey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SubjectKey> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subject_keys", table =>
        {
            // PRIV-RIGHT-005a: a live key carries the scheme's marker, an erased one the
            // erased marker and 32 zero bytes. No third shape exists.
            table.HasCheckConstraint(
                "ck_subject_keys_format",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"(format_marker = {PersonalDataFormat.Marker} AND "
                        + $"octet_length(wrapped_key) = {PersonalDataFormat.WrappedKeyLength})"
                        + $" OR (format_marker = {PersonalDataFormat.ErasedMarker} AND "
                        + $"wrapped_key = decode(repeat('00', {PersonalDataFormat.DataKeyLength}), 'hex'))"));

            table.HasCheckConstraint("ck_subject_keys_version", "key_version >= 1");
        });

        builder.HasKey(key => key.Subject).HasName("pk_subject_keys");

        builder.Property(key => key.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(key => key.FormatMarker)
            .HasColumnName("format_marker")
            .HasColumnType("smallint");

        builder.Property(key => key.KeyVersion).HasColumnName("key_version");

        builder.Property(key => key.WrappedKey)
            .HasColumnName("wrapped_key")
            .HasConversion(
                wrapped => wrapped.ToArray(),
                stored => new ReadOnlyMemory<byte>(stored));

        // OPS-SEC-003: the rotation reads the keys still under the previous version.
        builder.HasIndex(key => key.KeyVersion).HasDatabaseName("ix_subject_keys_key_version");
    }
}
