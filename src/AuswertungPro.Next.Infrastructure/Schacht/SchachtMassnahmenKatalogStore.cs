using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Schacht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Schacht;

/// <summary>
/// Speichert die globale Schacht-Massnahmen-Liste als
/// <c>%AppData%\SewerStudio\dropdowns\schacht_massnahmen.json</c> (atomar, mit Defaults).
/// Muster wie die bestehenden Dropdown-Listen; Verzeichnis fuer Tests injizierbar.
/// </summary>
public sealed class SchachtMassnahmenKatalogStore : ISchachtMassnahmenKatalogStore
{
    private readonly string _dir;

    public SchachtMassnahmenKatalogStore(string? directory = null)
        // Gleicher Ordner wie die bestehenden Dropdown-Listen (Roaming\SewerStudio\dropdowns),
        // damit der Anwender alle selbst gepflegten Listen an EINER Stelle findet.
        => _dir = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataPathResolver.DefaultProductName,
                "dropdowns")
            : directory!;

    private string FilePath => Path.Combine(_dir, "schacht_massnahmen.json");

    public IReadOnlyList<SchachtMassnahmeKatalogEintrag> Load(out string? loadError)
    {
        loadError = null;

        // File.Exists allein reicht nicht: Es liefert auch bei Zugriffsfehlern und bei
        // einem Ordner am Dateipfad false. Ein Erstlauf und eine unlesbare Datei duerfen
        // aber nicht dasselbe bedeuten (Audit M2).
        var probe = CostStoreFileProbe.Probe(FilePath);
        if (probe.State == CostStorePathState.Missing)
            return Defaults();

        if (probe.State == CostStorePathState.Invalid)
        {
            loadError = probe.Error ?? "Die Massnahmenliste kann nicht gelesen werden.";
            return Defaults();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<SchachtMassnahmeKatalogEintrag>>(json, JsonDefaults.CaseInsensitive);
            if (list is null)
            {
                loadError = "Die Massnahmenliste enthaelt keine gueltige Liste.";
                return Defaults();
            }

            var clean = list.Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Name)).ToList();
            // Eine leere, aber gueltige Datei ist eine bewusste Entscheidung des Anwenders
            // und kein Fehler; die Standardliste hilft ihm hier weiter.
            return clean.Count == 0 ? Defaults() : clean;
        }
        catch (Exception ex)
        {
            // Beschaedigt/gesperrt: sichtbar melden statt still die Standardliste
            // auszugeben — sonst ueberschreibt der Editor die echte Liste.
            loadError = ex.Message;
            return Defaults();
        }
    }

    public bool Save(IEnumerable<SchachtMassnahmeKatalogEintrag> eintraege, out string? error)
    {
        error = null;

        // Zweites Sicherheitsnetz: Selbst wenn ein Aufrufer den loadError uebersieht,
        // wird eine vorhandene, nicht sicher lesbare Liste nie ueberschrieben.
        Load(out var loadError);
        if (loadError is not null)
        {
            error = "Die vorhandene Massnahmenliste kann nicht gelesen werden und wird " +
                    $"deshalb nicht ueberschrieben: {loadError}";
            return false;
        }

        var clean = (eintraege ?? Enumerable.Empty<SchachtMassnahmeKatalogEintrag>())
            .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Name))
            .Select(e => e with
            {
                Name = e.Name.Trim(),
                Einheit = string.IsNullOrWhiteSpace(e.Einheit) ? "Stk" : e.Einheit.Trim(),
            })
            .ToList();

        try
        {
            Directory.CreateDirectory(_dir);
            var json = JsonSerializer.Serialize(clean, JsonDefaults.Indented);
            AtomicTextFileWriter.WriteAllText(FilePath, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Sinnvolle Start-Liste typischer Schacht-Massnahmen. Preise sind Platzhalter (CHF),
    /// die der Anwender selbst anpasst.
    /// </summary>
    public static IReadOnlyList<SchachtMassnahmeKatalogEintrag> Defaults() => new List<SchachtMassnahmeKatalogEintrag>
    {
        new() { Name = "Schacht reinigen", Preis = 150m, Einheit = "Stk" },
        new() { Name = "Deckel ersetzen", Preis = 450m, Einheit = "Stk" },
        new() { Name = "Rahmen und Deckel ersetzen", Preis = 850m, Einheit = "Stk" },
        new() { Name = "Steigeisen ersetzen", Preis = 60m, Einheit = "Stk" },
        new() { Name = "Konus / Auflagering sanieren", Preis = 350m, Einheit = "Stk" },
        new() { Name = "Schachthals sanieren", Preis = 400m, Einheit = "Stk" },
        new() { Name = "Bankett / Gerinne sanieren", Preis = 550m, Einheit = "Stk" },
        new() { Name = "Fugen sanieren", Preis = 220m, Einheit = "Stk" },
        new() { Name = "Schacht beschichten", Preis = 900m, Einheit = "Stk" },
        new() { Name = "Schachtunterteil ersetzen", Preis = 1200m, Einheit = "Stk" },
    };
}
