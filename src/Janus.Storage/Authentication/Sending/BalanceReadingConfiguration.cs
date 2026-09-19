using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How a gateway balance reading is stored.
/// </summary>
/// <remarks>Implements INT-SMS-004.</remarks>
internal sealed class BalanceReadingConfiguration : IEntityTypeConfiguration<BalanceReadingRecord>
{
    private const int Precision = 18;

    private const int Scale = 4;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BalanceReadingRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sms_balance_readings");

        builder.HasKey(reading => reading.ReadAt).HasName("pk_sms_balance_readings");

        builder.Property(reading => reading.ReadAt).HasColumnName("read_at");

        builder.Property(reading => reading.Balance)
            .HasColumnName("balance")
            .HasPrecision(Precision, Scale);
    }
}
