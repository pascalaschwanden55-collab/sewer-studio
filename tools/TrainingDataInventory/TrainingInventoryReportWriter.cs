using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace TrainingDataInventory;

internal sealed class TrainingInventoryReportWriter
{
    public async Task<TrainingInventoryReportFiles> WriteAsync(
        TrainingDataInventoryReport report,
        TrainingInventoryReportOutputPaths outputPaths,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(outputPaths);

        var reportBytes = TrainingDataInventoryJson.SerializeToUtf8Bytes(report);
        var reportHash = Convert.ToHexStringLower(SHA256.HashData(reportBytes));
        var hashBytes = Encoding.UTF8.GetBytes(
            $"{reportHash}  {Path.GetFileName(outputPaths.ReportPath)}{Environment.NewLine}");

        await AtomicTextFileWriter
            .WriteAllBytesAsync(outputPaths.ReportPath, reportBytes, cancellationToken)
            .ConfigureAwait(false);
        await AtomicTextFileWriter
            .WriteAllBytesAsync(outputPaths.Sha256Path, hashBytes, cancellationToken)
            .ConfigureAwait(false);

        return new TrainingInventoryReportFiles(
            outputPaths.ReportPath,
            outputPaths.Sha256Path,
            reportHash);
    }
}

internal sealed record TrainingInventoryReportFiles(
    string ReportPath,
    string Sha256Path,
    string Sha256);
