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

public sealed class LicenseSeatAssignmentService(AppDbContext context) : ILicenseSeatAssignmentService
{
    private const string EntityName = "LicenseSeatAssignment";

    public async Task<PagedResult<LicenseSeatAssignmentListItem>> SearchAsync(
        LicenseSeatAssignmentListQuery query,
        CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = NormalizePaging(query.PageNumber, query.PageSize);

        var rows = context.LicenseSeatAssignments.AsNoTracking().AsQueryable();

        if (query.ActiveOnly)
        {
            rows = rows.Where(x => x.Status == LicenseSeatAssignmentStatus.Active);
        }
        else if (query.Status is { } status)
        {
            rows = rows.Where(x => x.Status == status);
        }

        if (query.ProductId is { } productId)
        {
            rows = rows.Where(x => x.Package.ProductId == productId);
        }

        if (query.PackageId is { } packageId)
        {
            rows = rows.Where(x => x.PackageId == packageId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = BuildILikeContainsPattern(query.Search);
            rows = rows.Where(x =>
                EF.Functions.ILike(x.DisplayName, pattern)
                || (x.Mail != null && EF.Functions.ILike(x.Mail, pattern))
                || (x.NationalId != null && EF.Functions.ILike(x.NationalId, pattern))
                || EF.Functions.ILike(x.Package.Product.Name, pattern));
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await rows
            .OrderByDescending(x => x.Status == LicenseSeatAssignmentStatus.Active)
            .ThenByDescending(x => x.AssignedDate)
            .ThenBy(x => x.DisplayName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LicenseSeatAssignmentListItem(
                x.Id,
                x.PackageId,
                x.Package.Product.Name,
                x.Package.Product.Brand,
                x.Package.Purchase.Title,
                x.DisplayName,
                x.AdObjectId,
                x.Mail,
                x.NationalId,
                x.Department,
                x.AssignedDate,
                x.ReleasedDate,
                x.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<LicenseSeatAssignmentListItem>(items, pageNumber, pageSize, totalCount, totalPages);
    }

    public async Task<LicensePackageSeatOverview?> GetByPackageAsync(
        Guid packageId,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var package = await context.LicensePackages
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Purchase)
            .FirstOrDefaultAsync(x => x.Id == packageId, cancellationToken);

        if (package is null)
        {
            return null;
        }

        var query = context.LicenseSeatAssignments
            .AsNoTracking()
            .Where(x => x.PackageId == packageId);

        if (!includeInactive)
        {
            query = query.Where(x => x.Status == LicenseSeatAssignmentStatus.Active);
        }

        var rows = await query
            .OrderByDescending(x => x.Status == LicenseSeatAssignmentStatus.Active)
            .ThenByDescending(x => x.AssignedDate)
            .ThenBy(x => x.DisplayName)
            .Select(x => new
            {
                Assignment = x,
                ReplacesDisplayName = x.ReplacesAssignment != null ? x.ReplacesAssignment.DisplayName : null,
            })
            .ToListAsync(cancellationToken);

        var activeCount = rows.Count(r => r.Assignment.Status == LicenseSeatAssignmentStatus.Active);
        var assignments = rows.Select(r => Map(r.Assignment, r.ReplacesDisplayName)).ToList();

        return new LicensePackageSeatOverview(
            package.Id,
            package.Product.Name,
            package.Purchase.Title,
            package.Quantity,
            activeCount,
            Math.Max(0, package.Quantity - activeCount),
            assignments);
    }

    public async Task<LicenseSeatAssignmentOperationResult> AssignAsync(
        AssignLicenseSeatRequest request,
        CancellationToken cancellationToken = default)
    {
        var person = NormalizePerson(request.Person);
        if (person is null)
        {
            return new LicenseSeatAssignmentOperationResult(false, "A display name is required for the seat holder.");
        }

        var package = await context.LicensePackages.FirstOrDefaultAsync(x => x.Id == request.PackageId, cancellationToken);
        if (package is null)
        {
            return new LicenseSeatAssignmentOperationResult(false, "License package was not found.");
        }

        if (request.SourceRequestItemId is { } sourceItemId
            && !await context.LicenseRequestItems.AnyAsync(x => x.Id == sourceItemId, cancellationToken))
        {
            return new LicenseSeatAssignmentOperationResult(false, "The linked request item was not found.");
        }

        var activeCount = await context.LicenseSeatAssignments
            .CountAsync(x => x.PackageId == package.Id && x.Status == LicenseSeatAssignmentStatus.Active, cancellationToken);
        if (activeCount >= package.Quantity)
        {
            return new LicenseSeatAssignmentOperationResult(
                false,
                "This package has no free seats. Increase the package quantity or release a seat first.");
        }

        if (await IsPersonActiveOnPackageAsync(package.Id, person, cancellationToken))
        {
            return new LicenseSeatAssignmentOperationResult(false, "This person already holds an active seat on this package.");
        }

        var now = DateTime.UtcNow;
        var entity = new LicenseSeatAssignment
        {
            PackageId = package.Id,
            AdObjectId = person.AdObjectId,
            DisplayName = person.DisplayName,
            SamAccountName = person.SamAccountName,
            UserPrincipalName = person.UserPrincipalName,
            Mail = person.Mail,
            NationalId = person.NationalId,
            Department = person.Department,
            Title = person.Title,
            AssignedDate = request.AssignedDate ?? DateOnly.FromDateTime(now),
            Status = LicenseSeatAssignmentStatus.Active,
            SourceRequestItemId = request.SourceRequestItemId,
            Note = LicenseManagementValidation.TrimOrNull(request.Note),
            CreatedAt = now,
            CreatedBy = request.Actor.ActorUserName,
        };

        await context.LicenseSeatAssignments.AddAsync(entity, cancellationToken);
        await WriteAuditAsync(
            context,
            "Assign",
            EntityName,
            entity.Id,
            $"License seat assigned to {entity.DisplayName} on package {package.Id}.",
            request.Actor.ActorUserId,
            request.Actor.ActorUserName,
            request.Actor.ActorIpAddress,
            request.Actor.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseSeatAssignmentOperationResult(true, "License seat assigned.", Map(entity, null));
    }

    public async Task<LicenseSeatAssignmentOperationResult> ReleaseAsync(
        ReleaseLicenseSeatRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.LicenseSeatAssignments.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            return new LicenseSeatAssignmentOperationResult(false, "License seat assignment was not found.");
        }

        if (entity.Status != LicenseSeatAssignmentStatus.Active)
        {
            return new LicenseSeatAssignmentOperationResult(false, "Only an active seat assignment can be released.");
        }

        var now = DateTime.UtcNow;
        entity.Status = LicenseSeatAssignmentStatus.Released;
        entity.ReleasedDate = request.ReleasedDate ?? DateOnly.FromDateTime(now);
        entity.Note = MergeNote(entity.Note, request.Note);
        entity.UpdatedAt = now;
        entity.UpdatedBy = request.Actor.ActorUserName;

        await WriteAuditAsync(
            context,
            "Release",
            EntityName,
            entity.Id,
            $"License seat released from {entity.DisplayName} on package {entity.PackageId}.",
            request.Actor.ActorUserId,
            request.Actor.ActorUserName,
            request.Actor.ActorIpAddress,
            request.Actor.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseSeatAssignmentOperationResult(true, "License seat released.", Map(entity, null));
    }

    public async Task<LicenseSeatAssignmentOperationResult> TransferAsync(
        TransferLicenseSeatRequest request,
        CancellationToken cancellationToken = default)
    {
        var person = NormalizePerson(request.NewPerson);
        if (person is null)
        {
            return new LicenseSeatAssignmentOperationResult(false, "A display name is required for the new seat holder.");
        }

        var current = await context.LicenseSeatAssignments.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (current is null)
        {
            return new LicenseSeatAssignmentOperationResult(false, "License seat assignment was not found.");
        }

        if (current.Status != LicenseSeatAssignmentStatus.Active)
        {
            return new LicenseSeatAssignmentOperationResult(false, "Only an active seat assignment can be transferred.");
        }

        if (await IsPersonActiveOnPackageAsync(current.PackageId, person, cancellationToken))
        {
            return new LicenseSeatAssignmentOperationResult(false, "The new holder already has an active seat on this package.");
        }

        var now = DateTime.UtcNow;
        var effectiveDate = request.TransferDate ?? DateOnly.FromDateTime(now);

        current.Status = LicenseSeatAssignmentStatus.Transferred;
        current.ReleasedDate = effectiveDate;
        current.Note = MergeNote(current.Note, request.Note);
        current.UpdatedAt = now;
        current.UpdatedBy = request.Actor.ActorUserName;

        var replacement = new LicenseSeatAssignment
        {
            PackageId = current.PackageId,
            AdObjectId = person.AdObjectId,
            DisplayName = person.DisplayName,
            SamAccountName = person.SamAccountName,
            UserPrincipalName = person.UserPrincipalName,
            Mail = person.Mail,
            NationalId = person.NationalId,
            Department = person.Department,
            Title = person.Title,
            AssignedDate = effectiveDate,
            Status = LicenseSeatAssignmentStatus.Active,
            ReplacesAssignmentId = current.Id,
            SourceRequestItemId = current.SourceRequestItemId,
            Note = LicenseManagementValidation.TrimOrNull(request.Note),
            CreatedAt = now,
            CreatedBy = request.Actor.ActorUserName,
        };

        await context.LicenseSeatAssignments.AddAsync(replacement, cancellationToken);
        await WriteAuditAsync(
            context,
            "Transfer",
            EntityName,
            replacement.Id,
            $"License seat transferred from {current.DisplayName} to {replacement.DisplayName} on package {current.PackageId}.",
            request.Actor.ActorUserId,
            request.Actor.ActorUserName,
            request.Actor.ActorIpAddress,
            request.Actor.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new LicenseSeatAssignmentOperationResult(true, "License seat transferred.", Map(replacement, current.DisplayName));
    }

    public async Task<CopyLicenseSeatsResult> CopySeatsAsync(
        CopyLicenseSeatsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SourcePackageId == request.TargetPackageId)
        {
            return new CopyLicenseSeatsResult(false, "Source and target packages must be different.");
        }

        var source = await context.LicensePackages.FirstOrDefaultAsync(x => x.Id == request.SourcePackageId, cancellationToken);
        if (source is null)
        {
            return new CopyLicenseSeatsResult(false, "Source package was not found.");
        }

        var target = await context.LicensePackages.FirstOrDefaultAsync(x => x.Id == request.TargetPackageId, cancellationToken);
        if (target is null)
        {
            return new CopyLicenseSeatsResult(false, "Target package was not found.");
        }

        var sourceSeats = await context.LicenseSeatAssignments
            .Where(x => x.PackageId == source.Id && x.Status == LicenseSeatAssignmentStatus.Active)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        if (sourceSeats.Count == 0)
        {
            return new CopyLicenseSeatsResult(false, "The source package has no active seats to copy.");
        }

        var targetSeats = await context.LicenseSeatAssignments
            .Where(x => x.PackageId == target.Id && x.Status == LicenseSeatAssignmentStatus.Active)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var assignedDate = request.AssignedDate ?? DateOnly.FromDateTime(now);
        var copied = 0;
        var skipped = 0;

        foreach (var seat in sourceSeats)
        {
            var alreadyThere = targetSeats.Any(t => SamePerson(t, seat));
            var full = targetSeats.Count(t => t.Status == LicenseSeatAssignmentStatus.Active) >= target.Quantity;
            if (alreadyThere || full)
            {
                skipped++;
                continue;
            }

            var copy = new LicenseSeatAssignment
            {
                PackageId = target.Id,
                AdObjectId = seat.AdObjectId,
                DisplayName = seat.DisplayName,
                SamAccountName = seat.SamAccountName,
                UserPrincipalName = seat.UserPrincipalName,
                Mail = seat.Mail,
                NationalId = seat.NationalId,
                Department = seat.Department,
                Title = seat.Title,
                AssignedDate = assignedDate,
                Status = LicenseSeatAssignmentStatus.Active,
                ReplacesAssignmentId = seat.Id,
                SourceRequestItemId = seat.SourceRequestItemId,
                Note = "Copied from renewed package.",
                CreatedAt = now,
                CreatedBy = request.Actor.ActorUserName,
            };
            await context.LicenseSeatAssignments.AddAsync(copy, cancellationToken);
            targetSeats.Add(copy);
            copied++;
        }

        if (copied == 0)
        {
            return new CopyLicenseSeatsResult(false, "No seats were copied (all holders are already on the target package or it is full).", 0, skipped);
        }

        await WriteAuditAsync(
            context,
            "Copy",
            EntityName,
            target.Id,
            $"Copied {copied} license seat(s) from package {source.Id} to package {target.Id}.",
            request.Actor.ActorUserId,
            request.Actor.ActorUserName,
            request.Actor.ActorIpAddress,
            request.Actor.ActorUserAgent,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var message = skipped > 0
            ? $"Copied {copied} seat(s); skipped {skipped} (already present or package full)."
            : $"Copied {copied} seat(s).";
        return new CopyLicenseSeatsResult(true, message, copied, skipped);
    }

    private async Task<bool> IsPersonActiveOnPackageAsync(Guid packageId, LicenseSeatPersonInput person, CancellationToken cancellationToken)
    {
        var candidates = await context.LicenseSeatAssignments
            .Where(x => x.PackageId == packageId && x.Status == LicenseSeatAssignmentStatus.Active)
            .Select(x => new { x.AdObjectId, x.NationalId, x.Mail, x.UserPrincipalName })
            .ToListAsync(cancellationToken);

        return candidates.Any(c =>
            (!string.IsNullOrWhiteSpace(person.AdObjectId) && c.AdObjectId == person.AdObjectId)
            || (!string.IsNullOrWhiteSpace(person.NationalId) && c.NationalId == person.NationalId)
            || (!string.IsNullOrWhiteSpace(person.Mail) && string.Equals(c.Mail, person.Mail, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(person.UserPrincipalName) && string.Equals(c.UserPrincipalName, person.UserPrincipalName, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool SamePerson(LicenseSeatAssignment a, LicenseSeatAssignment b) =>
        (!string.IsNullOrWhiteSpace(a.AdObjectId) && a.AdObjectId == b.AdObjectId)
        || (!string.IsNullOrWhiteSpace(a.NationalId) && a.NationalId == b.NationalId)
        || (!string.IsNullOrWhiteSpace(a.Mail) && string.Equals(a.Mail, b.Mail, StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(a.UserPrincipalName) && string.Equals(a.UserPrincipalName, b.UserPrincipalName, StringComparison.OrdinalIgnoreCase));

    private static LicenseSeatPersonInput? NormalizePerson(LicenseSeatPersonInput person)
    {
        var displayName = person.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        return new LicenseSeatPersonInput(
            LicenseManagementValidation.TrimOrNull(person.AdObjectId),
            displayName,
            LicenseManagementValidation.TrimOrNull(person.SamAccountName),
            LicenseManagementValidation.TrimOrNull(person.UserPrincipalName),
            LicenseManagementValidation.TrimOrNull(person.Mail),
            LicenseManagementValidation.TrimOrNull(person.NationalId),
            LicenseManagementValidation.TrimOrNull(person.Department),
            LicenseManagementValidation.TrimOrNull(person.Title));
    }

    private static string? MergeNote(string? existing, string? addition)
    {
        var extra = addition?.Trim();
        if (string.IsNullOrWhiteSpace(extra))
        {
            return existing;
        }

        return string.IsNullOrWhiteSpace(existing) ? extra : $"{existing}\n{extra}";
    }

    private static LicenseSeatAssignmentItem Map(LicenseSeatAssignment entity, string? replacesDisplayName) =>
        new(
            entity.Id,
            entity.PackageId,
            entity.AdObjectId,
            entity.DisplayName,
            entity.SamAccountName,
            entity.UserPrincipalName,
            entity.Mail,
            entity.NationalId,
            entity.Department,
            entity.Title,
            entity.AssignedDate,
            entity.ReleasedDate,
            entity.Status,
            entity.ReplacesAssignmentId,
            replacesDisplayName,
            entity.SourceRequestItemId,
            entity.Note,
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy);
}
