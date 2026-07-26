using System.IO;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Verdrahtungsgarantie (Projektmuster Quelltext-Test): die Statusleiste der Videoanalyse
/// zeigt den Qualifikations-Hinweis des Altmodells an, sobald das Ergebnis die
/// Kennzeichnung traegt. Der Text selbst ist verbindlich (Nutzer-Vorgabe Phase 1).
/// </summary>
public sealed class VideoAnalysisQualificationWarningArchitectureTests
{
    [Fact]
    public void Statusleiste_zeigt_Altmodell_Hinweis_bei_unqualifiziertem_Detektor()
    {
        var source = ReadProjectFile(
            "src/AuswertungPro.Next.Infrastructure/Ai/VideoAnalysisPipelineService.cs");

        Assert.Contains("videoResult.DetectorQualified == false", source);
        Assert.Contains(
            "WARNUNG: YOLO nicht freigegeben – DINO/SAM laufen weiter; Ergebnis manuell pruefen.",
            source);
        Assert.Contains("Manuelle Pruefung erforderlich:", source);
        Assert.Contains("videoResult.Degraded", source);
    }

    private static string ReadProjectFile(string relativePath)
    {
        for (var current = new DirectoryInfo(System.AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(current.FullName, "sidecar")))
            {
                return File.ReadAllText(
                    Path.Combine(current.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
        }
        throw new InvalidDataException("SewerStudio-Projektwurzel wurde nicht gefunden.");
    }
}
