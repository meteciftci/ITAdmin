using SasPortal.Application.Common.Models;

namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdDeletedObjectRestoreRequestBody(
    AdDeletedObjectRestoreTargetMode? RestoreTargetMode = null,
    string? TargetPathDistinguishedName = null);
