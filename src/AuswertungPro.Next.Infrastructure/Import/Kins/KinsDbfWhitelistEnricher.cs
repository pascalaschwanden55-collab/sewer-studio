using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Common;
using AuswertungPro.Next.Infrastructure.Import.Dbf;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>Ergebnis der KINS-DBF-Anreicherung.</summary>
public sealed record KinsDbfEnrichResult(
    int HaltungsfelderGesetzt,
    int SchaechteNeu,
    int SchaechteAktualisiert,
    IReadOnlyList<string> Messages);

/// <summary>
/// Whitelist-Anreicherung aus den FoxPro-Stammdaten der KINS-DVD
/// (haltung.DBF/schacht.DBF): fuellt NUR leere Haltungsfelder und legt die
/// Schachtliste an. Prioritaet: UserEdit &gt; XTF &gt; kiDVDaten.txt &gt; DBF.
/// Muster: Sia405WhitelistEnricher (empty-only, UserEdited-Skip).
/// </summary>
public static class KinsDbfWhitelistEnricher
{
    public static KinsDbfEnrichResult Apply(Project project, string sourceFolder, ImportRunContext? ctx = null)
    {
        var messages = new List<string>();

        if (project is null || string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
        {
            messages.Add("KINS-DBF: Quellordner nicht gefunden — Anreicherung uebersprungen.");
            return new KinsDbfEnrichResult(0, 0, 0, messages);
        }

        var schachtDbf = FindeDatei(sourceFolder, "schacht.DBF");
        var haltungDbf = FindeDatei(sourceFolder, "haltung.DBF");
        if (schachtDbf is null && haltungDbf is null)
        {
            messages.Add("KINS-DBF: keine haltung.DBF/schacht.DBF gefunden — Anreicherung uebersprungen.");
            return new KinsDbfEnrichResult(0, 0, 0, messages);
        }

        var schachtNamenProNr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var schaechteNeu = 0;
        var schaechteAktualisiert = 0;

        if (schachtDbf is not null)
            ImportiereSchaechte(project, schachtDbf, schachtNamenProNr, messages, ctx, ref schaechteNeu, ref schaechteAktualisiert);

        var haltungsfelder = 0;
        if (haltungDbf is not null)
            haltungsfelder = ReichereHaltungenAn(project, haltungDbf, schachtNamenProNr, messages);

        return new KinsDbfEnrichResult(haltungsfelder, schaechteNeu, schaechteAktualisiert, messages);
    }

    // ------------------------------------------------------------------
    // schacht.DBF → Schachtliste (idempotent per Schachtnummer)
    // ------------------------------------------------------------------

    private static void ImportiereSchaechte(
        Project project,
        string schachtDbf,
        Dictionary<string, string> schachtNamenProNr,
        List<string> messages,
        ImportRunContext? ctx,
        ref int neu,
        ref int aktualisiert)
    {
        DbfTable tabelle;
        try
        {
            tabelle = DbfTable.Read(schachtDbf);
        }
        catch (Exception ex)
        {
            messages.Add($"KINS-DBF: schacht.DBF nicht lesbar: {ex.Message}");
            return;
        }

        foreach (var row in tabelle.Rows)
        {
            var nummer = Wert(row, "BEZ");
            if (string.IsNullOrWhiteSpace(nummer))
                continue;

            // NR → Nummer merken (Schluessel fuer S_O/S_U in haltung.DBF)
            var nr = Wert(row, "NR");
            if (!string.IsNullOrWhiteSpace(nr))
                schachtNamenProNr[nr] = nummer;

            var record = FindeSchacht(project, nummer);
            if (record is null)
            {
                record = new SchachtRecord();
                record.SetFieldValue("Schachtnummer", nummer);
                if (ctx is null)
                    project.SchaechteData.Add(record);
                else
                    ctx.WithCollectionLock(() => project.SchaechteData.Add(record));
                neu++;
            }
            else
            {
                aktualisiert++;
            }

            // Stammdaten nur setzen, wenn im Programm noch leer (empty-only)
            SetzeSchachtfeldWennLeer(record, "Strasse", Wert(row, "STRASSE"));
            SetzeSchachtfeldWennLeer(record, "Material", Wert(row, "MATERIAL"));
            SetzeSchachtfeldWennLeer(record, "Schachttiefe", DezimalOderLeer(Wert(row, "TIEFE"), "0.00"));
            SetzeSchachtfeldWennLeer(record, "Datum/Jahr", DatumAusJjjjMmTt(Wert(row, "UNTDATUM")));
        }
    }

    private static SchachtRecord? FindeSchacht(Project project, string nummer)
        => project.SchaechteData.FirstOrDefault(s =>
            string.Equals((s.GetFieldValue("Schachtnummer") ?? "").Trim(), nummer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((s.GetFieldValue("SchachtNr") ?? "").Trim(), nummer, StringComparison.OrdinalIgnoreCase));

    private static void SetzeSchachtfeldWennLeer(SchachtRecord record, string feld, string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return;
        if (!string.IsNullOrWhiteSpace(record.GetFieldValue(feld)))
            return;
        record.SetFieldValue(feld, wert.Trim());
    }

    // ------------------------------------------------------------------
    // haltung.DBF → Whitelist fuer leere Haltungsfelder
    // ------------------------------------------------------------------

    private static int ReichereHaltungenAn(
        Project project,
        string haltungDbf,
        Dictionary<string, string> schachtNamenProNr,
        List<string> messages)
    {
        DbfTable tabelle;
        try
        {
            tabelle = DbfTable.Read(haltungDbf);
        }
        catch (Exception ex)
        {
            messages.Add($"KINS-DBF: haltung.DBF nicht lesbar: {ex.Message}");
            return 0;
        }

        var gesetzt = 0;

        foreach (var row in tabelle.Rows)
        {
            var record = FindeHaltung(project, row, schachtNamenProNr);
            if (record is null)
            {
                var bez = Wert(row, "BEZ");
                if (!string.IsNullOrWhiteSpace(bez))
                    messages.Add($"KINS-DBF: Haltung mit Bezeichnung '{bez}' nicht im Projekt — uebersprungen.");
                continue;
            }

            gesetzt += SetzeHaltungsfeldWennLeer(record, "Strasse", Wert(row, "STRASSE"));
            gesetzt += SetzeHaltungsfeldWennLeer(record, "Eigentuemer", Wert(row, "EIGENT"));
            gesetzt += SetzeHaltungsfeldWennLeer(record, "Rohrmaterial", Wert(row, "MATERIAL"));
            gesetzt += SetzeHaltungsfeldWennLeerOderNull(record, "Haltungslaenge_m", DezimalOderLeer(Wert(row, "HALTLAENGE"), "0.0##"));
            gesetzt += SetzeHaltungsfeldWennLeerOderNull(record, "DN_mm", GanzzahlOderLeer(Wert(row, "BREITE")));

            // Felder ohne Ziel im FieldCatalog: nur melden, nicht setzen (bewusst, s. Plan).
            var baujahr = GanzzahlOderLeer(Wert(row, "BAUJAHR"));
            if (!string.IsNullOrWhiteSpace(baujahr))
                messages.Add($"KINS-DBF: Baujahr {baujahr} vorhanden, aber kein Zielfeld — nicht uebernommen.");
        }

        return gesetzt;
    }

    private static HaltungRecord? FindeHaltung(
        Project project,
        IReadOnlyDictionary<string, string> row,
        Dictionary<string, string> schachtNamenProNr)
    {
        // 1. Schachtpaar S_O/S_U → Schachtnummern → "{oben}-{unten}"
        if (schachtNamenProNr.TryGetValue(Wert(row, "S_O") ?? "", out var oben) &&
            schachtNamenProNr.TryGetValue(Wert(row, "S_U") ?? "", out var unten))
        {
            var schluessel = Normalisiere($"{oben}-{unten}");
            var perPaar = project.Data.FirstOrDefault(r =>
                string.Equals(Normalisiere(r.GetFieldValue("Haltungsname")), schluessel, StringComparison.OrdinalIgnoreCase));
            if (perPaar is not null)
                return perPaar;
        }

        // 2. Fallback: BEZ entspricht dem (noch nicht normalisierten) Haltungsnamen
        var bez = Wert(row, "BEZ");
        if (string.IsNullOrWhiteSpace(bez))
            return null;

        return project.Data.FirstOrDefault(r =>
            string.Equals(Normalisiere(r.GetFieldValue("Haltungsname")), Normalisiere(bez), StringComparison.OrdinalIgnoreCase));
    }

    private static int SetzeHaltungsfeldWennLeer(HaltungRecord record, string feld, string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return 0;
        if (IstUserEdited(record, feld))
            return 0;
        if (!string.IsNullOrWhiteSpace(record.GetFieldValue(feld)))
            return 0;

        record.SetFieldValue(feld, wert.Trim(), FieldSource.Legacy, userEdited: false);
        return 1;
    }

    /// <summary>Wie SetzeHaltungsfeldWennLeer, behandelt aber numerisch 0 als leer (XTF schreibt "0").</summary>
    private static int SetzeHaltungsfeldWennLeerOderNull(HaltungRecord record, string feld, string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            return 0;
        if (IstUserEdited(record, feld))
            return 0;

        var vorhanden = (record.GetFieldValue(feld) ?? string.Empty).Trim();
        var istLeerOderNull = string.IsNullOrWhiteSpace(vorhanden)
            || (double.TryParse(vorhanden.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v == 0d);
        if (!istLeerOderNull)
            return 0;

        record.SetFieldValue(feld, wert, FieldSource.Legacy, userEdited: false);
        return 1;
    }

    private static bool IstUserEdited(HaltungRecord record, string feld)
        => record.FieldMeta.TryGetValue(feld, out var meta) && meta.UserEdited;

    // ------------------------------------------------------------------
    // Wert-Normalisierung
    // ------------------------------------------------------------------

    private static string? Wert(IReadOnlyDictionary<string, string> row, string feld)
        => row.TryGetValue(feld, out var wert) ? wert : null;

    /// <summary>Dezimalzahl-Text normalisieren; leer bei fehlendem/0-Wert.</summary>
    private static string? DezimalOderLeer(string? text, string format = "0.0##")
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var wert))
            return null;
        return wert > 0d ? wert.ToString(format, CultureInfo.InvariantCulture) : null;
    }

    private static string? GanzzahlOderLeer(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var wert))
            return null;
        return wert > 0d ? ((long)Math.Round(wert)).ToString(CultureInfo.InvariantCulture) : null;
    }

    /// <summary>UNTDATUM "JJJJMMTT" → "TT.MM.JJJJ"; leer wenn nicht parsebar.</summary>
    private static string? DatumAusJjjjMmTt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return DateTime.TryParseExact(text.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum)
            ? datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : null;
    }

    private static string Normalisiere(string? wert)
        => string.IsNullOrWhiteSpace(wert)
            ? string.Empty
            : wert.Trim().Replace(" ", string.Empty).ToUpperInvariant();

    private static string? FindeDatei(string root, string dateiName)
    {
        try
        {
            return SafeFileEnumeration.EnumerateFilesSafe(root, dateiName, recursive: true)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

}
