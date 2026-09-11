using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.LicenseManagement;

public static class LicenseManagementLifecycleRules
{
    public static bool IsPurchaseOpenForPackages(LicensePurchaseStatus status) =>
        status is LicensePurchaseStatus.Draft or LicensePurchaseStatus.Active;

    public static bool IsPurchaseTransitionAllowed(
        LicensePurchaseStatus current,
        LicensePurchaseStatus target) =>
        current == target
        || current switch
        {
            LicensePurchaseStatus.Draft => target is LicensePurchaseStatus.Active
                or LicensePurchaseStatus.Cancelled
                or LicensePurchaseStatus.Archived,
            LicensePurchaseStatus.Active => target is LicensePurchaseStatus.Cancelled
                or LicensePurchaseStatus.Archived,
            LicensePurchaseStatus.Cancelled => target == LicensePurchaseStatus.Archived,
            LicensePurchaseStatus.Archived => false,
            _ => false,
        };

    public static bool IsPackageTransitionAllowed(
        LicensePackageStatus current,
        LicensePackageStatus target) =>
        current == target
        || current switch
        {
            LicensePackageStatus.Active => target is LicensePackageStatus.Expired
                or LicensePackageStatus.Cancelled
                or LicensePackageStatus.Suspended
                or LicensePackageStatus.Archived,
            LicensePackageStatus.Suspended => target is LicensePackageStatus.Active
                or LicensePackageStatus.Expired
                or LicensePackageStatus.Cancelled
                or LicensePackageStatus.Archived,
            LicensePackageStatus.Expired => target == LicensePackageStatus.Archived,
            LicensePackageStatus.Cancelled => target == LicensePackageStatus.Archived,
            LicensePackageStatus.Archived => false,
            _ => false,
        };

    public static bool IsPackageActive(LicensePackageStatus status) =>
        status == LicensePackageStatus.Active;

    public static string? ValidatePackageDates(
        DateOnly? startDate,
        DateOnly? endDate,
        bool isPerpetual,
        bool renewalRequired,
        DateOnly? renewalDate)
    {
        if (!LicenseManagementValidation.IsValidDateRange(startDate, endDate, out _))
        {
            return "License end date cannot be earlier than the start date.";
        }

        if (isPerpetual && endDate is not null)
        {
            return "A perpetual license cannot have an end date.";
        }

        if (renewalRequired && renewalDate is null)
        {
            return "Renewal date is required when renewal is enabled.";
        }

        if (!renewalRequired && renewalDate is not null)
        {
            return "Renewal date must be empty when renewal is not enabled.";
        }

        if (renewalDate is { } renewal && startDate is { } start && renewal < start)
        {
            return "Renewal date cannot be earlier than the license start date.";
        }

        if (renewalDate is { } renewalValue && endDate is { } end && renewalValue > end)
        {
            return "Renewal date cannot be later than the license end date.";
        }

        return null;
    }
}
