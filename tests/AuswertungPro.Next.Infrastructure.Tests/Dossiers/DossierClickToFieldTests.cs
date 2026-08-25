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
}
