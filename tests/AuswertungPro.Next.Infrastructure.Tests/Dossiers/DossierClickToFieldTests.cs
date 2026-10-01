using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;

using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Der ganze Weg vom Klick ins Blatt bis zum Feld rechts — für jeden Text,
/// jede Zeile und jedes Feld der echten Vorlage.
///
/// Der Weg hat drei Glieder, und jedes kann für sich reissen:
/// 1. Zu der Stelle gibt es rechts überhaupt eine Eingabe.
/// 2. Aus der Eingabe entsteht ein Suchtext (<c>BuildCandidates</c>).
/// 3. Dieser Text wird in den Wörtern der PDF-Seite wiedergefunden
///    (<c>DossierOutputPreviewHitMatcher</c>) — erst dann ist die Stelle im
///    Blatt anklickbar.
///
/// Geprüft wird gegen die ausgelieferte Vorlage, nicht gegen ein Beispiel: Der
/// Test nennt jede Stelle beim Namen, die durchfällt.
/// </summary>
public sealed class DossierClickToFieldTests
{
    private readonly ITestOutputHelper _bericht;

    public DossierClickToFieldTests(ITestOutputHelper bericht) => _bericht = bericht;

    /// <summary>Ein Dossier mit gefüllten Angaben — leere Felder sind nicht anklickbar.</summary>
    private static DossierDefinition Dossier()
    {
        return new DossierDefinition
        {
            Name = "Liegenschaft Nr. 439 Dittli"
        };
    }

    private static (DossierPreviewDocument Dokument, IReadOnlyList<DossierPreviewField> Felder,
        IReadOnlyDictionary<string, string> Werte) Aufbau()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var dokument = DossierPreviewBuilder.Build(Path.Combine(
            wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName));

        var werte = new Dictionary<string, string>(StringComparer.Ordinal);
        var nummer = 1;

        // Jeder Platzhalter bekommt einen eindeutigen, gut wiederfindbaren
        // Wert. Zwei gleiche Werte wuerden die Zuordnung mehrdeutig machen und
        // den Test wertlos.
        foreach (var key in dokument.Pages.SelectMany(seite => seite.FieldKeys).Distinct())
            werte[key] = $"Pruefwert{nummer++}";

        var felder = DossierPreviewFieldCatalog.Build(
            new DossierAreaSettings(),
            Dossier(),
            key => werte.TryGetValue(key, out var wert) ? wert : string.Empty);

        return (dokument, felder, werte);
    }

    /// <summary>Die Ziele einer Seite so, wie die Eingabeseite sie anbietet.</summary>
    private static IReadOnlyList<DossierPreviewTarget> Ziele(
        DossierPreviewPage seite,
        IReadOnlyList<DossierPreviewField> felder,
        DossierDefinition dossier,
        IReadOnlyDictionary<string, string> werte)
    {
        var ziele = new List<DossierPreviewTarget>();

        foreach (var feld in DossierPreviewFieldCatalog.ForPage(
                     felder,
                     seite,
                     dossier,
                     key => werte.TryGetValue(key, out var wert) ? wert : string.Empty))
        {
            ziele.Add(DossierPreviewTarget.Field(feld.Key));
        }

        foreach (var text in DossierPreviewTextInventory.Literals(seite))
            ziele.Add(DossierPreviewTarget.Literal(text));

        return ziele;
    }

    [Fact]
    public void Jedes_angebotene_Ziel_liefert_einen_Suchtext()
    {
        var (dokument, felder, werte) = Aufbau();
        var dossier = Dossier();
        var ohne = new List<string>();

        foreach (var seite in dokument.Pages)
        {
            foreach (var ziel in Ziele(seite, felder, dossier, werte))
            {
                var kandidaten = DossierOutputPreviewInteractionMapper.BuildCandidates(
                    [ziel],
                    felder,
                    werte,
                    dossier,
                    _ => Array.Empty<IReadOnlyDictionary<string, string>>());

                if (!kandidaten.Any(kandidat => kandidat.Text.Trim().Length > 0))
                    ohne.Add($"Seite {seite.Number}: {ziel.Kind} „{ziel.Key}\"");
            }
        }

        foreach (var eintrag in ohne)
            _bericht.WriteLine("OHNE SUCHTEXT: " + eintrag);

        Assert.True(ohne.Count == 0, "Ohne Suchtext: " + string.Join(" · ", ohne));
    }

    [Fact]
    public void Ein_Klick_auf_den_Text_findet_sein_Ziel()
    {
        var (dokument, felder, werte) = Aufbau();
        var dossier = Dossier();
        var ungetroffen = new List<string>();

        foreach (var seite in dokument.Pages)
        {
            var ziele = Ziele(seite, felder, dossier, werte);
            var kandidaten = DossierOutputPreviewInteractionMapper.BuildCandidates(
                ziele,
                felder,
                werte,
                dossier,
                _ => Array.Empty<IReadOnlyDictionary<string, string>>());

            foreach (var kandidat in kandidaten.Where(k => k.Text.Trim().Length > 0))
            {
                // Ein Blatt, auf dem genau dieser Text steht — mehr braucht es
                // nicht: Gemessen wird, ob der Sucher seinen eigenen Text
                // wiederfindet.
                var treffer = DossierOutputPreviewHitMatcher.Match(
                    Woerter(kandidat.Text),
                    [kandidat]);

                if (!treffer.Values.Any(liste => liste.Contains(kandidat.Target)))
                {
                    ungetroffen.Add(
                        $"Seite {seite.Number}: {kandidat.Target.Kind} "
                        + $"„{kandidat.Target.Key}\" → „{Kurz(kandidat.Text)}\"");
                }
            }
        }

        foreach (var eintrag in ungetroffen)
            _bericht.WriteLine("NICHT ANKLICKBAR: " + eintrag);

        Assert.True(
            ungetroffen.Count == 0,
            "Nicht anklickbar: " + string.Join(" · ", ungetroffen));
    }

    [Fact]
    public void Der_Klick_waehlt_die_genaueste_Stelle()
    {
        // In einer Tabelle liegen Zelle und Zeile uebereinander. Getroffen
        // werden muss die Zelle — sonst spraenge ein Klick auf den Eigentuemer
        // in die ganze Zeile.
        var zelle = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Name");
        var zeile = DossierPreviewTarget.Row("Eigentuemer", 0);

        var gewaehlt = DossierPreviewTarget.SelectMostSpecific([zeile, zelle], _ => true);

        Assert.Equal(zelle, gewaehlt);
    }

    [Fact]
    public void Eine_Stelle_ohne_Editor_wird_nicht_vorgetaeuscht()
    {
        // Ein Klick, der nirgends hinfuehrt, ist schlimmer als gar keiner.
        var zelle = DossierPreviewTarget.RowCell("Eigentuemer", 0, "Name");
        var zeile = DossierPreviewTarget.Row("Eigentuemer", 0);

        var gewaehlt = DossierPreviewTarget.SelectMostSpecific(
            [zeile, zelle],
            ziel => ziel.Kind == DossierPreviewTargetKind.Row);

        Assert.Equal(zeile, gewaehlt);
    }

    private static IReadOnlyList<DossierOutputPreviewWord> Woerter(string text)
        => text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select((wort, index) => new DossierOutputPreviewWord(
                wort, index * 10, 0, index * 10 + 9, 10))
            .ToList();

    private static string Kurz(string text)
        => text.Length <= 60 ? text : text[..60] + " …";

    [Fact]
    public void Jede_Zeile_und_jede_Spalte_der_Tabellen_ist_anklickbar()
    {
        // Pascals Anforderung: Jede Spalte, jede Zeile bekommt ihr eigenes
        // Feld. Im Blatt hatte die Spalte „Thema" keinen Rahmen, die Spalte
        // „Bemerkungen" schon — gemessen wird, woran das liegt.
        // Der echte Bestand aus Pascals Blatt.
        var dossier = Dossier();
        dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "30",
            ParcelNumber = "439",
            Name = "Karl Theodor Dittli"
        });
        dossier.Owners.Add(new DossierOwnerRow
        {
            HouseNumber = "30",
            ParcelNumber = "439",
            Name = "Johanna Meyer"
        });

        var gebiet = new DossierAreaSettings();
        foreach (var titel in new[]
                 {
                     "Ausführungstermin", "Ansprechpartner", "Unternehmer",
                     "Örtliche Bauleitung", "Behinderungen"
                 })
        {
            gebiet.Topics.Add(new DossierTopicRow { Title = titel, Text = "unbekannt" });
        }

        var zeilen = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, string>>>(
            StringComparer.Ordinal)
        {
            ["Themen"] = DossierWordTemplateExportService.BuildTopicRows(
                gebiet, dossier, new Dictionary<string, string>()),
            ["Eigentuemer"] = DossierWordTemplateExportService.BuildOwnerRows(dossier)
        };

        var fehlend = new List<string>();

        foreach (var (schluessel, liste) in zeilen)
        {
            _bericht.WriteLine($"── {schluessel}: {liste.Count} Zeilen");

            for (var zeile = 0; zeile < liste.Count; zeile++)
            {
                foreach (var (spalte, inhalt) in liste[zeile])
                {
                    if (spalte.Contains("Style", StringComparison.Ordinal)
                        || spalte.Contains("Farbe", StringComparison.Ordinal)
                        || inhalt.Trim().Length == 0)
                    {
                        continue;
                    }

                    var ziel = DossierPreviewTarget.RowCell(schluessel, zeile, spalte);
                    var kandidaten = DossierOutputPreviewInteractionMapper.BuildCandidates(
                        [ziel],
                        Array.Empty<DossierPreviewField>(),
                        new Dictionary<string, string>(),
                        dossier,
                        key => zeilen.TryGetValue(key, out var treffer)
                            ? treffer
                            : Array.Empty<IReadOnlyDictionary<string, string>>());

                    var text = kandidaten.FirstOrDefault()?.Text ?? string.Empty;
                    var trifft = text.Trim().Length > 0
                        && DossierOutputPreviewHitMatcher
                            .Match(Woerter(inhalt), kandidaten)
                            .Values.Any(liste2 => liste2.Contains(ziel));

                    _bericht.WriteLine(
                        $"   Zeile {zeile} · {spalte}: {(trifft ? "anklickbar" : "OFFEN")}"
                        + $" — „{Kurz(inhalt)}\"");

                    if (!trifft)
                        fehlend.Add($"{schluessel}[{zeile}].{spalte} = „{Kurz(inhalt)}\"");
                }
            }
        }

        Assert.True(fehlend.Count == 0, "Nicht anklickbar: " + string.Join(" · ", fehlend));
    }

    [Fact]
    public void Gleiche_Texte_in_verschiedenen_Zeilen_bleiben_unterscheidbar()
    {
        // Im echten Blatt steht in vier Themenzeilen dasselbe Wort
        // „unbekannt". Trifft der Sucher jeden Kandidaten an JEDER Fundstelle,
        // fuehrt ein Klick auf die vierte Zeile in das Feld der ersten — und
        // vier Zellen leuchten gemeinsam auf.
        var worte = Woerter(
            "Ausführungstermin unbekannt Ansprechpartner unbekannt "
            + "Unternehmer unbekannt Örtliche Bauleitung unbekannt");

        var kandidaten = Enumerable.Range(0, 4)
            .Select(zeile => new DossierPreviewTextCandidate(
                DossierPreviewTarget.RowCell("Themen", zeile, "Text"),
                "unbekannt"))
            .ToList();

        var treffer = DossierOutputPreviewHitMatcher.Match(worte, kandidaten);

        foreach (var (wort, ziele) in treffer.OrderBy(paar => paar.Key))
            _bericht.WriteLine($"Wort {wort}: {ziele.Count} Ziel(e)");

        // Jede Fundstelle gehoert genau einer Zeile.
        foreach (var (wort, ziele) in treffer)
        {
            Assert.True(
                ziele.Count == 1,
                $"Wort {wort} traegt {ziele.Count} Ziele statt einem.");
        }

        // Und zwar in Leserichtung: erste Fundstelle -> erste Zeile.
        var reihenfolge = treffer
            .OrderBy(paar => paar.Key)
            .Select(paar => paar.Value[0].RowIndex)
            .ToList();

        Assert.Equal([0, 1, 2, 3], reihenfolge);
    }
}
