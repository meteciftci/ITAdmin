namespace ITAdmin.Application.Common.Models;

public sealed record UploadBrandingLogoRequest(
    byte[] Content,
    string FileExtension,
    string ContentType,
    string UploadDirectoryPath,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
