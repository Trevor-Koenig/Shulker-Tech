using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ShulkerTech.Web.Services;

namespace ShulkerTech.Web.Areas.Admin.Pages.Site;

public class DbExportModel(IDatabaseExporter exporter, ILogger<DbExportModel> logger) : PageModel
{
    [TempData]
    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        try
        {
            var buffer = new MemoryStream();
            await exporter.ExportAsync(buffer, ct);
            buffer.Position = 0;

            var fileName = $"shulkertech-export-{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql.gz";
            return File(buffer, "application/gzip", fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database export failed");
            ErrorMessage = "Export failed — check server logs for details.";
            return RedirectToPage();
        }
    }
}
