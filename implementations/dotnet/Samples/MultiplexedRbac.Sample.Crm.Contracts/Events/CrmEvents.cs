using NServiceBus;

namespace MultiplexedRbac.Sample.Crm.Contracts.Events;

// ---------------------------
// Commands (optional)
// ---------------------------
// In this sample we publish events from the API.
// If you prefer "command to worker", change IEvent -> ICommand.
// ---------------------------

public sealed class InvoiceRefundRequested : IEvent
{
    public string InvoiceId { get; init; } = "";
    public decimal Amount { get; init; }
}

public sealed class InvoiceRefundProcessed : IEvent
{
    public string InvoiceId { get; init; } = "";
    public string ProviderRef { get; init; } = ""; // optional audit
}

public sealed class InvoiceRefundDenied : IEvent
{
    public string InvoiceId { get; init; } = "";
    public string Reason { get; init; } = "";
}