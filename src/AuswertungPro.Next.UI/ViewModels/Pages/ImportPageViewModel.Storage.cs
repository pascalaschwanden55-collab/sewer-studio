using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

// ImportPageViewModel Sidecar-Storage: speichert importierte XTF/PDF/TXT-
// Pfade in {project}.imports.json damit Re-Import erkennt was schon
// importiert wurde. Aus dem Hauptdatei extrahiert (Slice 25a).
public sealed partial class ImportPageViewModel
{
    private void StoreXtfFiles(string[] paths)
    {
        var projectPath = _settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            LastResult += "\nHinweis: Projekt bitte speichern, um XTF-Dateien im Projekt abzulegen.";
            return;
        }

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        var targetDir = Path.Combine(projectDir, "Imports", "XTF");
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0) return;

        var existing = LoadStoredXtfFiles(projectDir);
        foreach (var sItem in stored)
            if (!existing.Contains(sItem, StringComparer.OrdinalIgnoreCase))
                existing.Add(sItem);

        _shell.Project.Metadata["XTF_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private void StorePdfFiles(string[] paths)
    {
        var projectPath = _settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            LastResult += "\nHinweis: Projekt bitte speichern, um PDF-Dateien im Projekt abzulegen.";
            return;
        }

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        var targetDir = Path.Combine(projectDir, "Imports", "PDF");
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0) return;

        var existing = LoadStoredPdfFiles(projectDir);
        foreach (var sItem in stored)
            if (!existing.Contains(sItem, StringComparer.OrdinalIgnoreCase))
                existing.Add(sItem);

        _shell.Project.Metadata["PDF_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private void StoreTxtFiles(string[] paths)
    {
        var projectPath = _settings.LastProjectPath;
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            LastResult += "\nHinweis: Projekt bitte speichern, um TXT-Dateien im Projekt abzulegen.";
            return;
        }

        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        if (string.IsNullOrWhiteSpace(projectDir)) return;

        var targetDir = Path.Combine(projectDir, "Imports", "TXT");
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0) return;

        var existing = LoadStoredTxtFiles(projectDir);
        foreach (var sItem in stored)
            if (!existing.Contains(sItem, StringComparer.OrdinalIgnoreCase))
                existing.Add(sItem);

        _shell.Project.Metadata["TXT_StoredFiles"] = JsonSerializer.Serialize(existing);
    }

    private List<string> LoadStoredXtfFiles(string projectDir)
    {
        if (!_shell.Project.Metadata.TryGetValue("XTF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(si => !string.IsNullOrWhiteSpace(si)).Select(si => si.Trim()).ToList() ?? new List<string>();
        }
        catch
        {
            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        }
    }

    private List<string> LoadStoredPdfFiles(string projectDir)
    {
        if (!_shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(si => !string.IsNullOrWhiteSpace(si)).Select(si => si.Trim()).ToList() ?? new List<string>();
        }
        catch
        {
            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        }
    }

    private List<string> LoadStoredTxtFiles(string projectDir)
    {
        if (!_shell.Project.Metadata.TryGetValue("TXT_StoredFiles", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            return list?.Where(si => !string.IsNullOrWhiteSpace(si)).Select(si => si.Trim()).ToList() ?? new List<string>();
        }
        catch
        {
            var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        }
    }
}
