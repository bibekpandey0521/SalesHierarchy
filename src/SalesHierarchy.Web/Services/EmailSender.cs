using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SalesHierarchy.Web.Services
{
    /// <summary>
    /// SMTP based e-mail sender configured through the "Email" configuration section.
    /// When e-mail is not configured the sender throws so callers can report the
    /// invitation as failed/pending instead of pretending it was delivered.
    /// Secrets (host/user/password) must come from user secrets or environment variables.
    ///
    /// Local development can set Email:PickupDirectory instead of an SMTP host. Messages
    /// are then written as .eml files into that folder instead of being sent. The folder
    /// contains working password-setup links, so it must never be committed.
    /// </summary>
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration configuration, IWebHostEnvironment environment, ILogger<EmailSender> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var section = _configuration.GetSection("Email");
            var enabled = section.GetValue<bool>("Enabled");
            if (!enabled)
            {
                throw new InvalidOperationException("E-mail delivery is not configured.");
            }

            var from = section["FromAddress"];
            if (string.IsNullOrWhiteSpace(from))
            {
                throw new InvalidOperationException("E-mail delivery is not configured.");
            }

            var pickupDirectory = section["PickupDirectory"];
            var usesPickupDirectory = !string.IsNullOrWhiteSpace(pickupDirectory);
            var host = section["SmtpHost"];
            if (!usesPickupDirectory && string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException("E-mail delivery is not configured.");
            }

            var port = section.GetValue<int?>("SmtpPort") ?? 587;
            var useSsl = section.GetValue<bool?>("UseSsl") ?? true;
            var userName = section["UserName"];
            var password = section["Password"];
            var fromName = section["FromName"] ?? "SalesHierarchy";

            using var message = new MailMessage
            {
                From = new MailAddress(from, fromName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(email));

            using var client = new SmtpClient { Host = "localhost" };
            if (usesPickupDirectory)
            {
                var fullPath = Path.IsPathRooted(pickupDirectory)
                    ? pickupDirectory!
                    : Path.GetFullPath(pickupDirectory!, _environment.ContentRootPath);

                Directory.CreateDirectory(fullPath);
                client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                client.PickupDirectoryLocation = fullPath;
            }
            else
            {
                client.Host = host!;
                client.Port = port;
                client.EnableSsl = useSsl;
                if (!string.IsNullOrWhiteSpace(userName))
                {
                    client.Credentials = new NetworkCredential(userName, password);
                }
            }

            await client.SendMailAsync(message);

            if (usesPickupDirectory)
            {
                _logger.LogInformation("Notification e-mail for {Recipient} written to the local pickup directory.", email);
            }
            else
            {
                _logger.LogInformation("Notification e-mail sent to {Recipient} with subject {Subject}.", email, subject);
            }
        }
    }
}
