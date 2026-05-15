using SasPortal.Application.Abstractions.Services;
using SasPortal.Application.Common.Models;

namespace SasPortal.UnitTests.Fakes;

public sealed class FakeAdManagementValidationService : IAdManagementValidationService
{
    public AdManagementValidationResult NextResult { get; set; } =
        new(true, "ok", DateTimeOffset.UtcNow, new List<AdManagementValidationDetail>());

    public Func<AdManagementConnectionParameters, AdManagementValidationRequest, AdManagementValidationResult>? Responder { get; set; }

    public int InvocationCount { get; private set; }

    public AdManagementConnectionParameters? LastConnection { get; private set; }

    public AdManagementValidationRequest? LastRequest { get; private set; }

    public Task<AdManagementValidationResult> ValidateConnectionAsync(
        AdManagementConnectionParameters connection,
        AdManagementValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        LastConnection = connection;
        LastRequest = request;
        var result = Responder?.Invoke(connection, request) ?? NextResult;
        return Task.FromResult(result);
    }
}
