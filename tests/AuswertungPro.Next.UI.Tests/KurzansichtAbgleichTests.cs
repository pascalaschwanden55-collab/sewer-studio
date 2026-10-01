using System.Text.Json;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KurzansichtAbgleichTests
{
    public static IEnumerable<object[]> SchachtEingaben()
    {
        foreach (var f in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == "schacht" && f.Speicherfeld is not null && !f.NurLesen))
        {
            var eintraege = new[] { f.KatalogId, f.KatalogIdJeEltern }.Where(k => k is not null)
                .SelectMany(k => FieldCatalog.Objektfelder.Auswahl(k)?.Eintraege ?? [])
                .Select(e => e.Label).Where(t => t.Length > 0).Distinct().ToArray();
            foreach (var text in eintraege.Length > 0 ? eintraege : new[] { f.Id == "schacht.baujahr" ? "1974" : f.Id == "schacht.tiefe" ? "2.89" : "Eigene Angabe" })
                yield return [f.Id, text];
        }
    }

    [Theory]
    [MemberData(nameof(SchachtEingaben))]
    public void Jede_gemeinsame_Schachteingabe_bleibt_in_Kurz_sichtbar_ohne_Datenaenderung(string feldId, string text)
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        var f = FieldCatalog.Objektfelder.Feld(feldId);
        b.Schreibe(b.Wurzel, f, "", text);
        var stand = JsonSerializer.Serialize(p);
        var commits = 0;
        var builder = new SchaechteRecordDetailsBuilder(Optionen, _ => null, (_, _, _) => commits++);
        var item = Assert.Single(builder.Build([f.Speicherfeld!], s).SelectMany(g => g.Items).Where(i => i.FieldName == f.Speicherfeld));
        Assert.Equal(b.Lies(b.Wurzel, f), item.Value);
        if (item.IsCombo && !item.AllowFreeText) Assert.Contains(item.SelectedOption, item.AnzeigeOptionen);
        Assert.Equal(stand, JsonSerializer.Serialize(p));
        Assert.Equal(0, commits);
    }

    [Theory]
    [InlineData("Schachtform", "Kreisprofil")]
    [InlineData("Form", "Kreisprofil")]
    [InlineData("Material", "Beton, Fertigteil")]
    [InlineData("Sanierungsbedarf", "Saniert")]
    public void Belegte_Werte_der_Objektmaske_werden_nicht_als_falsche_Auswahl_gemeldet(string feld, string text)
    {
        var s = new SchachtRecord(); s.SetFieldValue(feld, text, FieldSource.Manual, true);
        var item = Assert.Single(new SchaechteRecordDetailsBuilder(Optionen, _ => null, (_, _, _) => { })
            .Build([feld], s).SelectMany(g => g.Items));
        Assert.Contains(item.SelectedOption, item.AnzeigeOptionen);
        Assert.Empty(item.AuswahlHinweis);
    }

    private static IEnumerable<string> Optionen(string quelle) => quelle switch
    {
        "SchachtformOptions" => SchachtformVokabular.Auswahl,
        "SchachtMaterialOptions" => SchachtMaterialVokabular.Auswahl,
        "SchachtFunktionOptions" => SchachtFunktionVokabular.Auswahl,
        "StatusOptions" => SiaKanalVokabular.Status.Auswahl,
        "SanierungsbedarfOptions" => SanierungsbedarfOptionen.Alle,
        _ => [""]
    };
}
