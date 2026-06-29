using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using static ITAdmin.Persistence.Services.LicenseManagement.LicenseManagementServiceHelpers;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicenseManagementOverviewService(AppDbContext context) : ILicenseManagementOverviewService
{
    public async Task<LicenseManagementOverviewSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var companyCount = await context.LicenseCompanies.CountAsync(cancellationToken);
        var activeProductCount = await context.LicensedProducts.CountAsync(x => x.IsActive, cancellationToken);
        var purchaseCount = await context.LicensePurchases.CountAsync(cancellationToken);
        var packageCount = await context.LicensePackages.CountAsync(cancellationToken);
        var totalLicenseQuantity = await context.LicensePackages.SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;

        return new LicenseManagementOverviewSummary(
            companyCount,
            activeProductCount,
            purchaseCount,
            packageCount,
            totalLicenseQuantity);
    }
}

public sealed class LicenseCompanyService(AppDbContext context) : ILicenseCompanyService
{
    public async Task<PagedResult<LicenseCompanyListItem>> GetListAsync(
        LicenseCompanyListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);
        var itemsQuery = context.LicenseCompanies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.Name, pattern)
                || (x.Email != null && EF.Functions.ILike(x.Email, pattern))
                || (x.Phone != null && EF.Functions.ILike(x.Phone, pattern))
                || (x.ContactPersonName != null && EF.Functions.ILike(x.ContactPersonName, pattern)));
        }

        if (query.IsActive is { } isActive)
        {
            itemsQuery = itemsQuery.Where(x => x.IsActive == isActive);
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await itemsQuery
            .OrderBy(x => x.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LicenseCompanyListItem(
                x.Id,
                x.Name,
                x.Email,
                x.Phone,
                x.ContactPersonName,
                x.ContactPersonPhone,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<LicenseCompanyListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<LicenseCompanyDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseCompanies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<LicenseCompanyOperationResult> CreateAsync(
        CreateLicenseCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCompanyFields(
            request.Name,
            request.Email,
            request.ContactPersonEmail,
            request.Website);
        if (validationError is not null)
        {
            return new LicenseCompanyOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        var entity = new LicenseCompany
        {
            Name = request.Name.Trim(),
            Phone = LicenseManagementValidation.TrimOrNull(request.Phone),
            Email = LicenseManagementValidation.TrimOrNull(request.Email),
            Website = LicenseManagementValidation.TrimOrNull(request.Website),
            ContactPersonName = LicenseManagementValidation.TrimOrNull(request.ContactPersonName),
            ContactPersonPhone = LicenseManagementValidation.TrimOrNull(request.ContactPersonPhone),
            ContactPersonEmail = LicenseManagementValidation.TrimOrNull(request.ContactPersonEmail),
            Notes = LicenseManagementValidation.TrimOrNull(request.Notes),
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedBy = request.ActorUserName
        };

        await context.LicenseCompanies.AddAsync(entity, cancellationToken);
        await WriteAuditAsync(
            context,
            "Create",
            "LicenseCompany",
            entity.Id,
            $"License company created: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseCompanyOperationResult(true, "License company created.", Map(entity));
    }

    public async Task<LicenseCompanyOperationResult> UpdateAsync(
        UpdateLicenseCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseCompanies.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            return new LicenseCompanyOperationResult(false, "License company was not found.");
        }

        var validationError = ValidateCompanyFields(
            request.Name,
            request.Email,
            request.ContactPersonEmail,
            request.Website);
        if (validationError is not null)
        {
            return new LicenseCompanyOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        entity.Name = request.Name.Trim();
        entity.Phone = LicenseManagementValidation.TrimOrNull(request.Phone);
        entity.Email = LicenseManagementValidation.TrimOrNull(request.Email);
        entity.Website = LicenseManagementValidation.TrimOrNull(request.Website);
        entity.ContactPersonName = LicenseManagementValidation.TrimOrNull(request.ContactPersonName);
        entity.ContactPersonPhone = LicenseManagementValidation.TrimOrNull(request.ContactPersonPhone);
        entity.ContactPersonEmail = LicenseManagementValidation.TrimOrNull(request.ContactPersonEmail);
        entity.Notes = LicenseManagementValidation.TrimOrNull(request.Notes);
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "Update",
            "LicenseCompany",
            entity.Id,
            $"License company updated: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseCompanyOperationResult(true, "License company updated.", Map(entity));
    }

    public async Task<LicenseCompanyOperationResult> UpdateStatusAsync(
        UpdateLicenseCompanyStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseCompanies.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            return new LicenseCompanyOperationResult(false, "License company was not found.");
        }

        if (entity.IsActive == request.IsActive)
        {
            return new LicenseCompanyOperationResult(true, "License company status is unchanged.", Map(entity));
        }

        var now = DateTime.UtcNow;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        var action = request.IsActive ? "Enable" : "Disable";
        await WriteAuditAsync(
            context,
            action,
            "LicenseCompany",
            entity.Id,
            $"License company {(request.IsActive ? "activated" : "deactivated")}: {entity.Name}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseCompanyOperationResult(true, "License company status updated.", Map(entity));
    }

    private static string? ValidateCompanyFields(
        string name,
        string? email,
        string? contactPersonEmail,
        string? website)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Company name is required.";
        }

        if (name.Trim().Length > 200)
        {
            return "Company name length is invalid.";
        }

        if (!LicenseManagementValidation.IsValidEmail(email))
        {
            return "Email format is invalid.";
        }

        if (!LicenseManagementValidation.IsValidEmail(contactPersonEmail))
        {
            return "Contact person email format is invalid.";
        }

        if (!LicenseManagementValidation.IsValidUrl(website))
        {
            return "Website URL format is invalid.";
        }

        return null;
    }

    private static LicenseCompanyDetail Map(LicenseCompany entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Phone,
            entity.Email,
            entity.Website,
            entity.ContactPersonName,
            entity.ContactPersonPhone,
            entity.ContactPersonEmail,
            entity.Notes,
            entity.IsActive,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);
}
