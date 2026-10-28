using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace SalesHierarchy.Web.Services
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            return Task.CompletedTask;
        }
    }
}
