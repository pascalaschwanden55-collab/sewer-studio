using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.Application.UseCases.TrainingStudioMultiObject;

/// <summary>
/// Erzeugt den Arbeitskontext fuer ein weiteres, vom Menschen erkanntes Objekt
/// auf demselben Bild. Eine PDF- oder Bestandsidentitaet wird bewusst nicht
/// weitergegeben: Das neue Objekt erhaelt beim Speichern eine eigene Sample-ID
/// und gilt ohne eigene Operateurreferenz als ManualCoding.
/// </summary>
public static class TrainingStudioAdditionalObjectPolicy
{
    public static WorkbenchItem CreateManualObject(WorkbenchItem source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new WorkbenchItem(
            FramePath: source.FramePath,
            CaseId: source.CaseId,
            MeterStart: source.MeterStart,
            // Ein zusaetzlich sichtbares Fotoobjekt darf nicht still den Bereich
            // eines anderen Streckenschadens erben.
            MeterEnd: source.MeterStart,
            HaltungName: source.HaltungName,
            VideoPath: source.VideoPath,
            PipeDiameterMm: source.PipeDiameterMm,
            ExistingSampleId: null,
            ExistingCode: null,
            ExistingBeschreibung: null,
            SuggestedMainCode: source.SuggestedMainCode,
            IsStreckenschaden: false,
            SourceSuggestion: null)
        {
            InspectionDate = source.InspectionDate,
            // Bindet einen bereits geschuetzten Bildstand weiter an dieselben
            // Bytes, ohne den Bestaetigungsstand eines alten Samples zu kopieren.
            ExpectedImageSha256 = source.ExpectedImageSha256,
        };
    }
}
