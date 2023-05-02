using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace DurableECommerceWorkflowIsolated.Extensions;

static class SendGridClientExtensions
{
    public static async Task PostAsync(this ISendGridClient sender, SendGridMessage message, ILogger log)
    {
        var sendGridKey = Environment.GetEnvironmentVariable("SendGridKey");
        // don't actually try to send SendGrid emails if we are just using example or missing email addresses
        var testMode = string.IsNullOrEmpty(sendGridKey) || sendGridKey == "TEST" || message.Personalizations.SelectMany(p => p.Tos.Select(t => t.Email))
            .Any(e => string.IsNullOrEmpty(e) || e.Contains("@example") || e.Contains("@email"));
        if (testMode)
        {
            log.LogWarning($"Sending email with body {message.HtmlContent}");
        }
        else
        {
            await sender.SendEmailAsync(message);
        }
    }
}