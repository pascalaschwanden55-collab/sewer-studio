using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SettingsPage : System.Windows.Controls.UserControl
{
    private string? _previewedSourceDir;

    public SettingsPage()
    {
        InitializeComponent();
        UpdateMarktdatenStatus();
        UpdateSanierungRulesStatus();
    }

    private void UpdateSanierungRulesStatus()
    {
        try
        {
            // Phase 5.1.B Etappe 3.C: via DI-Container statt Cast
            var file = App.Resolve<Infrastructure.Sanierung.SanierungUserRulesService>().Load();
            var active = file.Rules.Count(r => r.Enabled);
            SanierungRulesStatusText.Text =
                $"{file.Rules.Count} Regeln gesamt ({active} aktiv)  -  zuletzt: {file.LastUpdated:dd.MM.yyyy HH:mm}";
        }
        catch (Exception ex)
        {
            SanierungRulesStatusText.Text = $"Status nicht ermittelbar: {ex.Message}";
        }
    }

    private void OpenSanierungRules_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Phase 5.1.B Etappe 3.C: via DI
            var rules = App.Resolve<Infrastructure.Sanierung.SanierungUserRulesService>();
            var engine = App.Resolve<Infrastructure.Sanierung.RehabilitationRulesEngine>();
            var win = new Views.Windows.SanierungRulesWindow(rules, engine)
            {
                Owner = Window.GetWindow(this),
            };
            win.ShowDialog();
            UpdateSanierungRulesStatus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Konnte Regelverwaltung nicht oeffnen:\n{ex.Message}",
                "Sanierungsregeln", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateMarktdatenStatus()
    {
        try
        {
            // Phase 5.1.B Etappe 3.C: via DI
            var submissions = App.Resolve<Infrastructure.Devis.SubmissionsPositionService>();
            var historische = App.Resolve<Infrastructure.Devis.HistorischeSanierungenService>();
            var subPath = submissions.FilePath;
            var histPath = historische.FilePath;
            var hist = historische.LoadData();
            var anzahlHaltungen = hist.Haltungen.Count;
            var anzahlProfile = hist.ProfileAggregat.Count;

            var sb = new StringBuilder();
            sb.AppendLine($"Aktiv: submission_positionen.json  ({(File.Exists(subPath) ? "✓" : "fehlt")})");
            sb.AppendLine($"       historische_sanierungen.json ({(File.Exists(histPath) ? "✓" : "fehlt")}, "
                + $"{anzahlHaltungen} Haltungen, {anzahlProfile} Profile)");
            if (hist.Meta is { } m)
                sb.Append($"Stand: {m.Stand} - {m.Quelle}");
            MarktdatenStatusText.Text = sb.ToString();
        }
        catch (Exception ex)
        {
            MarktdatenStatusText.Text = $"Status nicht ermittelbar: {ex.Message}";
        }
    }

    private void MarktdatenPreview_Click(object sender, RoutedEventArgs e)
    {
        // Datei-Dialog: User waehlt eine der erwarteten JSONs - der Quellordner ist dann das Parent-Dir
        var dlg = new OpenFileDialog
        {
            Title = "submission_positionen.json oder historische_sanierungen.json auswaehlen",
            Filter = "JSON-Marktdaten|submission_positionen.json;historische_sanierungen.json|JSON|*.json",
            CheckFileExists = true,
        };
        // Default: Knowledge\sanierung im Repo-Root
        var defaultDir = ResolveDefaultKnowledgeDir();
        if (defaultDir is not null)
            dlg.InitialDirectory = defaultDir;

        if (dlg.ShowDialog() != true) return;

        var sourceDir = Path.GetDirectoryName(dlg.FileName);
        if (string.IsNullOrWhiteSpace(sourceDir)) return;

        try
        {
            // Phase 5.1.B Etappe 3.C: via DI
            var preview = App.Resolve<Infrastructure.Devis.MarktdatenImportService>().PreviewImport(sourceDir);

            var sb = new StringBuilder();
            sb.AppendLine($"Quelle: {preview.SourceDir}");
            sb.AppendLine();
            sb.AppendLine("Verfuegbar:");
            foreach (var f in preview.AvailableFiles)
            {
                var size = preview.FileInfos.TryGetValue(f, out var fi) ? $"{fi.Length / 1024} KB, {fi.LastWriteTime:dd.MM.yyyy HH:mm}" : "";
                var count = preview.RecordCounts.TryGetValue(f, out var c) ? $", {c} Records" : "";
                sb.AppendLine($"  ✓ {f}  ({size}{count})");
            }
            if (preview.MissingFiles.Count > 0)
            {
                sb.AppendLine("Fehlend:");
                foreach (var f in preview.MissingFiles)
                    sb.AppendLine($"  ✗ {f}");
            }
            MarktdatenStatusText.Text = sb.ToString();

            _previewedSourceDir = sourceDir;
            MarktdatenImportButton.IsEnabled = preview.AvailableFiles.Any(f => f != "marktpreise_burglen_2026.json");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler bei Vorschau:\n{ex.Message}", "Marktdaten-Import",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MarktdatenImport_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_previewedSourceDir))
        {
            MessageBox.Show("Bitte zuerst eine Quelle waehlen (Vorschau).", "Marktdaten-Import",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Marktdaten-JSONs aus folgendem Verzeichnis importieren?\n\n{_previewedSourceDir}\n\n" +
            "Bestehende Config-Dateien werden gesichert (.bak).",
            "Marktdaten-Import",
            MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            // Phase 5.1.B Etappe 3.C: via DI
            var result = App.Resolve<Infrastructure.Devis.MarktdatenImportService>().Import(_previewedSourceDir);

            var sb = new StringBuilder();
            sb.AppendLine($"Importiert: {result.ImportedFiles.Count} Datei(en)");
            foreach (var f in result.ImportedFiles)
                sb.AppendLine($"  ✓ {f}");
            if (result.SkippedFiles.Count > 0)
            {
                sb.AppendLine($"Uebersprungen: {result.SkippedFiles.Count}");
                foreach (var f in result.SkippedFiles)
                    sb.AppendLine($"  · {f}");
            }
            if (result.BackupFiles.Count > 0)
            {
                sb.AppendLine($"Backups: {result.BackupFiles.Count}");
                foreach (var f in result.BackupFiles)
                    sb.AppendLine($"  ↻ {f}");
            }
            if (result.Errors.Count > 0)
            {
                sb.AppendLine("Fehler:");
                foreach (var err in result.Errors)
                    sb.AppendLine($"  ✗ {err}");
            }
            sb.Append(result.CachesInvalidated
                ? "Service-Caches invalidiert. Naechste Devis-/KI-Anfrage liest die neuen Daten."
                : "Caches NICHT invalidiert.");

            MessageBox.Show(sb.ToString(), "Marktdaten-Import abgeschlossen",
                MessageBoxButton.OK, MessageBoxImage.Information);

            UpdateMarktdatenStatus();
            MarktdatenImportButton.IsEnabled = false;
            _previewedSourceDir = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Import:\n{ex.Message}", "Marktdaten-Import",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MarktdatenReload_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Phase 5.1.B Etappe 3.C: via DI
            App.Resolve<Infrastructure.Devis.SubmissionsPositionService>().Invalidate();
            App.Resolve<Infrastructure.Devis.HistorischeSanierungenService>().Invalidate();
            UpdateMarktdatenStatus();
            MessageBox.Show(
                "Caches invalidiert. Naechste Devis-/KI-Anfrage liest die JSONs neu vom Datentraeger.",
                "Marktdaten", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler:\n{ex.Message}", "Marktdaten",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? ResolveDefaultKnowledgeDir()
    {
        // Heuristik: Suche Knowledge/sanierung relativ vom Build-Output nach oben
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "Knowledge", "sanierung");
            if (Directory.Exists(candidate))
                return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent) || parent == dir) break;
            dir = parent;
        }
        return null;
    }
}
