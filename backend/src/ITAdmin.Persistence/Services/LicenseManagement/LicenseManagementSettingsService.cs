using Microsoft.EntityFrameworkCore;
using ITAdmin.Application.Abstractions.Services;
using ITAdmin.Application.Common.LicenseManagement;
using ITAdmin.Application.Common.Models.LicenseManagement;
using ITAdmin.Domain.Entities;
using ITAdmin.Persistence.Context;
using static ITAdmin.Persistence.Services.LicenseManagement.LicenseManagementServiceHelpers;

namespace ITAdmin.Persistence.Services.LicenseManagement;

public sealed class LicenseManagementSettingsService(AppDbContext context) : ILicenseManagementSettingsService
{
  private const string DefaultCurrencyValue = "TRY";
  private const int DefaultRenewalReminderDaysValue = 60;

  public async Task<LicenseManagementSettingsModel> GetSettingsAsync(CancellationToken cancellationToken = default)
  {
    var entity = await context.LicenseManagementSettings
      .AsNoTracking()
      .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
      .FirstOrDefaultAsync(cancellationToken);

    return MapToModel(entity);
  }

  public async Task<UpdateLicenseManagementSettingsResult> UpdateSettingsAsync(
    UpdateLicenseManagementSettingsRequest request,
    CancellationToken cancellationToken = default)
  {
    var validationError = ValidateUpdateRequest(request);
    if (validationError is not null)
    {
      return new UpdateLicenseManagementSettingsResult(false, validationError);
    }

    var entity = await context.LicenseManagementSettings
      .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
      .FirstOrDefaultAsync(cancellationToken);

    var now = DateTime.UtcNow;
    var actor = request.ActorUserName ?? "system";
    var isNew = entity is null;

    entity ??= new LicenseManagementSettings
    {
      CreatedAt = now,
      CreatedBy = actor
    };

    entity.DefaultCurrency = request.DefaultCurrency.Trim().ToUpperInvariant();
    entity.DefaultVatIncluded = request.DefaultVatIncluded;
    entity.DefaultRenewalReminderDays = request.DefaultRenewalReminderDays;
    entity.DefaultRenewalRecipients = LicenseManagementValidation.TrimOrNull(request.DefaultRenewalRecipients);
    entity.DefaultRenewalCcRecipients = LicenseManagementValidation.TrimOrNull(request.DefaultRenewalCcRecipients);
    entity.Notes = LicenseManagementValidation.TrimOrNull(request.Notes);
    entity.UpdatedAt = now;
    entity.UpdatedBy = actor;

    if (isNew)
    {
      await context.LicenseManagementSettings.AddAsync(entity, cancellationToken);
    }

    await WriteAuditAsync(
      context,
      "UpdateSettings",
      "LicenseManagementSettings",
      entity.Id,
      "License management settings updated.",
      request.ActorUserId,
      request.ActorUserName,
      request.ActorIpAddress,
      request.ActorUserAgent,
      cancellationToken);
    await context.SaveChangesAsync(cancellationToken);

    return new UpdateLicenseManagementSettingsResult(true, "License management settings updated.", MapToModel(entity));
  }

  private static string? ValidateUpdateRequest(UpdateLicenseManagementSettingsRequest request)
  {
    if (string.IsNullOrWhiteSpace(request.DefaultCurrency))
    {
      return "Default currency is required.";
    }

    if (request.DefaultCurrency.Trim().Length > 10)
    {
      return "Default currency length is invalid.";
    }

    if (request.DefaultRenewalReminderDays <= 0)
    {
      return "Default renewal reminder days must be positive.";
    }

    return null;
  }

  private static LicenseManagementSettingsModel MapToModel(LicenseManagementSettings? entity) =>
    entity is null
      ? new LicenseManagementSettingsModel(
        DefaultCurrencyValue,
        false,
        DefaultRenewalReminderDaysValue,
        null,
        null,
        null,
        null,
        null)
      : new LicenseManagementSettingsModel(
        entity.DefaultCurrency,
        entity.DefaultVatIncluded,
        entity.DefaultRenewalReminderDays,
        entity.DefaultRenewalRecipients,
        entity.DefaultRenewalCcRecipients,
        entity.Notes,
        entity.UpdatedAt ?? entity.CreatedAt,
        entity.UpdatedBy ?? entity.CreatedBy);
}
