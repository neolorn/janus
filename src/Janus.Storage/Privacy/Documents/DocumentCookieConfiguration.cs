using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Documents;

/// <summary>
/// How a cookie a version of the privacy notice lists is stored.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-006a (D-203). A version lists a cookie once, by its name, and
/// the list goes with the version it belongs to.
/// </remarks>
internal sealed class DocumentCookieConfiguration : IEntityTypeConfiguration<DocumentCookieRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DocumentCookieRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("legal_document_cookies", table =>
        {
            table.HasCheckConstraint(
                "ck_legal_document_cookies_cookie",
                "length(trim(cookie)) > 0");
            table.HasCheckConstraint(
                "ck_legal_document_cookies_purpose",
                "purpose IS NULL OR length(trim(purpose)) > 0");
            table.HasCheckConstraint(
                "ck_legal_document_cookies_lifetime",
                "lifetime IS NULL OR lifetime > interval '0'");
        });

        builder.HasKey(cookie => new
        {
            cookie.Name,
            cookie.Version,
            cookie.Cookie,
        }).HasName("pk_legal_document_cookies");

        builder.Property(cookie => cookie.Name).HasColumnName("document");
        builder.Property(cookie => cookie.Version).HasColumnName("version");
        builder.Property(cookie => cookie.Cookie).HasColumnName("cookie");
        builder.Property(cookie => cookie.Purpose).HasColumnName("purpose");
        builder.Property(cookie => cookie.Lifetime).HasColumnName("lifetime");
        builder.Property(cookie => cookie.ThirdParty).HasColumnName("third_party");

        builder.HasOne<DocumentVersionRecord>()
            .WithMany(version => version.Cookies)
            .HasForeignKey(cookie => new { cookie.Name, cookie.Version })
            .HasConstraintName("fk_legal_document_cookies_version")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
