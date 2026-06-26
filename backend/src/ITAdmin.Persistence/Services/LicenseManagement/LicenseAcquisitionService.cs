using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Domain.Enums;
using ITAdmin.Persistence.Context;
using static ITAdmin.Persistence.Services.LicenseManagement.LicenseManagementServiceHelpers;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicenseAcquisitionService(AppDbContext context) : ILicenseAcquisitionService
{
    public async Task<PagedResult<LicenseAcquisitionListItem>> GetListAsync(
        LicenseAcquisitionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);
        var itemsQuery = context.LicenseAcquisitions
            .AsNoTracking()
            .Include(x => x.SupplierCompany)
            .Include(x => x.SupportCompany)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            itemsQuery = itemsQuery.Where(x =>
                EF.Functions.ILike(x.Title, pattern)
                || (x.ContractNumber != null && EF.Functions.ILike(x.ContractNumber, pattern))
                || (x.SupplierCompany != null && EF.Functions.ILike(x.SupplierCompany.Name, pattern)));
        }

        if (query.AcquisitionType is { } acquisitionType)
        {
            itemsQuery = itemsQuery.Where(x => x.AcquisitionType == acquisitionType);
        }

        if (query.Status is { } status)
        {
            itemsQuery = itemsQuery.Where(x => x.Status == status);
        }

        if (query.SupplierCompanyId is { } supplierCompanyId)
        {
            itemsQuery = itemsQuery.Where(x => x.SupplierCompanyId == supplierCompanyId);
        }

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await itemsQuery
            .OrderByDescending(x => x.AcquisitionDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LicenseAcquisitionListItem(
                x.Id,
                x.Title,
                x.AcquisitionType,
                x.AcquisitionDate,
                x.SupplierCompany != null ? x.SupplierCompany.Name : null,
                x.SupportCompany != null ? x.SupportCompany.Name : null,
                x.ContractNumber,
                x.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<LicenseAcquisitionListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<LicenseAcquisitionDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseAcquisitions
            .AsNoTracking()
            .Include(x => x.SupplierCompany)
            .Include(x => x.SupportCompany)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<LicenseAcquisitionOperationResult> CreateAsync(
        CreateLicenseAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateAcquisitionAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return new LicenseAcquisitionOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        var entity = MapToEntity(new LicenseAcquisition(), request);
        entity.Status = request.Status;
        entity.CreatedAt = now;
        entity.CreatedBy = request.ActorUserName;

        await context.LicenseAcquisitions.AddAsync(entity, cancellationToken);
        await WriteAuditAsync(
            context,
            "Create",
            "LicenseAcquisition",
            entity.Id,
            $"License acquisition created: {entity.Title}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseAcquisitionOperationResult(true, "License acquisition created.", Map(entity));
    }

    public async Task<LicenseAcquisitionOperationResult> UpdateAsync(
        UpdateLicenseAcquisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseAcquisitions
            .Include(x => x.SupplierCompany)
            .Include(x => x.SupportCompany)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicenseAcquisitionOperationResult(false, "License acquisition was not found.");
        }

        var validationError = await ValidateAcquisitionAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return new LicenseAcquisitionOperationResult(false, validationError);
        }

        var now = DateTime.UtcNow;
        MapToEntity(entity, request);
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "Update",
            "LicenseAcquisition",
            entity.Id,
            $"License acquisition updated: {entity.Title}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseAcquisitionOperationResult(true, "License acquisition updated.", Map(entity));
    }

    public async Task<LicenseAcquisitionOperationResult> UpdateStatusAsync(
        UpdateLicenseAcquisitionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseAcquisitions
            .Include(x => x.SupplierCompany)
            .Include(x => x.SupportCompany)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return new LicenseAcquisitionOperationResult(false, "License acquisition was not found.");
        }

        if (entity.Status == request.Status)
        {
            return new LicenseAcquisitionOperationResult(true, "License acquisition status is unchanged.", Map(entity));
        }

        var now = DateTime.UtcNow;
        entity.Status = request.Status;
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.ActorUserName;

        await WriteAuditAsync(
            context,
            "Update",
            "LicenseAcquisition",
            entity.Id,
            $"License acquisition status changed to {request.Status}: {entity.Title}.",
            request.ActorUserId,
            request.ActorUserName,
            request.ActorIpAddress,
            request.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseAcquisitionOperationResult(true, "License acquisition status updated.", Map(entity));
    }

    private async Task<string?> ValidateAcquisitionAsync(
        CreateLicenseAcquisitionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Acquisition title is required.";
        }

        if (request.Title.Trim().Length > 300)
        {
            return "Acquisition title length is invalid.";
        }

        if (!Enum.IsDefined(request.AcquisitionType))
        {
            return "Acquisition type is invalid.";
        }

        if (!Enum.IsDefined(request.Status))
        {
            return "Acquisition status is invalid.";
        }

        if (!LicenseManagementValidation.IsValidDateRange(request.ContractStartDate, request.ContractEndDate, out _))
        {
            return "Contract end date cannot be earlier than contract start date.";
        }

        if (request.ActualTotalCost is < 0)
        {
            return "Actual total cost cannot be negative.";
        }

        if (request.ActualTotalCost is not null && string.IsNullOrWhiteSpace(request.Currency))
        {
            return "Currency is required when actual total cost is provided.";
        }

        return await ValidateCompanyReferencesAsync(request.SupplierCompanyId, request.SupportCompanyId, cancellationToken);
    }

    private async Task<string?> ValidateAcquisitionAsync(
        UpdateLicenseAcquisitionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "Acquisition title is required.";
        }

        if (!Enum.IsDefined(request.AcquisitionType))
        {
            return "Acquisition type is invalid.";
        }

        if (!LicenseManagementValidation.IsValidDateRange(request.ContractStartDate, request.ContractEndDate, out _))
        {
            return "Contract end date cannot be earlier than contract start date.";
        }

        if (request.ActualTotalCost is < 0)
        {
            return "Actual total cost cannot be negative.";
        }

        if (request.ActualTotalCost is not null && string.IsNullOrWhiteSpace(request.Currency))
        {
            return "Currency is required when actual total cost is provided.";
        }

        return await ValidateCompanyReferencesAsync(request.SupplierCompanyId, request.SupportCompanyId, cancellationToken);
    }

    private async Task<string?> ValidateCompanyReferencesAsync(
        Guid? supplierCompanyId,
        Guid? supportCompanyId,
        CancellationToken cancellationToken)
    {
        if (supplierCompanyId is { } supplierId)
        {
            var exists = await context.LicenseCompanies.AnyAsync(x => x.Id == supplierId, cancellationToken);
            if (!exists)
            {
                return "Supplier company was not found.";
            }
        }

        if (supportCompanyId is { } supportId)
        {
            var exists = await context.LicenseCompanies.AnyAsync(x => x.Id == supportId, cancellationToken);
            if (!exists)
            {
                return "Support company was not found.";
            }
        }

        return null;
    }

    private static LicenseAcquisition MapToEntity(LicenseAcquisition entity, CreateLicenseAcquisitionRequest request)
    {
        entity.AcquisitionType = request.AcquisitionType;
        entity.Title = request.Title.Trim();
        entity.Description = LicenseManagementValidation.TrimOrNull(request.Description);
        entity.AcquisitionDate = request.AcquisitionDate;
        entity.TenderNumber = LicenseManagementValidation.TrimOrNull(request.TenderNumber);
        entity.TenderDate = request.TenderDate;
        entity.DirectPurchaseNumber = LicenseManagementValidation.TrimOrNull(request.DirectPurchaseNumber);
        entity.DmoOrderNumber = LicenseManagementValidation.TrimOrNull(request.DmoOrderNumber);
        entity.EbysNumber = LicenseManagementValidation.TrimOrNull(request.EbysNumber);
        entity.EbysDate = request.EbysDate;
        entity.InvoiceNumber = LicenseManagementValidation.TrimOrNull(request.InvoiceNumber);
        entity.InvoiceDate = request.InvoiceDate;
        entity.ContractNumber = LicenseManagementValidation.TrimOrNull(request.ContractNumber);
        entity.ContractStartDate = request.ContractStartDate;
        entity.ContractEndDate = request.ContractEndDate;
        entity.SupplierCompanyId = request.SupplierCompanyId;
        entity.SupportCompanyId = request.SupportCompanyId;
        entity.ActualTotalCost = request.ActualTotalCost;
        entity.Currency = LicenseManagementValidation.TrimOrNull(request.Currency);
        entity.VatIncluded = request.VatIncluded;
        entity.Notes = LicenseManagementValidation.TrimOrNull(request.Notes);
        return entity;
    }

    private static LicenseAcquisition MapToEntity(LicenseAcquisition entity, UpdateLicenseAcquisitionRequest request)
    {
        entity.AcquisitionType = request.AcquisitionType;
        entity.Title = request.Title.Trim();
        entity.Description = LicenseManagementValidation.TrimOrNull(request.Description);
        entity.AcquisitionDate = request.AcquisitionDate;
        entity.TenderNumber = LicenseManagementValidation.TrimOrNull(request.TenderNumber);
        entity.TenderDate = request.TenderDate;
        entity.DirectPurchaseNumber = LicenseManagementValidation.TrimOrNull(request.DirectPurchaseNumber);
        entity.DmoOrderNumber = LicenseManagementValidation.TrimOrNull(request.DmoOrderNumber);
        entity.EbysNumber = LicenseManagementValidation.TrimOrNull(request.EbysNumber);
        entity.EbysDate = request.EbysDate;
        entity.InvoiceNumber = LicenseManagementValidation.TrimOrNull(request.InvoiceNumber);
        entity.InvoiceDate = request.InvoiceDate;
        entity.ContractNumber = LicenseManagementValidation.TrimOrNull(request.ContractNumber);
        entity.ContractStartDate = request.ContractStartDate;
        entity.ContractEndDate = request.ContractEndDate;
        entity.SupplierCompanyId = request.SupplierCompanyId;
        entity.SupportCompanyId = request.SupportCompanyId;
        entity.ActualTotalCost = request.ActualTotalCost;
        entity.Currency = LicenseManagementValidation.TrimOrNull(request.Currency);
        entity.VatIncluded = request.VatIncluded;
        entity.Notes = LicenseManagementValidation.TrimOrNull(request.Notes);
        return entity;
    }

    private static LicenseAcquisitionDetail Map(LicenseAcquisition entity) =>
        new(
            entity.Id,
            entity.AcquisitionType,
            entity.Title,
            entity.Description,
            entity.AcquisitionDate,
            entity.TenderNumber,
            entity.TenderDate,
            entity.DirectPurchaseNumber,
            entity.DmoOrderNumber,
            entity.EbysNumber,
            entity.EbysDate,
            entity.InvoiceNumber,
            entity.InvoiceDate,
            entity.ContractNumber,
            entity.ContractStartDate,
            entity.ContractEndDate,
            entity.SupplierCompanyId,
            entity.SupplierCompany?.Name,
            entity.SupportCompanyId,
            entity.SupportCompany?.Name,
            entity.ActualTotalCost,
            entity.Currency,
            entity.VatIncluded,
            entity.Notes,
            entity.Status,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);
}
