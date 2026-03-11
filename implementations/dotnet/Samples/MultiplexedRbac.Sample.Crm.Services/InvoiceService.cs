namespace MultiplexedRbac.Sample.Crm.Services.Contracts;

public sealed class InvoiceService : IInvoiceService
{
    public Task RefundAsync(string invoiceId, decimal amount, CancellationToken ct = default)
    {
        // simulate long running / PSP call
        return Task.Delay(150, ct);
    }

    public Task<string> GetAsync(string invoiceId, CancellationToken ct = default)
    {
        return Task.FromResult($"invoice:{invoiceId}");
    }

    public Task CapturePaymentAsync(string invoiceId, decimal amount, CancellationToken ct = default)
    {
        return Task.Delay(100, ct);
    }
}