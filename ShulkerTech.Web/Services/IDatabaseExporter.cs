namespace ShulkerTech.Web.Services;

public interface IDatabaseExporter
{
    /// <summary>Runs a full database export and writes gzip-compressed SQL to <paramref name="destination"/>.</summary>
    Task ExportAsync(Stream destination, CancellationToken ct = default);
}
