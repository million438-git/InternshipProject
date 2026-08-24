using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HawassaUnifiedCampusEventManagementSystem.Services
{
    public class CampusEmailSender : IEmailSender
    {
        private readonly ILogger<CampusEmailSender> _logger;
        private readonly IConfiguration _config;

        public CampusEmailSender(ILogger<CampusEmailSender> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("CampusEmailSender: Attempted to send email with empty recipient.");
                return;
            }

            var smtpHost = _config["Smtp:Host"] ?? Environment.GetEnvironmentVariable("SMTP_HOST");
            var smtpPortStr = _config["Smtp:Port"] ?? Environment.GetEnvironmentVariable("SMTP_PORT");
            var smtpUser = _config["Smtp:Username"] ?? Environment.GetEnvironmentVariable("SMTP_USERNAME");
            var smtpPass = _config["Smtp:Password"] ?? Environment.GetEnvironmentVariable("SMTP_PASSWORD");
            var senderEmail = _config["Smtp:SenderEmail"] ?? Environment.GetEnvironmentVariable("SMTP_SENDER_EMAIL") ?? "no-reply@hawassa.edu.et";
            var senderName = _config["Smtp:SenderName"] ?? Environment.GetEnvironmentVariable("SMTP_SENDER_NAME") ?? "HUCEMS Campus Portal";
            var enableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out bool ssl) ? ssl : true;

            int port = 587;
            if (!string.IsNullOrEmpty(smtpPortStr) && int.TryParse(smtpPortStr, out int parsedPort))
            {
                port = parsedPort;
            }

            // 1. If SMTP server is configured, dispatch via SMTP
            if (!string.IsNullOrWhiteSpace(smtpHost))
            {
                try
                {
                    using var client = new SmtpClient(smtpHost, port)
                    {
                        EnableSsl = enableSsl,
                        Timeout = 10000
                    };

                    if (!string.IsNullOrWhiteSpace(smtpUser) && !string.IsNullOrWhiteSpace(smtpPass))
                    {
                        client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                    }

                    using var mail = new MailMessage
                    {
                        From = new MailAddress(senderEmail, senderName),
                        Subject = subject,
                        Body = htmlMessage,
                        IsBodyHtml = true
                    };
                    mail.To.Add(new MailAddress(email));

                    await client.SendMailAsync(mail);
                    _logger.LogInformation("CampusEmailSender: Successfully dispatched SMTP email to {Email} | Subject: '{Subject}'", email, subject);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "CampusEmailSender: Failed to dispatch SMTP email to {Email}. Falling back to system log.", email);
                }
            }

            // 2. Fallback logger notification
            _logger.LogInformation("CampusEmailSender [LOCAL LOG]: Notification to {Email} | Subject: '{Subject}'", email, subject);
        }
    }
}
