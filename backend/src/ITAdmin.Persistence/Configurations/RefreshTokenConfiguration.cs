using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.PortalUserId)
            .HasColumnName("portal_user_id")
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");

        builder.Property(x => x.ReplacedByTokenHash)
            .HasColumnName("replaced_by_token_hash")
            .HasMaxLength(512);

        builder.Property(x => x.CreatedByIp)
            .HasColumnName("created_by_ip")
            .HasMaxLength(100);

        builder.Property(x => x.RevokedByIp)
            .HasColumnName("revoked_by_ip")
            .HasMaxLength(100);

        builder.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.IsPersistent)
            .HasColumnName("is_persistent")
            .IsRequired();

        builder.Property(x => x.LastUsedAt)
            .HasColumnName("last_used_at")
            .IsRequired();

        builder.HasIndex(x => x.TokenHash).IsUnique();

        builder.HasOne(x => x.PortalUser)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.PortalUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(x => x.PortalUser != null && !x.PortalUser.IsDeleted);
    }
}
