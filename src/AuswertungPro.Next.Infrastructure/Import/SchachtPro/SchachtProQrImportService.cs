using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

public sealed class SchachtProQrImportService(IQrImageReader images) : ISchachtProQrImportService
{
    public Result<ImportStats> ImportImage(string path, Project project, ImportRunContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var ct = context?.CancellationToken ?? CancellationToken.None;
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!ImportSourcePathGuard.TryInspectFile(path, out var safePath, out var exists, out var error) || !exists)
                throw new InvalidDataException(error ?? "QR-Bild nicht gefunden.");
            // QR-Inhalt und spaeter kopiertes Original muessen aus derselben Dateiversion stammen.
            using var sourceLock = new FileStream(safePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var texts = images.Read(safePath, ct).Where(t => t.StartsWith("SPQR", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).ToArray();
            if (texts.Length != 1)
                throw new InvalidDataException(texts.Length == 0
                    ? "Kein lesbarer SchachtPro-QR-Code im Bild. Bitte einen groesseren Screenshot verwenden."
                    : "Mehrere SchachtPro-QR-Codes im Bild. Bitte je Schacht ein einzelnes Bild verwenden.");
            var payload = SchachtProQrPayload.Parse(texts[0], ct);
            SchachtProProtocolImport.EnsureUniqueTarget(project, payload.Protocol.SchachtNr!);
            var mapped = SchachtProProtocolMapper.Map(payload.Protocol, false);
            ct.ThrowIfCancellationRequested();
            var messages = new List<string> { "SchachtPro-QR: Pruefsumme und Datenformat geprueft. Fotos und Logos sind nicht enthalten." };
            // Vor der Datensatzuebernahme kopieren: ein Ablagefehler darf keinen neuen Schacht hinterlassen.
            var imagePath = context?.FileStaging is { } staging
                ? SchachtProQrAblage.Prepare(safePath, payload.Protocol, staging, ct)
                : null;
            int found = 0, created = 0, updated = 0, uncertain = 0;
            SchachtProProtocolImport.Apply(payload.Protocol, false, path, project, context,
                messages, ref found, ref created, ref updated, ref uncertain, mapped);
            // Vollstaendiger Quellbeleg: auch Angaben ohne derzeitiges Anzeigefeld bleiben erhalten.
            project.Metadata["SchachtPro.QR." + payload.SourceKey] = payload.Json;
            if (imagePath is not null)
            {
                project.Metadata["SchachtPro.QR.Bild." + payload.SourceKey] = imagePath;
                messages.Add($"QR-Bild dem Schacht zugeordnet: {imagePath}");
            }
            else
                messages.Add("QR-Bild noch nicht im Schachtordner abgelegt: Projekt zuerst speichern und den Import erneut ausfuehren.");
            project.Dirty = true;
            project.ModifiedAtUtc = DateTime.UtcNow;
            messages.Add("Vollstaendiger QR-Quellbeleg in den Projektmetadaten gespeichert; verwendet wird die vorhandene SchachtPro-Feldzuordnung.");
            context?.Log.AddEntry("SchachtPro-QR", "Import", ImportLogStatus.Info,
                sourceFile: path, recordKey: payload.Protocol.SchachtNr);
            return Result<ImportStats>.Success(new(found, created, updated, 0, uncertain, messages));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException
            or FormatException or InvalidOperationException or ArgumentException or NotSupportedException
            or System.Runtime.InteropServices.ExternalException)
        {
            context?.Log.AddEntry("SchachtPro-QR", "Lesen", ImportLogStatus.Error, sourceFile: path, detail: ex.Message);
            return Result<ImportStats>.Fail("SPQR_INVALID", $"QR-Import fehlgeschlagen: {ex.Message}");
        }
    }
}
