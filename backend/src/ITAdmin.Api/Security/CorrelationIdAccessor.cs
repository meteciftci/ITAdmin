using ITAdmin.Application.Abstractions.Security;
using ITAdmin.Application.Common.Security;

namespace ITAdmin.Api.Security;

public sealed class CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    public string? CorrelationId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            if (context.Items.TryGetValue(CorrelationIdConstants.HttpContextItemKey, out var value)
                && value is string correlationId
                && !string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }

            return null;
        }
    }
}
