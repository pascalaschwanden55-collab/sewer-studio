using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Import + Protocol-Sync extrahiert aus PlayerWindow.xaml.cs.
//
// Slice 8a.3 Step 5b: Alle Import-Klick-Handler (ImportEvents_DoubleClick,
// ImportSeek_Click, ImportConfirm_Click) sowie LoadExistingProtocolEntries
// und ShowCodingProtocolPreview / CodingOfferPdfExport entfaellt — sie
// wurden vom alten In-Place-Codier-Modus + dessen IMPORT-Spalte (LstImportEvents)
// gerufen. Beides existiert nach Step 5b nicht mehr.
//
// Was bleibt: SyncCodingToPrimaryDamages — wird vom Bridge-Pfad
// (CodingMode_Click → CodingModeWindow.ShowDialog → Protokoll zurueck)
// in PlayerWindow.CodingApply.cs gerufen.
public partial class PlayerWindow
{
    private void SyncCodingToPrimaryDamages(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var entries = doc.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();
        if (entries == null || entries.Count == 0)
        {
            _haltungRecord.SetFieldValue("Primaere_Schaeden", "", FieldSource.Manual, userEdited: true);
            _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
            return;
        }

        // Zeilen fuer Primaere_Schaeden aufbauen
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var code = (entry.Code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;

            var meter = entry.MeterStart ?? entry.MeterEnd;
            var meterKey = meter.HasValue ? meter.Value.ToString("F2") : "";
            if (!seen.Add($"{code.ToUpperInvariant()}|{meterKey}")) continue;

            var parts = new List<string>();
            if (meter.HasValue) parts.Add($"{meter.Value:0.00}m");
            parts.Add(code);
            if (!string.IsNullOrWhiteSpace(entry.Beschreibung))
                parts.Add(entry.Beschreibung.Trim().Replace("\r", "").Replace("\n", " "));

            if (entry.CodeMeta?.Parameters != null)
            {
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q1", out var q1) && !string.IsNullOrWhiteSpace(q1))
                    parts.Add($"Q1={q1}");
                if (entry.CodeMeta.Parameters.TryGetValue("vsa.q2", out var q2) && !string.IsNullOrWhiteSpace(q2))
                    parts.Add($"Q2={q2}");
            }

            lines.Add(string.Join(" ", parts));
        }

        var primaryText = string.Join("\n", lines);
        _haltungRecord.SetFieldValue("Primaere_Schaeden", primaryText, FieldSource.Manual, userEdited: true);
        _haltungRecord.ModifiedAtUtc = DateTime.UtcNow;
    }
}
