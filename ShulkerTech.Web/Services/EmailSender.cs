using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace ShulkerTech.Web.Services;

public class EmailSender(
    HttpClient http,
    IOptions<EmailSettings> options,
    ILogger<EmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _settings = options.Value;

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        var payload = JsonSerializer.Serialize(new
        {
            from = $"{_settings.FromName} <{_settings.FromAddress}>",
            to = new[] { email },
            subject,
            html = htmlMessage,
        });

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await http.PostAsync("https://api.resend.com/emails", content);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogCritical("Resend API key is invalid or missing. Check the RESEND_API_KEY environment variable.");
            throw new InvalidOperationException("Email service is misconfigured. Please contact an administrator.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            logger.LogError("Resend returned {StatusCode} sending to {Email}: {Body}",
                (int)response.StatusCode, email, body);
            response.EnsureSuccessStatusCode();
        }
    }
}
