using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public sealed class LicenseSeatAssignmentConfiguration : IEntityTypeConfiguration<LicenseSeatAssignment>
{
    public void Configure(EntityTypeBuilder<LicenseSeatAssignment> builder)
    {
        builder.ToTable("license_seat_assignments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PackageId).HasColumnName("package_id").IsRequired();

        builder.Property(x => x.AdObjectId).HasColumnName("ad_object_id").HasMaxLength(100);
        builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SamAccountName).HasColumnName("sam_account_name").HasMaxLength(100);
        builder.Property(x => x.UserPrincipalName).HasColumnName("user_principal_name").HasMaxLength(250);
        builder.Property(x => x.Mail).HasColumnName("mail").HasMaxLength(250);
        builder.Property(x => x.NationalId).HasColumnName("national_id").HasMaxLength(20);
        builder.Property(x => x.Department).HasColumnName("department").HasMaxLength(200);
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);

        builder.Property(x => x.AssignedDate).HasColumnName("assigned_date").IsRequired();
        builder.Property(x => x.ReleasedDate).HasColumnName("released_date");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ReplacesAssignmentId).HasColumnName("replaces_assignment_id");
        builder.Property(x => x.SourceRequestItemId).HasColumnName("source_request_item_id");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(4000);

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasOne(x => x.Package)
            .WithMany(x => x.SeatAssignments)
            .HasForeignKey(x => x.PackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReplacesAssignment)
            .WithMany()
            .HasForeignKey(x => x.ReplacesAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PackageId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AdObjectId);
        builder.HasIndex(x => x.NationalId);
        builder.HasIndex(x => new { x.PackageId, x.Status });
    }
}
