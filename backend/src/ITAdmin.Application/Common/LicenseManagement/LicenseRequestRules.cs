using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.LicenseManagement;

public static class LicenseRequestRules
{
    public static readonly IReadOnlySet<LicenseRequestItemStatus> ManualItemStatuses =
        new HashSet<LicenseRequestItemStatus>
        {
            LicenseRequestItemStatus.Pending,
            LicenseRequestItemStatus.InReview,
            LicenseRequestItemStatus.Approved,
            LicenseRequestItemStatus.Rejected,
            LicenseRequestItemStatus.Cancelled,
        };

    public static readonly IReadOnlySet<LicenseRequestItemUserStatus> ManualUserStatuses =
        new HashSet<LicenseRequestItemUserStatus>
        {
            LicenseRequestItemUserStatus.Pending,
            LicenseRequestItemUserStatus.Approved,
            LicenseRequestItemUserStatus.Rejected,
            LicenseRequestItemUserStatus.Cancelled,
        };

    /// <summary>
    /// Derives an approved item's status from how much of its approved quantity has been fulfilled.
    /// Not fulfilled yet -> Approved; fully fulfilled -> Fulfilled; otherwise -> PartiallyFulfilled.
    /// </summary>
    public static LicenseRequestItemStatus DeriveItemStatus(int? approvedQuantity, int fulfilledQuantity)
    {
        var approved = approvedQuantity ?? 0;
        if (fulfilledQuantity <= 0)
        {
            return LicenseRequestItemStatus.Approved;
        }

        return fulfilledQuantity >= approved
            ? LicenseRequestItemStatus.Fulfilled
            : LicenseRequestItemStatus.PartiallyFulfilled;
    }

    /// <summary>
    /// Derives a request's status from its item statuses. Item status is the single source of truth;
    /// the request status is never set manually. Cancelled/Rejected items are ignored when deciding
    /// completeness unless every item is terminal (all rejected -> Rejected, otherwise Cancelled).
    /// </summary>
    public static LicenseRequestStatus DeriveRequestStatus(IEnumerable<LicenseRequestItemStatus> itemStatuses)
    {
        var all = itemStatuses.ToList();
        if (all.Count == 0)
        {
            return LicenseRequestStatus.Pending;
        }

        var active = all
            .Where(status => status is not LicenseRequestItemStatus.Cancelled
                and not LicenseRequestItemStatus.Rejected)
            .ToList();

        if (active.Count == 0)
        {
            return all.Any(status => status is LicenseRequestItemStatus.Rejected)
                ? LicenseRequestStatus.Rejected
                : LicenseRequestStatus.Cancelled;
        }

        if (active.All(status => status is LicenseRequestItemStatus.Fulfilled))
        {
            return LicenseRequestStatus.Fulfilled;
        }

        if (active.Any(status => status is LicenseRequestItemStatus.Fulfilled
            or LicenseRequestItemStatus.PartiallyFulfilled))
        {
            return LicenseRequestStatus.PartiallyFulfilled;
        }

        if (active.Any(status => status is LicenseRequestItemStatus.InReview
            or LicenseRequestItemStatus.Approved))
        {
            return LicenseRequestStatus.InReview;
        }

        return LicenseRequestStatus.Pending;
    }

    public const string DuplicateProductMessage =
        "Aynı ürün talebe birden fazla kez eklenemez.";

    public const string DuplicateUserMessage =
        "Aynı kullanıcı aynı ürün kalemine birden fazla kez eklenemez.";

    public static (string? ExternalRequestNumber, string? EbysNumber, DateOnly? EbysDate) NormalizeSourceFields(
        LicenseRequestSource source,
        string? externalRequestNumber,
        string? ebysNumber,
        DateOnly? ebysDate) =>
        source switch
        {
            LicenseRequestSource.OfficialLetter => (
                null,
                LicenseManagementValidation.TrimOrNull(ebysNumber),
                ebysDate),
            LicenseRequestSource.CorporateRequestSystem => (
                LicenseManagementValidation.TrimOrNull(externalRequestNumber),
                null,
                null),
            _ => (null, null, null),
        };

    public static string? ValidateSourceFields(
        LicenseRequestSource source,
        string? externalRequestNumber,
        string? ebysNumber,
        DateOnly? ebysDate)
    {
        switch (source)
        {
            case LicenseRequestSource.OfficialLetter:
                if (string.IsNullOrWhiteSpace(ebysNumber))
                {
                    return "EBYS number is required for official letter requests.";
                }

                if (ebysDate is null)
                {
                    return "EBYS date is required for official letter requests.";
                }

                break;
            case LicenseRequestSource.CorporateRequestSystem:
                if (string.IsNullOrWhiteSpace(externalRequestNumber))
                {
                    return "External request number is required for corporate request system source.";
                }

                break;
        }

        return null;
    }
}
