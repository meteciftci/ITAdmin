namespace ITAdmin.Application.Abstractions.Security;

public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
