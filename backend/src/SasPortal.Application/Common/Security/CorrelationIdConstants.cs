namespace SasPortal.Application.Common.Security;

public static class CorrelationIdConstants
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId";
    public const int MaxLength = 64;
}
