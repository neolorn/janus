using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Alerting;

/// <summary>
/// How a raised condition is remembered for its deduplication window.
/// </summary>
/// <remarks>Implements OPS-ALERT-002.</remarks>
internal sealed class AlertConfiguration : IEntityTypeConfiguration<AlertRecord>
{
    private const int KeyLength = 256;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AlertRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("alerts");

        builder.HasKey(alert => alert.Key).HasName("pk_alerts");

        builder.Property(alert => alert.Key)
            .HasColumnName("key")
            .HasMaxLength(KeyLength);

        builder.Property(alert => alert.At).HasColumnName("at");
    }
}
