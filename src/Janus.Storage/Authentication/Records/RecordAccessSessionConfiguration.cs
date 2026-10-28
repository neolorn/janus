using System;
using Janus.Authentication.Records;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// How a record access session is stored.
/// </summary>
/// <remarks>
/// Implements REC-ACC-001. The records reached are named by subject with no foreign key:
/// each action reads again whether a record is still in reach, so a record erased or
/// attached meanwhile is simply no longer reached.
/// </remarks>
internal sealed class RecordAccessSessionConfiguration : IEntityTypeConfiguration<RecordAccessSessionRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecordAccessSessionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("record_access_sessions");

        builder.HasKey(session => session.Id).HasName("pk_record_access_sessions");

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new RecordAccessSessionId(value));

        builder.Property(session => session.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<IdentifierKind>());

        builder.Property(session => session.Fingerprints).HasColumnName("fingerprints");
        builder.Property(session => session.Throttled).HasColumnName("throttled");
        builder.Property(session => session.CreatedAt).HasColumnName("created_at");
        builder.Property(session => session.OpenedAt).HasColumnName("opened_at");
        builder.Property(session => session.ExpiresAt).HasColumnName("expires_at");
        builder.Property(session => session.Reached).HasColumnName("reached");

        // REC-ACC-001: the sweep reads the sessions that have lapsed and no others.
        builder.HasIndex(session => session.ExpiresAt)
            .HasDatabaseName("ix_record_access_sessions_expires_at");
    }
}
