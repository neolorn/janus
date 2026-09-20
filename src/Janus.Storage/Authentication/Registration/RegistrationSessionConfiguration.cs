using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// How a registration session is stored.
/// </summary>
/// <remarks>
/// Implements REG-SESS-001 and REG-SESS-002. No foreign key names the provisional
/// subject: no account holds it until the terms step, and a session that never
/// reaches that step leaves nothing to point at.
/// </remarks>
internal sealed class RegistrationSessionConfiguration
    : IEntityTypeConfiguration<RegistrationSessionRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "registration_sessions";

    /// <summary>The column everything the steps collected is held in.</summary>
    public const string SessionColumn = "enc_session";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RegistrationSessionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable(Table);

        builder.HasKey(session => session.Id).HasName("pk_registration_sessions");

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new RegistrationSessionId(value));

        builder.Property(session => session.ProvisionalSubject)
            .HasColumnName("provisional_subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(session => session.ExpiresAt).HasColumnName("expires_at");
        builder.Property(session => session.KeyVersion).HasColumnName("key_version");
        builder.Property(session => session.WrappedKey).HasColumnName("wrapped_key");
        builder.Property(session => session.Session).HasColumnName(SessionColumn);

        // REG-SESS-001: the sweep reads the sessions that have lapsed and no others.
        builder.HasIndex(session => session.ExpiresAt)
            .HasDatabaseName("ix_registration_sessions_expires_at");
    }
}
