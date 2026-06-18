namespace ITAdmin.Api.Contracts.Setup;

public sealed record CompleteSetupResponse(
    bool IsCompleted,
    string Message);
