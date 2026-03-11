namespace MultiplexedRbac.Sample.Crm.Api.Auth
{
    public interface IDemoBootstrapTicketProtector
    {
        string Protect(DemoBootstrapTicket ticket);
        DemoBootstrapTicket? Unprotect(string protectedValue);
    }
}
