using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SasPortal.Domain.Entities;

namespace SasPortal.Persistence.Configurations;

public class PortalRolePermissionConfiguration : IEntityTypeConfiguration<PortalRolePermission>
{
    public void Configure(EntityTypeBuilder<PortalRolePermission> builder)
    {
        builder.ToTable("portal_role_permissions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PortalRoleId)
            .HasColumnName("portal_role_id")
            .IsRequired();

        builder.Property(x => x.PortalPermissionId)
            .HasColumnName("portal_permission_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.PortalRoleId, x.PortalPermissionId }).IsUnique();

        builder.HasOne(x => x.PortalRole)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PortalRoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PortalPermission)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PortalPermissionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
