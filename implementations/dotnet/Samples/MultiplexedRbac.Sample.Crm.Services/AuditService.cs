using MultiplexedRbac.Sample.Crm.Services.Contracts;

namespace MultiplexedRbac.Sample.Crm.Services
{
    public sealed class AuditService : IAuditService
    {
        public Task WriteAsync(string message, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
