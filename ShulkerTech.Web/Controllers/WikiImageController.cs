using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ShulkerTech.Web.Controllers;

[ApiController]
[Route("api/wiki")]
[Authorize]
public class WikiImageController(
    IWebHostEnvironment env,
    IAntiforgery antiforgery,
    ILogger<WikiImageController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedMime = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/gif", "image/webp" };

    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private const long MaxBytes = 20 * 1024 * 1024; // 20 MB

    [HttpPost("upload-image")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        // [ValidateAntiForgeryToken] doesn't fire on [ApiController] — validate manually.
        // ASP.NET reads the "RequestVerificationToken" header (case-insensitive).
        try { await antiforgery.ValidateRequestAsync(HttpContext); }
        catch (AntiforgeryValidationException) { return BadRequest(new { error = "Invalid CSRF token." }); }

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxBytes)
            return BadRequest(new { error = "File exceeds the 20 MB limit." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExt.Contains(ext))
            return BadRequest(new { error = "File extension not allowed." });

        if (!AllowedMime.Contains(file.ContentType))
            return BadRequest(new { error = "MIME type not allowed." });

        var dir = Path.Combine(env.WebRootPath, "uploads", "wiki");
        Directory.CreateDirectory(dir);

        // Use a GUID filename — never the original — to prevent double-extension attacks.
        var name = $"{Guid.NewGuid():N}{ext}";
        await using var fs = System.IO.File.Create(Path.Combine(dir, name));
        await file.CopyToAsync(fs);

        logger.LogInformation("Wiki image uploaded: {Name} by {User}", name, User.Identity?.Name);

        // EasyMDE expects { data: { filePath: "..." } } on success
        return Ok(new { data = new { filePath = $"/uploads/wiki/{name}" } });
    }
}
