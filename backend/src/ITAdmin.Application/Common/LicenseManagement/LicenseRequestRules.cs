using ITAdmin.Domain.Enums;

namespace ITAdmin.Application.Common.LicenseManagement;

public static class LicenseRequestRules
{
    public static readonly IReadOnlySet<LicenseRequestStatus> ManualRequestStatuses =
        new HashSet<LicenseRequestStatus>
        {
            LicenseRequestStatus.Draft,
            LicenseRequestStatus.Pending,
            LicenseRequestStatus.InReview,
            LicenseRequestStatus.Rejected,
            LicenseRequestStatus.Cancelled,
            LicenseRequestStatus.Archived,
        };

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
