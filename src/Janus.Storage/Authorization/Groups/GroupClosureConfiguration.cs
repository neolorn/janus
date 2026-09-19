using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Groups;

/// <summary>
/// How the group closure is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-002, AUTHZ-INHERIT-002 AC4, CONV-ENUM-001 and
/// CONV-DESIGN-003. The index over the member is what makes a principal's groups one
/// read rather than a walk.
/// </remarks>
internal sealed class GroupClosureConfiguration : IEntityTypeConfiguration<GroupClosureRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GroupClosureRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("group_closure", table =>
        {
            table.HasCheckConstraint(
                "ck_group_closure_member_type",
                Vocabulary.Admits<SubjectType>("member_type"));

            // A group holds a member at some remove; nothing holds itself.
            table.HasCheckConstraint("ck_group_closure_depth", "depth >= 1");
        });

        builder.HasKey(entry => new { entry.Group, entry.MemberType, entry.MemberId })
            .HasName("pk_group_closure");

        builder.Property(entry => entry.Group)
            .HasColumnName("group_id")
            .HasConversion(id => id.Value, value => new GroupId(value));

        builder.Property(entry => entry.MemberType)
            .HasColumnName("member_type")
            .HasConversion(new VocabularyConverter<SubjectType>());

        builder.Property(entry => entry.MemberId).HasColumnName("member_id");
        builder.Property(entry => entry.Depth).HasColumnName("depth");

        builder.HasOne<GroupRecord>()
            .WithMany()
            .HasForeignKey(entry => entry.Group)
            .HasConstraintName("fk_group_closure_group")
            .OnDelete(DeleteBehavior.Cascade);

        // The one read every request makes: which groups hold this subject.
        builder.HasIndex(entry => new { entry.MemberType, entry.MemberId })
            .HasDatabaseName("ix_group_closure_member");
    }
}
