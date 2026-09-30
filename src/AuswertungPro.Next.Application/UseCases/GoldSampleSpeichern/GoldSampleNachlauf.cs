using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.UseCases.GoldSampleSpeichern;

/// <summary>
/// Phasen "KB-Nachtrag" und "Teacher-Nachlauf" fuer ein bereits dauerhaft gespeichertes
/// Goldsample. Beide Schritte fangen jede Ausnahme (bewusst auch einen Abbruch) und geben
/// sie als sichtbare Warnung zurueck: das gespeicherte Sample wird nie zurueckgenommen.
/// </summary>
internal sealed class GoldSampleNachlauf
{
    private readonly ITrainingSampleStore _sampleStore;
    private readonly IKnowledgeBaseIndexer _kbIndexer;
    private readonly ITeacherAnnotationStore _teacherStore;
    private readonly IVsaYoloClassMapStore _teacherClassMap;
    private readonly Func<ITrainingAnnotationExportService> _createExportService;

    public GoldSampleNachlauf(
        ITrainingSampleStore sampleStore,
        IKnowledgeBaseIndexer kbIndexer,
        ITeacherAnnotationStore teacherStore,
        IVsaYoloClassMapStore teacherClassMap,
        Func<ITrainingAnnotationExportService> createExportService)
    {
        _sampleStore = sampleStore;
        _kbIndexer = kbIndexer;
        _teacherStore = teacherStore;
        _teacherClassMap = teacherClassMap;
        _createExportService = createExportService;
    }

    /// <summary>
    /// KB-Index fuer das gespeicherte Sample nachtragen: indexieren, den resultierenden
    /// KbIndexState setzen und ueber MergeOrUpdateAsync nachtragen (Skipped/Error werden
    /// nicht wiederholt). Ein Fehler HIER (Index ODER Nachtrag, SQLite-Lock, DB-Fehler) darf
    /// den Save NICHT als "Nicht gespeichert" darstellen — sonst legt der Nutzer dasselbe
    /// Sample erneut an. Der bare "catch (Exception ex)" (bewusst OHNE Abbruch-Ausnahme
    /// auszuschliessen) ist Ist-Verhalten.
    /// </summary>
    public async Task<(string KbState, string? KbWarning)> RecordKbIndexAsync(
        DauerhaftGespeichertesGoldSample gespeichert, CancellationToken ct)
    {
        var sample = gespeichert.Sample;
        try
        {
            var outcome = await _kbIndexer.IndexAsync(new[] { sample }, ct).ConfigureAwait(false);
            sample.KbIndexState = outcome.IsIndexed(sample.SampleId) ? KbIndexState.Indexed
                : outcome.IsSkipped(sample.SampleId) ? KbIndexState.Skipped
                : KbIndexState.Error;
            await _sampleStore.MergeOrUpdateAsync(new List<TrainingSample> { sample }).ConfigureAwait(false);
            return (sample.KbIndexState.ToString(), null);
        }
        catch (Exception ex)
        {
            return (KbIndexState.Error.ToString(), $"KB-Index nicht aktualisiert: {UserError.DescribeAndReport(ex, "KB-Index aktualisieren")}");
        }
    }

    /// <summary>
    /// Teacher-Kandidat fuer das gespeicherte Sample bauen und exportieren. Ein Fehler hier
    /// darf das gespeicherte Sample nicht ruecknehmen. Auch eine OperationCanceledException
    /// wird wie bisher als sichtbare Warnung behandelt.
    /// </summary>
    public async Task<(string? TeacherId, string? TeacherWarning)> RecordTeacherCandidateAsync(
        DauerhaftGespeichertesGoldSample gespeichert,
        WorkbenchItem item,
        BoundingBox box,
        WorkbenchDecision decision,
        string finalCode,
        string beschreibung,
        CancellationToken ct)
    {
        try
        {
            var classId = _teacherClassMap.GetOrAddClassId(finalCode);
            var bbox = new NormalizedBoundingBox
            {
                XCenter = box.XCenter,
                YCenter = box.YCenter,
                Width = box.Width,
                Height = box.Height,
            };
            var annotation = new TeacherAnnotation
            {
                VsaCode = finalCode,
                Beschreibung = beschreibung,
                Severity = decision.Severity,
                MeterPosition = item.MeterStart,
                BoundingBox = bbox,
                ClockPosition = decision.ClockPosition,
                HaltungName = item.HaltungName,          // <-- schliesst die QuarantineOrigin-Luecke
                VideoPath = item.VideoPath,
                SourceSampleId = gespeichert.SampleId,   // <-- Fremdschluessel fuer die Codekorrektur-Bereinigung
            };

            var exportService = _createExportService();
            var export = await exportService
                .ExportAsync(gespeichert.StoredFramePath, bbox, finalCode, classId, $"wb_{annotation.AnnotationId}", ct)
                .ConfigureAwait(false);
            if (!export.Success)
                throw new UserFacingException(TeacherExportGrund(export));

            annotation.FullFramePath = export.FullFramePath;
            annotation.CroppedRegionPath = export.CroppedRegionPath;
            annotation.YoloAnnotationPath = export.YoloAnnotationPath;
            await _teacherStore.AppendAsync(annotation).ConfigureAwait(false);
            return (annotation.AnnotationId, null);
        }
        catch (Exception ex)
        {
            // Sample bleibt gespeichert; die Warnung wird sichtbar zurueckgegeben (nie still).
            return (null, $"Teacher-Kandidat nicht gespeichert: {UserError.DescribeAndReport(ex, "Teacher-Kandidat speichern")}");
        }
    }

    /// <summary>
    /// Deutscher Grund fuer einen gescheiterten Teacher-Export (Aufgabe 10c2, Fix-Runde 2).
    /// <see cref="TrainingAnnotationResult.Error"/> kann je nach Exporteur ein roher
    /// Framework-Text sein ("The process cannot access the file …") und wird deshalb nie
    /// angezeigt, sondern nur protokolliert. Angezeigt wird die Einordnung der Ausnahme
    /// ueber <see cref="UserError"/> oder ein fester deutscher Satz.
    /// </summary>
    internal static string TeacherExportGrund(TrainingAnnotationResult export)
    {
        if (export.Failure is { } fehler)
            return UserError.Describe(fehler);

        BestEffort.ReportWarning(
            "[AnnotationWorkbenchService.TeacherExport] Teacher-Export ohne Erfolg: "
            + (string.IsNullOrWhiteSpace(export.Error) ? "(kein Grund gemeldet)" : export.Error));
        return "Der Teacher-Export ist fehlgeschlagen. Technische Details stehen im Programmlog.";
    }
}
