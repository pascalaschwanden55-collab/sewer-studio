using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Infrastructure.Dossiers;
using AuswertungPro.Next.Infrastructure.Dossiers.Preview;

using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers;

/// <summary>
/// Jeder sichtbare Text der Vorlage muss eine Eingabestelle haben.
///
/// Gemessen statt vermutet: Beim ersten Lauf waren fünf Beschriftungen offen —
/// „Datum:", „Revision:", „Proj. Nr. AWU  :", „Erstellungsdatum:" und
/// „Autoren:". Alle fünf stehen mit ihrem Platzhalter im selben Absatz und
/// waren deshalb gesperrt. Der Test nennt jede offene Stelle beim Namen, damit
/// eine geänderte Vorlage nicht still eine Lücke mitbringt.
///
/// Absichtlich NICHT umfasst: Planbilder, Haltungs- und Schachtprotokolle und
/// angehängte PDF-Seiten. Das sind Originale und keine Dossiertexte.
/// </summary>
public sealed class DossierEditableCoverageTests
{
    private readonly ITestOutputHelper _bericht;

    public DossierEditableCoverageTests(ITestOutputHelper bericht) => _bericht = bericht;

    [Fact]
    public void Jeder_sichtbare_Text_der_Vorlage_hat_eine_Eingabestelle()
    {
        var wurzel = new AuswertungPro.Next.Infrastructure.Backup.RepositoryRootFileLocator()
            .Locate(AppContext.BaseDirectory);
        Assert.NotNull(wurzel);

        var dokument = DossierPreviewBuilder.Build(Path.Combine(
            wurzel!, "Export_Vorlage", DossierWordTemplate.TemplateFileName));

        var offeneStellen = new List<string>();

        for (var nummer = 0; nummer < dokument.Pages.Count; nummer++)
        {
            var seite = dokument.Pages[nummer];
            var abgedeckt = new HashSet<string>(
                DossierPreviewTextInventory.Literals(seite), StringComparer.Ordinal);

            var offen = new List<string>();
            Sammle(seite.Blocks, offen);

            var fehlend = offen
                .Where(text => !abgedeckt.Contains(text))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            _bericht.WriteLine(
                $"── Seite {nummer + 1}: {abgedeckt.Count} bearbeitbar, "
                + $"{fehlend.Count} ohne Stelle · Felder: "
                + string.Join(", ", seite.FieldKeys));

            foreach (var text in fehlend)
            {
                _bericht.WriteLine("      OFFEN: " + Kurz(text));
                offeneStellen.Add($"Seite {nummer + 1}: {Kurz(text)}");
            }
        }

        Assert.True(
            offeneStellen.Count == 0,
            "Ohne Eingabestelle: " + string.Join(" · ", offeneStellen));
    }

    private static void Sammle(IEnumerable<DossierPreviewBlock> blocks, List<string> ziel)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case DossierPreviewParagraph absatz:
                    var text = string.Concat(absatz.Runs.Select(run => run.Text)).Trim();

                    // Punktlinien zum Ausfuellen von Hand sind kein Text; sie
                    // waeren im Blatt auch nie anklickbar.
                    if (DossierPreviewTextInventory.IstEchterText(text))
                        ziel.Add(absatz.TocEntry?.Title ?? text);

                    foreach (var schwebend in absatz.Floating)
                        Sammle(schwebend.Blocks, ziel);
                    break;

                case DossierPreviewTable tabelle:
                    foreach (var zelle in tabelle.Rows.SelectMany(row => row.Cells))
                        Sammle(zelle.Paragraphs, ziel);
                    break;
            }
        }
    }

    private static string Kurz(string text)
        => text.Length <= 90 ? text : text[..90] + " …";
}
