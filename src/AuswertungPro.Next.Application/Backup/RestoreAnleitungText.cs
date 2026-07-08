using System;
using System.Linq;
using System.Text;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Generiert die deutsche RESTORE-ANLEITUNG.txt, die in jede Datensicherung gelegt wird.
/// Reine Textgenerierung — die Original-Quellpfade kommen aus den aufgeloesten Sources,
/// damit die Anleitung die ECHTEN Pfade dieses PCs nennt.
/// </summary>
public static class RestoreAnleitungText
{
    public static string Build(FullBackupSources sources)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SEWERSTUDIO — WIEDERHERSTELLUNG NACH PC-AUSFALL");
        sb.AppendLine("================================================");
        sb.AppendLine();
        sb.AppendLine("Diese Sicherung enthaelt alles Unersetzliche: Programm (Quellcode inkl.");
        sb.AppendLine("Git-Verlauf), KI-Gehirn, Einstellungen und Logdateien.");
        sb.AppendLine("NICHT enthalten: Projekte/Videos (separat gesichert), Ollama-Modelle");
        sb.AppendLine("(neu ladbar, Liste in umgebung.txt), Werkzeuge wie ffmpeg/Playwright/");
        sb.AppendLine("Tesseract (neu installierbar), TensorRT-Engines (werden neu gebaut).");
        sb.AppendLine();
        sb.AppendLine($"Ordner \"{BackupVersionRetention.VersionsFolderName}\": aeltere Staende ersetzter oder entfallener");
        sb.AppendLine("Dateien (Schutz vor versehentlichem Loeschen, die letzten");
        sb.AppendLine($"{BackupVersionRetention.MaxStaende} Sicherungslaeufe). Fuer die normale Wiederherstellung ignorieren —");
        sb.AppendLine("nur hineinschauen, wenn eine aeltere Dateiversion gebraucht wird.");
        sb.AppendLine();
        sb.AppendLine("SCHRITT 1 — Programm zuruecklegen");
        sb.AppendLine($"  Ordner \"Programm\" nach {sources.RepoRoot ?? "C:\\Sewer-Studio_KI_4.4"} kopieren.");
        sb.AppendLine("  Danach benoetigt: Visual Studio (oder .NET 10 SDK) und Python fuer den Sidecar.");
        sb.AppendLine("  Sidecar-Umgebung neu aufsetzen (venv + pip install, siehe sidecar\\README/Skripte).");
        sb.AppendLine();
        sb.AppendLine("SCHRITT 2 — KI-Gehirn zuruecklegen");
        sb.AppendLine($"  Ordner \"KI_BRAIN\" nach {sources.KnowledgeRoot} kopieren.");
        sb.AppendLine("  WICHTIG: Umgebungsvariable SEWERSTUDIO_KNOWLEDGE_ROOT auf diesen Pfad setzen");
        sb.AppendLine("  (Werte aller Variablen stehen in Extras\\umgebung.txt).");
        sb.AppendLine("  Ausgelassen wurden nur regenerierbare Trainings-Datensaetze (yolo_*_dataset*,");
        sb.AppendLine("  training_frames, kb_backups) — sie lassen sich aus der Wissensdatenbank neu bauen.");
        sb.AppendLine();
        sb.AppendLine("SCHRITT 3 — Einstellungen zuruecklegen");
        sb.AppendLine($"  \"Einstellungen\\Local_SewerStudio\"    nach {sources.LocalSewerStudioDir}");
        sb.AppendLine($"  \"Einstellungen\\Roaming_SewerStudio\"  nach {sources.RoamingSewerStudioDir}");
        sb.AppendLine($"  \"Einstellungen\\Roaming_AuswertungPro\" nach {sources.RoamingAuswertungProDir}");
        sb.AppendLine();
        sb.AppendLine("SCHRITT 4 — Logs (nur bei Bedarf fuer Diagnose)");
        sb.AppendLine($"  \"Logs\\logs\" und \"Logs\\Telemetry\" nach {sources.LocalSewerStudioDir}");
        sb.AppendLine();
        sb.AppendLine("SCHRITT 5 — Umgebung herstellen");
        sb.AppendLine("  a) Umgebungsvariablen laut Extras\\umgebung.txt setzen (System-Ebene).");
        sb.AppendLine("  b) Desktop-Skripte aus Extras\\ zurueck auf den Desktop legen.");
        sb.AppendLine("  c) Ollama installieren und Modelle laut Liste in umgebung.txt laden");
        sb.AppendLine("     (z. B. \"ollama pull qwen3-vl:8b-q8\").");
        sb.AppendLine("  d) Projekt bauen: dotnet build AuswertungPro.sln");
        sb.AppendLine();
        sb.AppendLine("HINWEIS QGIS: Eigene QGIS-Plugins (awu_schadensimport, awu_wincan_export)");
        sb.AppendLine("liegen im QGIS-Profil und sind NICHT Teil dieser Sicherung — separat sichern.");
        sb.AppendLine();
        sb.AppendLine("Urspruengliche Umgebungsvariablen dieses PCs:");
        if (sources.EnvironmentVariables.Count == 0)
        {
            sb.AppendLine("  (keine SEWERSTUDIO_*/SEWER_*-Variablen gesetzt)");
        }
        else
        {
            foreach (var kv in sources.EnvironmentVariables.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"  {kv.Key} = {kv.Value}");
        }

        return sb.ToString();
    }
}
