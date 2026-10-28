using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace SalesHierarchy.Web
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // No-op in development; log not implemented as per security requirements
            return Task.CompletedTask;
        }
    }
}
