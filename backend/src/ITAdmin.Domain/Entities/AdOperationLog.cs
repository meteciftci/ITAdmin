using ITAdmin.Domain.Common;

namespace ITAdmin.Domain.Entities;

public class AdOperationLog : BaseEntity
{
    public string OperationType { get; set; } = string.Empty;
    public string? TargetObjectType { get; set; }
    public string? TargetDistinguishedName { get; set; }
    public string? TargetObjectGuid { get; set; }
    public string? TargetSamAccountName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DomainController { get; set; }
    public string? RequestSummaryJson { get; set; }
    public string? BeforeSnapshotJson { get; set; }
    public string? AfterSnapshotJson { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorUserName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
