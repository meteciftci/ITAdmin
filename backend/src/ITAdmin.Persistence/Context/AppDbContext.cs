using Microsoft.EntityFrameworkCore;
using ITAdmin.Domain.Entities;

namespace ITAdmin.Persistence.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<PortalUser> PortalUsers => Set<PortalUser>();
    public DbSet<PortalRole> PortalRoles => Set<PortalRole>();
    public DbSet<PortalPermission> PortalPermissions => Set<PortalPermission>();
    public DbSet<PortalUserRole> PortalUserRoles => Set<PortalUserRole>();
    public DbSet<PortalRolePermission> PortalRolePermissions => Set<PortalRolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();
    public DbSet<LdapSetting> LdapSettings => Set<LdapSetting>();
    public DbSet<AdManagementSettings> AdManagementSettings => Set<AdManagementSettings>();
    public DbSet<AdAttributeMapping> AdAttributeMappings => Set<AdAttributeMapping>();
    public DbSet<AdOperationLog> AdOperationLogs => Set<AdOperationLog>();
    public DbSet<NotificationProviderSettings> NotificationProviderSettings => Set<NotificationProviderSettings>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationOutbox> NotificationOutboxItems => Set<NotificationOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
