using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Eingabe-Datenstruktur fuer den Aufbau eines ProtocolEntry aus der VSA-Code-Auswahl.
/// </summary>
public sealed class VsaSelectionInput
{
    public string FinalCode { get; init; } = "";
    public string FinalLabel { get; init; } = "";
    public string? FinalSublabel { get; init; }
    public bool IsStreckenschaden { get; init; }
    public string MeterStart { get; init; } = "";
    public string? MeterEnd { get; init; }
    public string? Zeit { get; init; }
    public string Q1Value { get; init; } = "";
    public string Q2Value { get; init; } = "";
    public string ClockMode { get; init; } = "range";
    public string? ClockVon { get; init; }
    public string? ClockBis { get; init; }
    public bool AnRohrverbindung { get; init; }
    public string? StreckenschadenTyp { get; init; }
    public string? Bemerkungen { get; init; }
    public IReadOnlyList<string> FotoPaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OriginalFotoPaths { get; init; } = Array.Empty<string>();
    public VsaCodeDef? CurrentVsaCodeDef { get; init; }
}

/// <summary>
/// Baut einen <see cref="ProtocolEntry"/> aus den Feldern der VSA-Code-Auswahl.
/// Reine Logik, keine UI-Abhaengigkeiten.
/// </summary>
public static class ProtocolEntryFromVsaSelectionBuilder
{
    /// <summary>
    /// Erstellt oder aktualisiert einen ProtocolEntry anhand der VSA-Selektion.
    /// Wenn <paramref name="existingEntry"/> angegeben, wird dieser in-place aktualisiert;
    /// andernfalls wird ein neuer Entry erstellt.
    /// </summary>
    public static ProtocolEntry Build(VsaSelectionInput input, ProtocolEntry? existingEntry = null)
    {
        var entry = existingEntry ?? new ProtocolEntry();

        entry.Code = input.FinalCode;
        entry.Beschreibung = BuildBeschreibung(input.FinalLabel, input.FinalSublabel);
        entry.IsStreckenschaden = input.IsStreckenschaden;

        if (VsaCodeEntryValidator.TryParseDouble(input.MeterStart, out var ms))
            entry.MeterStart = ms;

        if (!string.IsNullOrWhiteSpace(input.MeterEnd)
            && VsaCodeEntryValidator.TryParseDouble(input.MeterEnd, out var me))
            entry.MeterEnd = me;
        else
            entry.MeterEnd = null;

        if (!string.IsNullOrWhiteSpace(input.Zeit)
            && VsaCodeEntryValidator.TryParseTime(input.Zeit, out var zeit))
            entry.Zeit = zeit;

        // CodeMeta aufbauen
        entry.CodeMeta ??= new ProtocolEntryCodeMeta();
        entry.CodeMeta.Code = input.FinalCode;
        entry.CodeMeta.UpdatedAt = DateTimeOffset.UtcNow;

        var p = entry.CodeMeta.Parameters;
        AddCatalogMetadata(p, input.CurrentVsaCodeDef);
        SetOrRemove(p, "vsa.q1", input.Q1Value);
        SetOrRemove(p, "vsa.q2", input.Q2Value);

        var (clockVon, clockBis) = NormalizeClockValues(input.ClockMode, input.ClockVon, input.ClockBis);
        SetOrRemove(p, "vsa.uhr.von", clockVon);
        SetOrRemove(p, "vsa.uhr.bis", clockBis);

        // Zusatzfelder
        SetOrRemove(p, "vsa.rohrverbindung", input.AnRohrverbindung ? "1" : null);
        SetOrRemove(p, "vsa.strecke.typ",
            string.IsNullOrWhiteSpace(input.StreckenschadenTyp) ? null : input.StreckenschadenTyp);
        SetOrRemove(p, "vsa.bemerkungen",
            string.IsNullOrWhiteSpace(input.Bemerkungen) ? null : input.Bemerkungen);

        // Fotos
        entry.FotoPaths.Clear();
        foreach (var foto in input.FotoPaths)
            entry.FotoPaths.Add(foto);

        entry.OriginalFotoPaths.Clear();
        foreach (var originalFoto in input.OriginalFotoPaths)
            entry.OriginalFotoPaths.Add(originalFoto);

        return entry;
    }

    /// <summary>
    /// Baut den Beschreibungstext aus Label und Sublabel zusammen.
    /// </summary>
    public static string BuildBeschreibung(string finalLabel, string? finalSublabel)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(finalLabel)) parts.Add(finalLabel);
        if (!string.IsNullOrEmpty(finalSublabel)) parts.Add(finalSublabel);
        return string.Join(" - ", parts);
    }

    /// <summary>
    /// Normalisiert Von/Bis gemaess Clock-Mode (none / single / range).
    /// Gibt null zurueck wenn der Wert ungueltig oder im Modus "none" ist.
    /// </summary>
    public static (string? Von, string? Bis) NormalizeClockValues(
        string clockMode,
        string? rawVon,
        string? rawBis)
    {
        var von = NormalizeClockValue(rawVon);
        var bis = NormalizeClockValue(rawBis);

        if (clockMode == "none")
            return (null, null);

        if (clockMode == "single")
        {
            // Einzelpunkt: Bis immer "00"
            bis = string.IsNullOrWhiteSpace(von) ? null : "00";
            return (von, bis);
        }

        // range: Bis leer = Punktschaden, automatisch "00"
        if (!string.IsNullOrWhiteSpace(von) && string.IsNullOrWhiteSpace(bis))
            bis = "00";

        return (von, bis);
    }

    // ── Private Hilfsmethoden ──────────────────────────────────────

    private static string? NormalizeClockValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (!int.TryParse(text, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var v)
            || v < 0 || v > 12)
            return null;

        return v.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void SetOrRemove(Dictionary<string, string> dict, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            dict.Remove(key);
        else
            dict[key] = value.Trim();
    }

    private static void AddCatalogMetadata(Dictionary<string, string> parameters, VsaCodeDef? codeDef)
    {
        if (codeDef is null)
            return;

        SetOrRemove(parameters, "catalog.source", codeDef.Source);
        SetOrRemove(parameters, "catalog.canonicalCode", codeDef.CanonicalCode);
        SetOrRemove(parameters, "catalog.standardAnnotation", codeDef.StandardAnnotation);
    }
}
