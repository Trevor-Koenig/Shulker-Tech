using System.Diagnostics;
using System.IO.Compression;
using Npgsql;

namespace ShulkerTech.Web.Services;

public class PgDumpExporter(IConfiguration configuration) : IDatabaseExporter
{
    public async Task ExportAsync(Stream destination, CancellationToken ct = default)
    {
        var csb = new NpgsqlConnectionStringBuilder(
            configuration.GetConnectionString("DefaultConnection")!);

        var psi = new ProcessStartInfo
        {
            FileName = "pg_dump",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-h"); psi.ArgumentList.Add(csb.Host!);
        psi.ArgumentList.Add("-p"); psi.ArgumentList.Add((csb.Port == 0 ? 5432 : csb.Port).ToString());
        psi.ArgumentList.Add("-U"); psi.ArgumentList.Add(csb.Username!);
        psi.ArgumentList.Add(csb.Database!);
        psi.Environment["PGPASSWORD"] = csb.Password ?? "";

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start pg_dump.");

        string stderrResult;
        await using (var gzip = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true))
        {
            var copyTask = process.StandardOutput.BaseStream.CopyToAsync(gzip, ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(copyTask, stderrTask);
            stderrResult = stderrTask.Result;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(4));
        await process.WaitForExitAsync(timeoutCts.Token);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"pg_dump exited with code {process.ExitCode}: {stderrResult}");
    }
}
