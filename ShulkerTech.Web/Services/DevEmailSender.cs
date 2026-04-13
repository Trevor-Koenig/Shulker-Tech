using Microsoft.AspNetCore.Identity.UI.Services;

namespace ShulkerTech.Web.Services;

/// <summary>
/// Development-only email sender. Writes each email as an .html file under
/// the "dev-emails" folder in the project root so you can open it in a browser
/// and click confirmation links without a real mail server.
/// </summary>
public class DevEmailSender(ILogger<DevEmailSender> logger, IWebHostEnvironment env) : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var dir = Path.Combine(env.ContentRootPath, "dev-emails");
        Directory.CreateDirectory(dir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var safeTo = string.Concat(email.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{timestamp}_{safeTo}.html";
        var path = Path.Combine(dir, fileName);

        var content = $"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"><title>{subject}</title></head>
            <body style="font-family:sans-serif;max-width:640px;margin:2rem auto;padding:1rem">
              <p style="color:#888;font-size:.8rem">To: {email}</p>
              <p style="color:#888;font-size:.8rem">Subject: {subject}</p>
              <hr>
              {htmlMessage}
            </body>
            </html>
            """;

        File.WriteAllText(path, content);

        logger.LogInformation("[DEV EMAIL] Written to {Path}", path);
        return Task.CompletedTask;
    }
}
