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
}
