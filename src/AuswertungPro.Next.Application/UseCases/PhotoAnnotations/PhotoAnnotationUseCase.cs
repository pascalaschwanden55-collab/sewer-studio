using System.Globalization;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.UseCases.PhotoAnnotations;

/// <summary>
/// Echter fachlicher Kontext eines Fotos aus dem Codiermodus.
/// Ohne Haltungskontext wird kein persoenliches Goldsample angelegt.
/// </summary>
public sealed record PhotoAnnotationSessionContext(
    string CaseId,
    string? HaltungName,
    int? PipeDiameterMm);

/// <summary>
/// Kontext, der beim Oeffnen des Foto-Assistenten bereits bekannt ist.
/// Meter und Beschreibung werden erst aus dem final bestaetigten Eintrag uebernommen.
/// </summary>
public sealed record PhotoAnnotationCaptureContext(
    PhotoAnnotationSessionContext Session,
    string CodeHint,
    string? VideoPath);

public sealed record PhotoAnnotationSegmentRequest(
    string OriginalPhotoPath,
    PhotoAnnotationCaptureContext Context,
    OverlayGeometry Geometry);

/// <summary>
/// Unveraenderliches Paket aus Originalbild, Bild-Hash, Box und SAM-Maske.
/// Dieses Paket wird erst nach der finalen Code-Bestaetigung gespeichert.
/// </summary>
public sealed record PhotoAnnotationDraft(
    WorkbenchItem Item,
    string OriginalPhotoSha256,
    BoundingBox Box,
    WorkbenchSegmentation Segmentation,
    OverlaySamMask SamMask,
    string CodeHint,
    WorkbenchImageSnapshot? OriginalPhotoSnapshot = null);

public sealed record PhotoAnnotationSegmentResult(
    bool Success,
    string Message,
    PhotoAnnotationDraft? Draft);

public sealed record PhotoAnnotationSaveRequest(
    PhotoAnnotationDraft Draft,
    ProtocolEntry FinalEntry,
    string ConfirmedByUser);

public sealed record PhotoAnnotationSaveResult(
    bool SampleSaved,
    bool KnowledgeBaseIndexed,
    string Message,
    string? Warning,
    string? SampleId = null);

public interface IPhotoAnnotationUseCase
{
    Task<PhotoAnnotationSegmentResult> SegmentAsync(
        PhotoAnnotationSegmentRequest request,
        CancellationToken cancellationToken = default);

    Task<PhotoAnnotationSaveResult> SaveAsync(
        PhotoAnnotationSaveRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Verbindet den Foto-Assistenten mit dem geschuetzten Gold-/KB-Pruefplatz.
/// Der Workbench-Service bleibt der einzige Besitzer der Persistenz.
/// </summary>
public sealed class PhotoAnnotationUseCase : IPhotoAnnotationUseCase
{
    private readonly IAnnotationWorkbenchService _workbench;
    private readonly Func<string, byte[]> _readFileBytes;

    public PhotoAnnotationUseCase(
        IAnnotationWorkbenchService workbench,
        Func<string, byte[]>? readFileBytes = null)
    {
        ArgumentNullException.ThrowIfNull(workbench);
        _workbench = workbench;
        _readFileBytes = readFileBytes ?? File.ReadAllBytes;
    }

    public async Task<PhotoAnnotationSegmentResult> SegmentAsync(
        PhotoAnnotationSegmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);
        ArgumentNullException.ThrowIfNull(request.Context.Session);
        ArgumentNullException.ThrowIfNull(request.Geometry);

        var caseId = request.Context.Session.CaseId?.Trim() ?? string.Empty;
        if (caseId.Length == 0)
            return FailedSegment("Haltungskennung fehlt. Das Bild wird nicht als KI-Beispiel freigegeben.");

        if (!TryCreateBox(request.Geometry, out var box))
            return FailedSegment("Die Markierung liegt nicht vollstaendig im Foto. Bitte die Box neu ziehen.");

        byte[] bytesBefore;
        string hashBefore;
        try
        {
            bytesBefore = _readFileBytes(request.OriginalPhotoPath);
            hashBefore = ComputeSha256(bytesBefore);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FailedSegment($"Originalfoto kann nicht gelesen werden: {ex.Message}");
        }

        var codeHint = NormalizeCode(request.Context.CodeHint);
        var item = new WorkbenchItem(
            FramePath: request.OriginalPhotoPath,
            CaseId: caseId,
            MeterStart: 0,
            MeterEnd: 0,
            HaltungName: NullIfWhiteSpace(request.Context.Session.HaltungName),
            VideoPath: NullIfWhiteSpace(request.Context.VideoPath),
            PipeDiameterMm: request.Context.Session.PipeDiameterMm);

        WorkbenchSegmentation segmentation;
        try
        {
            segmentation = await _workbench
                .SegmentAsync(item, box, codeHint, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FailedSegment($"SAM-Segmentierung fehlgeschlagen: {ex.Message}");
        }

        if (segmentation.Degraded)
            return FailedSegment("SAM hat nur eine Teil-Segmentierung geliefert. Bitte die Box neu ziehen.");

        if (!SamMaskFormatValidator.IsValid(
                segmentation.MaskRle,
                segmentation.MaskImageWidth,
                segmentation.MaskImageHeight,
                out var maskReason))
        {
            return FailedSegment($"SAM-Maske ist nicht verwendbar: {maskReason}");
        }

        if (!SamMaskFormatValidator.HasForegroundPixelInsideBox(
                segmentation.MaskRle,
                segmentation.MaskImageWidth,
                segmentation.MaskImageHeight,
                box,
                out var boxReason))
        {
            return FailedSegment($"SAM-Maske passt nicht zur Markierung: {boxReason}");
        }

        byte[] bytesAfter;
        try
        {
            bytesAfter = _readFileBytes(request.OriginalPhotoPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FailedSegment($"Originalfoto kann nach der Segmentierung nicht gelesen werden: {ex.Message}");
        }

        WorkbenchImageSnapshot snapshot;
        try
        {
            snapshot = WorkbenchImageSnapshot.Create(
                bytesAfter,
                Path.GetExtension(request.OriginalPhotoPath));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FailedSegment($"Originalfoto kann nicht gebunden werden: {ex.Message}");
        }

        if (!string.Equals(hashBefore, snapshot.Sha256, StringComparison.Ordinal))
            return FailedSegment("Das Originalfoto wurde waehrend der Segmentierung veraendert. Bitte neu markieren.");

        var samMask = new OverlaySamMask
        {
            MaskRle = segmentation.MaskRle!,
            ImageWidth = segmentation.MaskImageWidth,
            ImageHeight = segmentation.MaskImageHeight,
            MaskAreaPixels = CountForegroundPixels(segmentation.MaskRle!),
            Confidence = 0,
            Label = codeHint
        };
        request.Geometry.SamMask = samMask;

        return new PhotoAnnotationSegmentResult(
            true,
            string.IsNullOrWhiteSpace(segmentation.StatusText)
                ? "SAM-Maske erstellt."
                : segmentation.StatusText,
            new PhotoAnnotationDraft(
                item,
                snapshot.Sha256,
                box,
                segmentation,
                samMask,
                codeHint,
                snapshot));
    }

    public async Task<PhotoAnnotationSaveResult> SaveAsync(
        PhotoAnnotationSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Draft);
        ArgumentNullException.ThrowIfNull(request.FinalEntry);

        var user = request.ConfirmedByUser?.Trim() ?? string.Empty;
        if (user.Length == 0)
            return FailedSave("Name der pruefenden Person fehlt.");

        WorkbenchImageSnapshot snapshot;
        try
        {
            // Neue Entwuerfe tragen die beim Segmentieren unveraenderlich gebundenen
            // Bytes. Der Einmal-Lesefallback erhaelt alte, bereits erzeugte Entwuerfe.
            snapshot = request.Draft.OriginalPhotoSnapshot
                ?? WorkbenchImageSnapshot.Create(
                    _readFileBytes(request.Draft.Item.FramePath),
                    Path.GetExtension(request.Draft.Item.FramePath));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FailedSave($"Originalfoto kann nicht mehr gelesen werden: {ex.Message}");
        }

        if (!string.Equals(
                snapshot.Sha256,
                request.Draft.OriginalPhotoSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return FailedSave("Die gebundenen Originalbildbytes stimmen nicht mit ihrem SHA-256 ueberein. Bitte neu markieren.");
        }

        var finalCode = NormalizeCode(request.FinalEntry.Code);
        if (finalCode.Length == 0)
            return FailedSave("Der bestaetigte VSA-Code fehlt.");

        var meterStart = request.FinalEntry.MeterStart ?? 0;
        var item = request.Draft.Item with
        {
            MeterStart = meterStart,
            MeterEnd = request.FinalEntry.MeterEnd ?? meterStart,
            IsStreckenschaden = request.FinalEntry.IsStreckenschaden
        };
        var decision = new WorkbenchDecision(
            VsaCode: finalCode,
            // Dies ist eine reine Handcodierung, keine Korrektur eines KI-Vorschlags.
            WasCorrected: false,
            Beschreibung: BuildGoldDescription(request.FinalEntry.Beschreibung),
            ClockPosition: ResolveClockPosition(request.FinalEntry),
            Severity: ResolveSeverity(request.FinalEntry),
            ConfirmedByUser: user);

        WorkbenchSaveResult saved;
        try
        {
            saved = await _workbench
                .SaveAsync(
                    item,
                    request.Draft.Box,
                    request.Draft.Segmentation,
                    decision,
                    snapshot,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FailedSave($"KI-Beispiel konnte nicht gespeichert werden: {ex.Message}");
        }

        if (!saved.Saved)
            return FailedSave(saved.RefusalReason ?? "KI-Beispiel wurde nicht gespeichert.");

        if (!saved.GoldApproved)
        {
            return new PhotoAnnotationSaveResult(
                SampleSaved: false,
                KnowledgeBaseIndexed: false,
                Message: CombineMessages(
                    "Das Bild wurde als Entwurf gespeichert, ist aber noch kein Goldsample.",
                    saved.RefusalReason),
                Warning: null,
                SampleId: saved.SampleId);
        }

        var indexed = string.Equals(
            saved.KbIndexState,
            "Indexed",
            StringComparison.OrdinalIgnoreCase);
        var warning = saved.RefusalReason;
        if (!indexed)
        {
            warning = CombineMessages(
                $"Goldsample wurde gespeichert, aber der KB-Index meldet '{saved.KbIndexState}'.",
                warning);
        }

        return new PhotoAnnotationSaveResult(
            SampleSaved: true,
            KnowledgeBaseIndexed: indexed,
            Message: indexed
                ? "Originalbild, Box, SAM-Maske und Code wurden in der Wissensdatenbank gespeichert."
                : "Das Goldsample wurde gespeichert.",
            Warning: warning,
            SampleId: saved.SampleId);
    }

    private static bool TryCreateBox(OverlayGeometry geometry, out BoundingBox box)
    {
        box = default;
        if (geometry.ToolType != OverlayToolType.Rectangle
            || geometry.Points.Count < 2
            || geometry.Points.Any(point =>
                !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
        {
            return false;
        }

        var minX = geometry.Points.Min(point => point.X);
        var maxX = geometry.Points.Max(point => point.X);
        var minY = geometry.Points.Min(point => point.Y);
        var maxY = geometry.Points.Max(point => point.Y);
        return BoundingBox.TryCreate(
            (minX + maxX) / 2.0,
            (minY + maxY) / 2.0,
            maxX - minX,
            maxY - minY,
            out box);
    }

    private static string BuildGoldDescription(string? description)
    {
        var clearText = description?.Trim() ?? string.Empty;
        if (clearText.Length == 0)
            clearText = "Manuell codierte Beobachtung";

        return $"{clearText} (im Originalfoto manuell markiert)";
    }

    private static double? ResolveClockPosition(ProtocolEntry entry)
    {
        if (entry.CodeMeta?.Parameters.TryGetValue("vsa.uhr.von", out var raw) != true)
            return null;

        return double.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static int? ResolveSeverity(ProtocolEntry entry)
        => int.TryParse(
            entry.CodeMeta?.Severity,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var severity)
            ? severity
            : null;

    private static int CountForegroundPixels(string rle)
    {
        var parts = rle.Split(',');
        var foreground = parts[0] == "1";
        long total = 0;
        for (var index = 1; index < parts.Length; index++)
        {
            var run = int.Parse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (foreground)
                total += run;
            foreground = !foreground;
        }

        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    private static string ComputeSha256(byte[] bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string NormalizeCode(string? code)
        => code?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CombineMessages(string first, string? second)
        => string.IsNullOrWhiteSpace(second)
            ? first
            : $"{first} {second.Trim()}";

    private static PhotoAnnotationSegmentResult FailedSegment(string message)
        => new(false, message, null);

    private static PhotoAnnotationSaveResult FailedSave(string message)
        => new(false, false, message, null);
}
