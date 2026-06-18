using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public class PortalUserRoleConfiguration : IEntityTypeConfiguration<PortalUserRole>
{
    public void Configure(EntityTypeBuilder<PortalUserRole> builder)
    {
        builder.ToTable("portal_user_roles");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PortalUserId)
            .HasColumnName("portal_user_id")
            .IsRequired();

        builder.Property(x => x.PortalRoleId)
            .HasColumnName("portal_role_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.PortalUserId, x.PortalRoleId }).IsUnique();

        builder.HasOne(x => x.PortalUser)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.PortalUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PortalRole)
            .WithMany(x => x.UserRoles)
            .HasForeignKey(x => x.PortalRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x =>
            x.PortalUser != null &&
            !x.PortalUser.IsDeleted &&
            x.PortalRole != null &&
            !x.PortalRole.IsDeleted);
    }
}
