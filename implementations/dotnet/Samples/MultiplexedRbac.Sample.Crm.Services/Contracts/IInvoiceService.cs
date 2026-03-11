using MultiplexedRbac.Core.Authorization.Attributes;
using MultiplexedRbac.Core.Authorization.Scope;

namespace MultiplexedRbac.Sample.Crm.Services.Contracts;

// Example: Interface-level requirement applies to all methods (if your proxy supports it).
// If you prefer method-only, remove this class-level attribute.
[RequireCapability("billing", "invoice", "read")]
public interface IInvoiceService
{
    // Method-level override / stronger requirement
    [RequireCapability("billing", "invoice", "refund")]
    Task RefundAsync(string invoiceId, decimal amount, CancellationToken ct = default);

    // Will inherit interface-level requirement if your proxy supports it
    Task<string> GetAsync(string invoiceId, CancellationToken ct = default);

    // Another explicit method capability
    [RequireCapability("billing", "invoice", "capture")]
    Task CapturePaymentAsync(string invoiceId, decimal amount, CancellationToken ct = default);
}