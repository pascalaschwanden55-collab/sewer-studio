using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

internal static class TrainingInventoryFileAccess
{
    public static FileStream OpenReadShared(string path)
        => new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

    public static async Task<TrainingInventoryHashResult> ComputeHashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = OpenReadShared(path);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return new TrainingInventoryHashResult(
                TrainingInventoryHashState.Computed,
                Convert.ToHexStringLower(hash),
                null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new TrainingInventoryHashResult(
                TrainingInventoryHashState.ReadError,
                null,
                ex.Message);
        }
    }
}

internal readonly record struct TrainingInventoryHashResult(
    TrainingInventoryHashState State,
    string? Sha256,
    string? Error)
{
    public static TrainingInventoryHashResult NotRequested { get; } = new(
        TrainingInventoryHashState.NotRequested,
        null,
        null);

    public static TrainingInventoryHashResult NotApplicable { get; } = new(
        TrainingInventoryHashState.NotApplicable,
        null,
        null);
}
