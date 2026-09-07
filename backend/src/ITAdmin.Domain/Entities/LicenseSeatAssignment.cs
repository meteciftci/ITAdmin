using ITAdmin.Domain.Common;
using ITAdmin.Domain.Enums;

namespace ITAdmin.Domain.Entities;

/// <summary>
/// Records that one person holds one seat of a <see cref="LicensePackage"/>. Unlike the
/// request-&gt;fulfilment chain (which tracks how many seats a request line consumed), this is the
/// operational "who has it right now" roster: it survives renewals (seats are copied onto the new
/// package) and person-to-person transfers (the old row is closed and linked from the new one).
/// </summary>
public class LicenseSeatAssignment : AuditableEntity
{
    public Guid PackageId { get; set; }

    // Person snapshot. Ad object id is optional because legacy/imported rows only carry name + national id.
    public string? AdObjectId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? SamAccountName { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? Mail { get; set; }
    public string? NationalId { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }

    public DateOnly AssignedDate { get; set; }
    public DateOnly? ReleasedDate { get; set; }
    public LicenseSeatAssignmentStatus Status { get; set; } = LicenseSeatAssignmentStatus.Active;

    /// <summary>When this seat came from a transfer, the assignment it took over from.</summary>
    public Guid? ReplacesAssignmentId { get; set; }

    /// <summary>Optional trace back to the request line that originally justified this seat.</summary>
    public Guid? SourceRequestItemId { get; set; }

    public string? Note { get; set; }

    public LicensePackage Package { get; set; } = null!;
    public LicenseSeatAssignment? ReplacesAssignment { get; set; }
}
