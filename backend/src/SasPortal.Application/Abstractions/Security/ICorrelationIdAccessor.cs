namespace SasPortal.Application.Abstractions.Security;

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
