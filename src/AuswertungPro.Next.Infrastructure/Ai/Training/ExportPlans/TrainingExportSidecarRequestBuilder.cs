using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Verpackt den fertigen Plan fuer den Sidecar. Es werden keine Klassen-,
/// Filter- oder Splitentscheidungen getroffen.
/// </summary>
public sealed class TrainingExportSidecarRequestBuilder : ITrainingExportSidecarRequestBuilder
{
    public const int MaximumImagesPerRequest = 500;
    private const int MaximumImageBytes = 25 * 1024 * 1024;

    public async Task<TrainingExportPlanRequestDto> BuildAsync(
        TrainingExportPlanBundle bundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        TrainingExportPlanValidator.Validate(bundle.Plan);
        if (bundle.Plan.Images.Count == 0)
            throw new TrainingExportPlanException("Der Exportplan enthaelt keine auszugebenden Bilder.");
        if (bundle.Plan.Images.Count > MaximumImagesPerRequest)
        {
            throw new TrainingExportPlanException(
                $"Der Sidecar verarbeitet hoechstens {MaximumImagesPerRequest} Planbilder pro Request.");
        }

        var samples = new List<TrainingExportPlanSampleDto>(bundle.Plan.Images.Count);
        foreach (var image in bundle.Plan.Images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!bundle.SourcePathsByImageSha256.TryGetValue(image.ImageSha256, out var sourcePath))
                throw new TrainingExportPlanException($"Originalpfad fuer Bild {image.ImageSha256} fehlt.");
            var bytes = await ReadStableVerifiedImageAsync(
                    sourcePath,
                    image.ImageSha256,
                    cancellationToken)
                .ConfigureAwait(false);
            samples.Add(new TrainingExportPlanSampleDto(
                image.ImageSha256,
                Convert.ToBase64String(bytes),
                image.Target == TrainingExportTarget.Train ? "train" : "val",
                image.TargetFileName,
                image.Labels.Select(label => new TrainingExportPlanLabelDto(
                    label.ClassId,
                    label.BoundingBox.XCenter,
                    label.BoundingBox.YCenter,
                    label.BoundingBox.Width,
                    label.BoundingBox.Height)).ToArray()));
        }

        var manifest = TrainingExportPlanSerializer.SerializeManifest(bundle.Plan);
        var manifestHash = Convert.ToHexStringLower(SHA256.HashData(manifest));
        return new TrainingExportPlanRequestDto(
            TrainingExportPlan.CurrentSchemaVersion,
            bundle.Plan.PlanId,
            bundle.Plan.PlanId,
            bundle.Plan.ClassMapVersion,
            bundle.Plan.VsaManifestHash,
            bundle.Plan.RegistryHash,
            bundle.Plan.Classes,
            Convert.ToBase64String(manifest),
            manifestHash,
            samples);
    }

    internal static async Task<byte[]> ReadStableVerifiedImageAsync(
        string path,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var reparsePoint = TrainingInventoryPaths.FindReparsePoint(path);
            if (reparsePoint is not null)
                throw new TrainingExportPlanException($"Bildpfad enthaelt eine Verknuepfung: {reparsePoint}");
            var before = new FileInfo(path);
            before.Refresh();
            if (!before.Exists)
                throw new TrainingExportPlanException($"Geplantes Originalbild fehlt: {path}");
            if (before.Length > MaximumImageBytes)
                throw new TrainingExportPlanException($"Geplantes Originalbild ist groesser als 25 MiB: {path}");

            byte[] bytes;
            await using (var stream = new FileStream(
                             path,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             bufferSize: 128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                using var buffer = new MemoryStream(checked((int)stream.Length));
                await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                bytes = buffer.ToArray();
            }
            var after = new FileInfo(path);
            after.Refresh();
            if (!after.Exists
                || before.Length != after.Length
                || before.LastWriteTimeUtc != after.LastWriteTimeUtc)
            {
                continue;
            }

            var actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!actualHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingExportPlanException(
                    $"Originalbild wurde nach dem Live-Inventar veraendert: {path}");
            }
            return bytes;
        }

        throw new TrainingExportPlanException($"Originalbild wurde waehrend des Lesens veraendert: {path}");
    }
}
