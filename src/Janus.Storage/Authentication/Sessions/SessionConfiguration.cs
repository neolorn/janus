using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// How a session is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-001, AUTH-SESS-002, AUTH-SESS-003, AUTH-SESS-013, BFF-CSRF-001 and
/// CONV-ENUM-001. The spine is indexed, so ending a record is one statement over the
/// sessions that name it.
/// </remarks>
internal sealed class SessionConfiguration : IEntityTypeConfiguration<SessionRecord>
{
    /// <summary>The table, which the encrypted columns name as their location.</summary>
    public const string Table = "sessions";

    /// <summary>The column the origin's address and city are held in.</summary>
    public const string OriginPlaceColumn = "origin_place";

    /// <summary>The column the last use's address and city are held in.</summary>
    public const string LastSeenPlaceColumn = "last_seen_place";

    private const int DescriptionLength = 64;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SessionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_sessions_type",
                Vocabulary.Admits<SessionType>("type"));

            // AUTH-SESS-001 (D-191): a presentation writes the instant of each level it
            // reaches, its own and every lower one, so a level holds an instant only
            // where the level below holds one no earlier.
            table.HasCheckConstraint(
                "ck_sessions_levels",
                "(aal1_at IS NULL OR aal1_at <= delegated_at)"
                + " AND (aal2_at IS NULL OR (aal1_at IS NOT NULL AND aal2_at <= aal1_at))"
                + " AND (aal3_at IS NULL OR (aal2_at IS NOT NULL AND aal3_at <= aal2_at))");

            // OPS-BOOT-002, D-170: only the session the break-glass credential opens,
            // and one derived from it, keeps the reason given at its use.
            table.HasCheckConstraint(
                "ck_sessions_breakglass_reason",
                "breakglass_reason IS NULL OR (satisfies_every_gate AND length(btrim(breakglass_reason)) BETWEEN 1 AND 1024)");

            table.HasCheckConstraint(
                "ck_sessions_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(session => session.Id).HasName("pk_sessions");

        builder.Property(session => session.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new SessionId(value));

        builder.Property(session => session.Spine)
            .HasColumnName("spine")
            .HasConversion(id => id.Value, value => new SessionId(value));

        builder.Property(session => session.Type)
            .HasColumnName("type")
            .HasConversion(new VocabularyConverter<SessionType>());

        builder.Property(session => session.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(session => session.SecretFingerprint)
            .HasColumnName("secret_fingerprint")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(session => session.CsrfFingerprint)
            .HasColumnName("csrf_fingerprint")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(session => session.CreatedAt).HasColumnName("created_at");
        builder.Property(session => session.LastSeenAt).HasColumnName("last_seen_at");

        builder.Property(session => session.DelegatedAt).HasColumnName("delegated_at");
        builder.Property(session => session.Aal1At).HasColumnName("aal1_at");
        builder.Property(session => session.Aal2At).HasColumnName("aal2_at");
        builder.Property(session => session.Aal3At).HasColumnName("aal3_at");
        builder.Property(session => session.PhishingResistantAt).HasColumnName("phishing_resistant_at");
        builder.Property(session => session.DowngradedAt).HasColumnName("downgraded_at");

        builder.Property(session => session.OriginBrowser)
            .HasColumnName("origin_browser")
            .HasMaxLength(DescriptionLength);

        builder.Property(session => session.OriginOs)
            .HasColumnName("origin_os")
            .HasMaxLength(DescriptionLength);

        builder.Property(session => session.OriginPlace).HasColumnName(OriginPlaceColumn);

        builder.Property(session => session.LastSeenBrowser)
            .HasColumnName("last_seen_browser")
            .HasMaxLength(DescriptionLength);

        builder.Property(session => session.LastSeenOs)
            .HasColumnName("last_seen_os")
            .HasMaxLength(DescriptionLength);

        builder.Property(session => session.LastSeenPlace).HasColumnName(LastSeenPlaceColumn);

        builder.Property(session => session.IdleExpiry).HasColumnName("idle_expiry");
        builder.Property(session => session.AbsoluteExpiry).HasColumnName("absolute_expiry");
        builder.Property(session => session.EndedAt).HasColumnName("ended_at");
        builder.Property(session => session.SatisfiesEveryGate).HasColumnName("satisfies_every_gate");
        builder.Property(session => session.BreakGlassReason).HasColumnName("breakglass_reason");
        builder.Property(session => session.Client).HasColumnName("client");

        // AUTH-SESS-003: the cookie is looked up by what it fingerprints to, and two
        // sessions never share one.
        builder.HasIndex(session => session.SecretFingerprint)
            .HasDatabaseName("ux_sessions_secret_fingerprint")
            .IsUnique();

        // AUTH-SESS-001: revoking the record ends everything standing on it, which is
        // one statement over this index.
        builder.HasIndex(session => session.Spine).HasDatabaseName("ix_sessions_spine");

        // AUTH-SESS-013: the account's list is the live sessions of one subject.
        builder.HasIndex(session => new { session.Subject, session.EndedAt })
            .HasDatabaseName("ix_sessions_subject");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(session => session.Subject)
            .HasConstraintName("fk_sessions_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
