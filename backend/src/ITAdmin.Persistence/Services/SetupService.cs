using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.Constants;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Security;
using ITAdmin.Application.Common.Setup;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;

namespace ITAdmin.Persistence.Services;

public sealed partial class SetupService(
    AppDbContext context,
    ILdapService ldapService,
    ISecretProtector secretProtector,
    IConfiguration configuration,
    ISetupKeyValidator setupKeyValidator,
    ILogger<SetupService> logger) : ISetupService
{
    private const string SuperAdminRoleCode = "SuperAdmin";
    private const string AdministratorRoleCode = "Administrator";
    private const string UserRoleCode = "User";
    private const string SetupActor = "setup";
    private const string ActiveDirectoryDirectorySource = "ActiveDirectory";

    internal const string DirectoryUserProfileCouldNotBeLoadedMessage =
        "Directory user profile could not be loaded. Check the administrator user name, User Search Base, and User Search Filter.";

    internal const string AdminUserNotFoundInDirectoryMessage =
        "One or more selected admin users could not be found in the directory.";

    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        var hasAnyUser = await context.PortalUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        var isSetupCompleted = await context.ApplicationSettings
            .AsNoTracking()
            .AnyAsync(x =>
                    x.Key == "Setup:IsCompleted" &&
                    x.Value == "true" &&
                    x.IsActive &&
                    !x.IsDeleted,
                cancellationToken);

        return !hasAnyUser || !isSetupCompleted;
    }

    public async Task<ValidateSetupLdapResult> ValidateLdapAsync(
        ValidateSetupLdapRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!SetupRequestValidator.TryValidateLdapSettings(request.Ldap, out var message, out _))
        {
            return new ValidateSetupLdapResult(false, message);
        }

        var setupKeyValidation = SetupRequestValidator.ValidateSetupKey(
            setupKeyValidator,
            configuration,
            request.SetupKey);
        if (!SetupRequestValidator.TryMapSetupKeyValidationFailure(setupKeyValidation, out message, out _))
        {
            return new ValidateSetupLdapResult(false, message);
        }

        if (!await IsSetupRequiredAsync(cancellationToken))
        {
            return new ValidateSetupLdapResult(false, "Setup has already been completed.");
        }

        return await ValidateLdapConnectionAsync(request.Ldap, cancellationToken);
    }

    public async Task<SearchSetupAdminUsersResult> SearchAdminUsersAsync(
        SearchSetupAdminUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        var setupKeyValidation = SetupRequestValidator.ValidateSetupKey(
            setupKeyValidator,
            configuration,
            request.SetupKey);
        if (!SetupRequestValidator.TryMapSetupKeyValidationFailure(setupKeyValidation, out var setupKeyMessage, out _))
        {
            return new SearchSetupAdminUsersResult(Array.Empty<SetupAdminUserSearchResult>(), setupKeyMessage);
        }

        if (!await IsSetupRequiredAsync(cancellationToken))
        {
            return new SearchSetupAdminUsersResult(Array.Empty<SetupAdminUserSearchResult>(), "Setup has already been completed.");
        }

        if (!SetupRequestValidator.TryValidateLdapSettings(request.Ldap, out var ldapMessage, out _))
        {
            return new SearchSetupAdminUsersResult(Array.Empty<SetupAdminUserSearchResult>(), ldapMessage);
        }

        var search = request.Search.Trim();
        if (search.Length < SetupConstants.MinAdminUserSearchLength)
        {
            return new SearchSetupAdminUsersResult(Array.Empty<SetupAdminUserSearchResult>());
        }

        var lookupResults = await ldapService.SearchUsersAsync(
            new LdapUserLookupRequest(
                Host: request.Ldap.Host,
                BaseDn: request.Ldap.BaseDn,
                UserSearchBase: request.Ldap.UserSearchBase,
                BindUserName: request.Ldap.BindUserName,
                BindUserDomain: request.Ldap.BindUserDomain,
                BindPassword: request.Ldap.BindPassword,
                Search: search,
                MaxResults: SetupConstants.MaxAdminUserSearchResults,
                NationalIdAttribute: null),
            cancellationToken);

        var users = lookupResults
            .Select(item => new SetupAdminUserSearchResult(
                item.UserName,
                item.DisplayName,
                item.Email,
                item.DistinguishedName,
                item.DirectoryObjectId))
            .ToList();

        return new SearchSetupAdminUsersResult(users);
    }

    public async Task<CompleteSetupResult> CompleteSetupAsync(
        CompleteSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!SetupRequestValidator.TryValidateCompleteSetupRequest(request, out var validationMessage, out _))
        {
            return new CompleteSetupResult(false, validationMessage);
        }

        var setupKeyValidation = SetupRequestValidator.ValidateSetupKey(
            setupKeyValidator,
            configuration,
            request.SetupKey);
        if (!SetupRequestValidator.TryMapSetupKeyValidationFailure(setupKeyValidation, out validationMessage, out _))
        {
            return new CompleteSetupResult(false, validationMessage);
        }

        if (!await IsSetupRequiredAsync(cancellationToken))
        {
            return new CompleteSetupResult(false, "Setup has already been completed.");
        }

        logger.LogInformation("Setup complete started.");
        logger.LogInformation("Setup LDAP validation started.");

        var ldapValidation = await ValidateLdapConnectionAsync(request.Ldap, cancellationToken);
        logger.LogInformation("Setup LDAP validation completed. IsValid={IsValid}", ldapValidation.IsValid);
        if (!ldapValidation.IsValid)
        {
            logger.LogWarning("Setup failed.");
            return new CompleteSetupResult(false, ldapValidation.Message);
        }

        logger.LogInformation("Setup LDAP profile lookup started.");
        var resolvedProfiles = await ResolveAdminUserProfilesAsync(request.Ldap, request.AdminUsers, cancellationToken);
        if (resolvedProfiles is null)
        {
            logger.LogWarning("Setup failed.");
            return new CompleteSetupResult(false, AdminUserNotFoundInDirectoryMessage);
        }

        logger.LogInformation("Setup LDAP profile lookup completed. Count={Count}", resolvedProfiles.Count);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        logger.LogInformation("Setup transaction started.");

        try
        {
            var now = DateTime.UtcNow;

            await PersistLdapSettingsAsync(request.Ldap, now, cancellationToken);
            var superAdminRole = await EnsureDefaultRolesAndPermissionsAsync(now, cancellationToken);
            await PersistAdManagementModuleSettingsAsync(request.Ldap, request.Modules, now, cancellationToken);

            var primaryAdminUser = await PersistAdminUsersAsync(resolvedProfiles, superAdminRole, now, cancellationToken);
            await MarkSetupCompletedAsync(now, cancellationToken);

            await context.SecurityLogs.AddAsync(
                new SecurityLog
                {
                    UserId = primaryAdminUser.Id,
                    UserName = primaryAdminUser.UserName,
                    EventType = "SetupCompleted",
                    Severity = "Info",
                    Description = "Initial setup completed.",
                    CreatedAt = now
                },
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Setup completed successfully.");
            return new CompleteSetupResult(true, "Setup completed successfully.");
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(exception, "Setup failed.");
            return new CompleteSetupResult(false, "Setup could not be completed.");
        }
    }

    private async Task<ValidateSetupLdapResult> ValidateLdapConnectionAsync(
        CompleteSetupLdapSettings ldap,
        CancellationToken cancellationToken)
    {
        var bindResult = await ldapService.ValidateBindAsync(
            new LdapBindValidationRequest
            {
                Host = ldap.Host,
                BindUserName = ldap.BindUserName,
                BindUserDomain = ldap.BindUserDomain,
                BindPassword = ldap.BindPassword
            },
            cancellationToken);

        if (!bindResult.IsValid)
        {
            return new ValidateSetupLdapResult(false, bindResult.Message);
        }

        var searchBasesResult = await ldapService.ValidateSearchBasesAsync(
            new LdapSearchBasesValidationRequest
            {
                Host = ldap.Host,
                BaseDn = ldap.BaseDn,
                UserSearchBase = ldap.UserSearchBase,
                BindUserName = ldap.BindUserName,
                BindUserDomain = ldap.BindUserDomain,
                BindPassword = ldap.BindPassword
            },
            cancellationToken);

        return new ValidateSetupLdapResult(searchBasesResult.IsValid, searchBasesResult.Message);
    }

    private async Task<IReadOnlyList<LdapUserProfile>?> ResolveAdminUserProfilesAsync(
        CompleteSetupLdapSettings ldap,
        IReadOnlyList<CompleteSetupAdminUser> adminUsers,
        CancellationToken cancellationToken)
    {
        var resolvedProfiles = new List<LdapUserProfile>(adminUsers.Count);

        foreach (var adminUser in adminUsers)
        {
            LdapUserProfile? profile = null;

            if (!string.IsNullOrWhiteSpace(adminUser.DirectoryObjectId))
            {
                profile = await ldapService.GetUserProfileByObjectIdAsync(
                    new LdapUserProfileByObjectIdRequest(
                        Host: ldap.Host,
                        BaseDn: ldap.BaseDn,
                        UserSearchBase: ldap.UserSearchBase,
                        BindUserName: ldap.BindUserName,
                        BindUserDomain: ldap.BindUserDomain,
                        BindPassword: ldap.BindPassword,
                        DirectoryObjectId: adminUser.DirectoryObjectId.Trim(),
                        NationalIdAttribute: null),
                    cancellationToken);
            }

            if (profile is null)
            {
                foreach (var candidateUserName in BuildLdapUserNameCandidates(adminUser.UserName))
                {
                    profile = await ldapService.GetUserProfileAsync(
                        new LdapUserProfileRequest(
                            Host: ldap.Host,
                            BaseDn: ldap.BaseDn,
                            UserSearchBase: ldap.UserSearchBase,
                            UserSearchFilter: ldap.UserSearchFilter,
                            BindUserName: ldap.BindUserName,
                            BindUserDomain: ldap.BindUserDomain,
                            BindPassword: ldap.BindPassword,
                            UserName: candidateUserName,
                            NationalIdAttribute: null),
                        cancellationToken);

                    if (profile is not null)
                    {
                        break;
                    }
                }
            }

            if (profile is null)
            {
                return null;
            }

            resolvedProfiles.Add(profile);
        }

        return resolvedProfiles;
    }

    private async Task PersistLdapSettingsAsync(
        CompleteSetupLdapSettings ldap,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var activeLdapSetting = await context.LdapSettings
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);

        if (activeLdapSetting is null)
        {
            activeLdapSetting = new LdapSetting
            {
                Name = string.IsNullOrWhiteSpace(ldap.Name) ? "Default LDAP" : ldap.Name.Trim(),
                Host = ldap.Host.Trim(),
                BaseDn = ldap.BaseDn.Trim(),
                UserSearchBase = ldap.UserSearchBase.Trim(),
                UserSearchFilter = ldap.UserSearchFilter.Trim(),
                BindUserName = ldap.BindUserName.Trim(),
                BindUserDomain = ldap.BindUserDomain?.Trim(),
                EncryptedBindPassword = secretProtector.Protect(ldap.BindPassword),
                IsActive = true,
                CreatedAt = now,
                CreatedBy = SetupActor
            };

            await context.LdapSettings.AddAsync(activeLdapSetting, cancellationToken);
            return;
        }

        activeLdapSetting.Name = string.IsNullOrWhiteSpace(ldap.Name) ? "Default LDAP" : ldap.Name.Trim();
        activeLdapSetting.Host = ldap.Host.Trim();
        activeLdapSetting.BaseDn = ldap.BaseDn.Trim();
        activeLdapSetting.UserSearchBase = ldap.UserSearchBase.Trim();
        activeLdapSetting.UserSearchFilter = ldap.UserSearchFilter.Trim();
        activeLdapSetting.BindUserName = ldap.BindUserName.Trim();
        activeLdapSetting.BindUserDomain = ldap.BindUserDomain?.Trim();
        activeLdapSetting.EncryptedBindPassword = secretProtector.Protect(ldap.BindPassword);
        activeLdapSetting.UpdatedAt = now;
        activeLdapSetting.UpdatedBy = SetupActor;
    }

    private async Task PersistAdManagementModuleSettingsAsync(
        CompleteSetupLdapSettings ldap,
        CompleteSetupModulesSettings modules,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var entity = await context.AdManagementSettings
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var adManagement = modules.AdManagement;
        if (adManagement is null || !adManagement.IsEnabled)
        {
            if (entity is null)
            {
                entity = new AdManagementSettings
                {
                    IsEnabled = false,
                    PowerShellHealthEnabled = false,
                    PowerShellTimeoutSeconds = 30,
                    CreatedAt = now,
                    CreatedBy = SetupActor
                };
                await context.AdManagementSettings.AddAsync(entity, cancellationToken);
            }
            else
            {
                entity.IsEnabled = false;
                entity.UpdatedAt = now;
                entity.UpdatedBy = SetupActor;
            }

            return;
        }

        entity ??= new AdManagementSettings
        {
            PowerShellHealthEnabled = false,
            PowerShellTimeoutSeconds = 30,
            CreatedAt = now,
            CreatedBy = SetupActor
        };

        var isNewEntity = !context.AdManagementSettings.Local.Any(x => x.Id == entity.Id) &&
                          !await context.AdManagementSettings.AnyAsync(x => x.Id == entity.Id, cancellationToken);

        entity.IsEnabled = true;
        entity.BaseDn = ldap.BaseDn.Trim();
        entity.DefaultNamingContext = ldap.BaseDn.Trim();
        entity.DomainFqdn = DeriveDomainFqdn(ldap.BaseDn, ldap.Host);
        entity.NetbiosDomainName = ldap.BindUserDomain?.Trim();
        entity.UsersRootOu = adManagement.UsersSearchBase!.Trim();
        entity.GroupsSearchBase = adManagement.GroupsSearchBase!.Trim();
        entity.ComputersSearchBase = adManagement.ComputersSearchBase!.Trim();
        entity.DisabledUsersOu = string.IsNullOrWhiteSpace(adManagement.DefaultUserOu)
            ? adManagement.UsersSearchBase!.Trim()
            : adManagement.DefaultUserOu.Trim();
        entity.ServiceAccountUserName = ldap.BindUserName.Trim();
        entity.EncryptedServiceAccountPassword = secretProtector.Protect(ldap.BindPassword);
        entity.UpdatedAt = now;
        entity.UpdatedBy = SetupActor;

        if (isNewEntity)
        {
            await context.AdManagementSettings.AddAsync(entity, cancellationToken);
        }
    }

    private async Task<PortalUser> PersistAdminUsersAsync(
        IReadOnlyList<LdapUserProfile> profiles,
        PortalRole superAdminRole,
        DateTime now,
        CancellationToken cancellationToken)
    {
        PortalUser? primaryAdminUser = null;

        foreach (var ldapProfile in profiles)
        {
            var usernameConflictExists = await context.PortalUsers.AnyAsync(
                x => x.UserName == ldapProfile.UserName &&
                     x.DirectoryObjectId != ldapProfile.DirectoryObjectId,
                cancellationToken);

            if (usernameConflictExists)
            {
                throw new InvalidOperationException("A portal user with the same user name already exists.");
            }

            var displayName = ResolveDisplayName(ldapProfile);
            var adminUser = await context.PortalUsers
                .FirstOrDefaultAsync(x => x.DirectoryObjectId == ldapProfile.DirectoryObjectId, cancellationToken);

            if (adminUser is null)
            {
                adminUser = new PortalUser
                {
                    DirectorySource = ActiveDirectoryDirectorySource,
                    DirectoryObjectId = ldapProfile.DirectoryObjectId,
                    PreferredLanguage = "tr",
                    UserName = ldapProfile.UserName,
                    DisplayName = displayName,
                    Email = ldapProfile.Email,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = SetupActor
                };
                await context.PortalUsers.AddAsync(adminUser, cancellationToken);
            }
            else
            {
                adminUser.DirectorySource = ActiveDirectoryDirectorySource;
                adminUser.DirectoryObjectId = ldapProfile.DirectoryObjectId;
                adminUser.UserName = ldapProfile.UserName;
                adminUser.DisplayName = displayName;
                adminUser.Email = ldapProfile.Email;
                adminUser.IsActive = true;
                adminUser.UpdatedAt = now;
                adminUser.UpdatedBy = SetupActor;
            }

            var hasSuperAdminRole = await context.PortalUserRoles
                .AnyAsync(
                    x => x.PortalUserId == adminUser.Id &&
                         x.PortalRoleId == superAdminRole.Id,
                    cancellationToken);

            if (!hasSuperAdminRole)
            {
                await context.PortalUserRoles.AddAsync(
                    new PortalUserRole
                    {
                        PortalUserId = adminUser.Id,
                        PortalRoleId = superAdminRole.Id,
                        CreatedAt = now,
                        CreatedBy = SetupActor
                    },
                    cancellationToken);
            }

            primaryAdminUser ??= adminUser;
        }

        return primaryAdminUser ?? throw new InvalidOperationException("At least one admin user is required.");
    }

    private async Task MarkSetupCompletedAsync(DateTime now, CancellationToken cancellationToken)
    {
        var setupCompletionSetting = await context.ApplicationSettings
            .FirstOrDefaultAsync(x => x.Key == "Setup:IsCompleted", cancellationToken);

        if (setupCompletionSetting is null)
        {
            setupCompletionSetting = new ApplicationSetting
            {
                Key = "Setup:IsCompleted",
                Value = "true",
                ValueType = SettingValueType.Boolean,
                Description = "Indicates whether initial setup has been completed.",
                IsEncrypted = false,
                IsSystem = true,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = SetupActor
            };
            await context.ApplicationSettings.AddAsync(setupCompletionSetting, cancellationToken);
            return;
        }

        setupCompletionSetting.Value = "true";
        setupCompletionSetting.ValueType = SettingValueType.Boolean;
        setupCompletionSetting.Description = "Indicates whether initial setup has been completed.";
        setupCompletionSetting.IsEncrypted = false;
        setupCompletionSetting.IsSystem = true;
        setupCompletionSetting.IsActive = true;
        setupCompletionSetting.UpdatedAt = now;
        setupCompletionSetting.UpdatedBy = SetupActor;
    }

    private static string DeriveDomainFqdn(string baseDn, string host)
    {
        var dcParts = baseDn
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
            .Select(part => part[3..])
            .ToArray();

        if (dcParts.Length > 0)
        {
            return string.Join('.', dcParts);
        }

        return host.Trim();
    }

    internal static IReadOnlyList<string> BuildLdapUserNameCandidates(string userName)
    {
        var trimmed = userName.Trim();
        if (trimmed.Length == 0)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        void AddCandidate(string candidate)
        {
            var c = candidate.Trim();
            if (c.Length == 0 || seen.Contains(c))
            {
                return;
            }

            seen.Add(c);
            result.Add(c);
        }

        AddCandidate(trimmed);

        var slashIdx = trimmed.LastIndexOf('\\');
        if (slashIdx >= 0 && slashIdx < trimmed.Length - 1)
        {
            AddCandidate(trimmed[(slashIdx + 1)..]);
        }

        var atIdx = trimmed.IndexOf('@');
        if (atIdx > 0)
        {
            AddCandidate(trimmed[..atIdx]);
        }

        return result;
    }

    private static string ResolveDisplayName(LdapUserProfile ldapProfile) =>
        string.IsNullOrWhiteSpace(ldapProfile.DisplayName)
            ? ldapProfile.UserName
            : ldapProfile.DisplayName;
}
