using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How a registered client is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, API-REDIR-001, OPS-SEC-002 and CONV-ENUM-001. There is no
/// registration endpoint: a row arrives here because the deployment put it here from
/// the server, so the table is read on every request and written by nothing a request
/// can reach.
/// </remarks>
internal sealed class OidcClientConfiguration : IEntityTypeConfiguration<OidcClientRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "oidc_clients";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OidcClientRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_oidc_clients_kind",
                Vocabulary.Admits<OidcClientKind>("kind"));

            // A replaced secret is held only with the instant it stops being accepted.
            table.HasCheckConstraint(
                "ck_oidc_clients_previous",
                "(previous_secret IS NULL) = (previous_secret_until IS NULL)");
        });

        builder.HasKey(client => client.ClientId).HasName("pk_oidc_clients");

        builder.Property(client => client.ClientId).HasColumnName("client_id");
        builder.Property(client => client.Name).HasColumnName("name");

        builder.Property(client => client.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<OidcClientKind>());

        builder.Property(client => client.Redirect).HasColumnName("redirect");
        builder.Property(client => client.Secret).HasColumnName("secret");
        builder.Property(client => client.PreviousSecret).HasColumnName("previous_secret");
        builder.Property(client => client.PreviousSecretUntil).HasColumnName("previous_secret_until");
        builder.Property(client => client.Scopes).HasColumnName("scopes");
    }
}
