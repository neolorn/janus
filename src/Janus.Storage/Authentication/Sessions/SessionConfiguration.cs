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
            table.HasCheckConstraint(
                "ck_sessions_attained",
                Vocabulary.Admits<AssuranceLevel>("attained"));

            // AUTH-SESS-002: what was reached resisting relay was reached at an
            // instant, and what was not reached has none.
            table.HasCheckConstraint(
                "ck_sessions_phishing_resistant",
                "phishing_resistant = (phishing_resistant_at IS NOT NULL)");
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

        builder.Property(session => session.Attained)
            .HasColumnName("attained")
            .HasConversion(new VocabularyConverter<AssuranceLevel>());

        builder.Property(session => session.AttainedAt).HasColumnName("attained_at");
        builder.Property(session => session.PhishingResistant).HasColumnName("phishing_resistant");
        builder.Property(session => session.PhishingResistantAt).HasColumnName("phishing_resistant_at");

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
