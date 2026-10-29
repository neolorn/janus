using System;
using Janus.Authentication.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// How a collection session is stored.
/// </summary>
/// <remarks>
/// Implements REC-COL-001. No column names a subject: no record exists until the host
/// writes one, and a session that never reaches that leaves nothing to point at.
/// </remarks>
internal sealed class CollectionSessionConfiguration : IEntityTypeConfiguration<CollectionSessionRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "collection_sessions";

    /// <summary>The column everything the steps staged is held in.</summary>
    public const string SessionColumn = "enc_session";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CollectionSessionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable(Table);

        builder.HasKey(session => session.Id).HasName("pk_collection_sessions");

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new CollectionSessionId(value));

        builder.Property(session => session.ExpiresAt).HasColumnName("expires_at");
        builder.Property(session => session.WrappedKey).HasColumnName("wrapped_key");
        builder.Property(session => session.Session).HasColumnName(SessionColumn);

        // REC-COL-001: the sweep reads the sessions that have lapsed and no others.
        builder.HasIndex(session => session.ExpiresAt)
            .HasDatabaseName("ix_collection_sessions_expires_at");
    }
}
