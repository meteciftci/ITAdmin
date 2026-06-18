namespace ITAdmin.Application.Common.Models;

public sealed record CompleteSetupResult(
    bool IsCompleted,
    string Message);
