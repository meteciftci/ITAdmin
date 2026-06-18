namespace ITAdmin.Application.Common.Models;

public sealed record UploadBrandingFaviconRequest(
    byte[] Content,
    string FileExtension,
    string ContentType,
    string UploadDirectoryPath,
    Guid? ActorUserId,
    string? ActorUserName,
    string? ActorIpAddress,
    string? ActorUserAgent);
