using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Groups;

/// <summary>
/// How a group's direct members are stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001, CONV-ENUM-001 and CONV-DESIGN-003. A member is an
/// account or another group, which is the whole of the nesting; the closure beside this
/// table is what a question reads.
/// </remarks>
internal sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMemberRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GroupMemberRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("group_members", table =>
            table.HasCheckConstraint(
                "ck_group_members_member_type",
                Vocabulary.Admits<SubjectType>("member_type")));

        builder.HasKey(member => new { member.Group, member.MemberType, member.MemberId })
            .HasName("pk_group_members");

        builder.Property(member => member.Group)
            .HasColumnName("group_id")
            .HasConversion(id => id.Value, value => new GroupId(value));

        builder.Property(member => member.MemberType)
            .HasColumnName("member_type")
            .HasConversion(new VocabularyConverter<SubjectType>());

        builder.Property(member => member.MemberId).HasColumnName("member_id");

        builder.HasOne<GroupRecord>()
            .WithMany()
            .HasForeignKey(member => member.Group)
            .HasConstraintName("fk_group_members_group")
            .OnDelete(DeleteBehavior.Cascade);

        // A subject's own memberships are read when one is removed.
        builder.HasIndex(member => new { member.MemberType, member.MemberId })
            .HasDatabaseName("ix_group_members_member");
    }
}
