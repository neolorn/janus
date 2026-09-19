using System;
using Janus.Core.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Settings;

/// <summary>
/// How a runtime-changeable configuration value is stored.
/// </summary>
/// <remarks>Implements OPS-CFG-008 and CONV-DESIGN-003.</remarks>
internal sealed class SettingConfiguration : IEntityTypeConfiguration<SettingRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SettingRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("settings");

        builder.HasKey(setting => setting.Key).HasName("pk_settings");

        builder.Property(setting => setting.Key)
            .HasColumnName("key")
            .HasConversion(key => key.ToString(), stored => ConfigurationKey.Parse(stored));

        builder.Property(setting => setting.Value).HasColumnName("value");
    }
}
