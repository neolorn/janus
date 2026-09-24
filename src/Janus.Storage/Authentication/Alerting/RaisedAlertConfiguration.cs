using System;
using Janus.Authentication.Alerting;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Alerting;

/// <summary>
/// How a raised condition waits for the alert channels.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-001 and CONV-DESIGN-002. The severity is not kept: it is the
/// condition's, and read from the table of conditions when the row is carried.
/// </remarks>
internal sealed class RaisedAlertConfiguration : IEntityTypeConfiguration<RaisedAlertRecord>
{
    private const int KeyLength = 320;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RaisedAlertRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("raised_alerts");

        builder.HasKey(alert => alert.Id).HasName("pk_raised_alerts");

        builder.Property(alert => alert.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new RaisedAlertId(value));

        builder.Property(alert => alert.RaisedAt).HasColumnName("raised_at");

        builder.Property(alert => alert.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(KeyLength);

        builder.Property(alert => alert.Condition)
            .HasColumnName("condition")
            .HasConversion(new VocabularyConverter<AlertCondition>());

        builder.Property(alert => alert.Details)
            .HasColumnName("details")
            .HasColumnType("jsonb");
    }
}
