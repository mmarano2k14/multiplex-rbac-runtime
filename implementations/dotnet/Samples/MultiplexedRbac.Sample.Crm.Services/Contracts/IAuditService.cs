using MultiplexedRbac.Core.Authorization.Attributes;

namespace MultiplexedRbac.Sample.Crm.Services.Contracts
{
    public interface IAuditService
    {
        [RequireCapability("audit", "events", "write")]
        Task WriteAsync(string message, CancellationToken ct = default);
    }
}
