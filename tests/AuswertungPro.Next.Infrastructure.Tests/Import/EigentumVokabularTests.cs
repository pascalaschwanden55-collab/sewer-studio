using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Das Eigentum entscheidet in beiden Excel-Berichten ueber die Farbe. Die Vorlagen
/// pruefen auf exakte Gleichheit — gemessen an den ausgelieferten Dateien:
/// Haltungen.xlsx Spalte O, Schaechte.xlsx Spalte J, jeweils
/// ="AWU", ="Kanton", ="Bund", ="Gemeinde", ="Privat".
///
/// Die XTF schreibt dagegen "Abwasser Uri" und "Kanton Uri" (Zone 1.17: Privat 204x,
/// Abwasser Uri 68x, Kanton Uri 17x an Normschacht). Ohne Uebersetzung waere die Spalte
/// zwar gefuellt, aber zwei Drittel der Zeilen blieben farblos.
/// </summary>
public sealed class EigentumVokabularTests
{
    [Theory]
    [InlineData("Abwasser Uri", "AWU")]
    [InlineData("abwasser uri", "AWU")]
    [InlineData("Abwasser Uri (AWU)", "AWU")]
    [InlineData("AWU", "AWU")]
    [InlineData("Kanton Uri", "Kanton")]
    [InlineData("Kanton", "Kanton")]
    [InlineData("Privat", "Privat")]
    [InlineData("privat", "Privat")]
    [InlineData("Gemeinde", "Gemeinde")]
    [InlineData("Bund", "Bund")]
    public void Bekannte_Schreibweisen_werden_zum_Excel_Wert(string gelesen, string erwartet)
    {
        Assert.Equal(erwartet, EigentumVokabular.Normalisieren(gelesen));
    }

    [Fact]
    public void Jeder_Begriff_der_App_faerbt_die_Excel_Zelle()
    {
        // Der eigentliche Punkt: Was das Vokabular ausgibt, muss die Vorlage faerben.
        // Geprueft wird gegen ExcelReportStyle.Eigentuemer und nicht gegen eine hier
        // abgeschriebene Liste — dieser Vertrag ist seinerseits an die ausgelieferte
        // Datei gebunden (Kostenblock_enthaelt_jeden_aufgefuehrten_Eigentuemer liest
        // die SUMIF-Formeln aus Haltungen.xlsx). So faellt eine spaetere Aenderung an
        // der Vorlage hier auf, statt still eine farblose Spalte zu erzeugen.
        var gefaerbt = Next.Infrastructure.Export.Excel.ExcelReportStyle.Eigentuemer
            .Select(r => r.Wert)
            .ToArray();

        foreach (var wert in EigentumVokabular.Auswahl.Where(w => w.Length > 0))
            Assert.Contains(wert, gefaerbt);

        // Und die Gegenrichtung: Fuer jede Farbe der Vorlage muss es einen Begriff
        // geben, sonst bleibt eine Farbe unerreichbar.
        foreach (var wert in gefaerbt)
            Assert.Contains(wert, EigentumVokabular.Auswahl);
    }

    [Fact]
    public void Ein_unbekannter_Eigentuemer_bleibt_lesbar_stehen()
    {
        // Eine Korporation oder Genossenschaft ist eine echte Angabe. Sie zu loeschen
        // waere schlimmer, als sie ohne Farbe stehen zu lassen.
        Assert.Equal("Korporation Uri", EigentumVokabular.Normalisieren("Korporation Uri"));
        Assert.Equal("", EigentumVokabular.Normalisieren(null));
        Assert.Equal("", EigentumVokabular.Normalisieren("   "));
    }

    [Fact]
    public void Der_AWU_Filter_erkennt_den_normalisierten_Wert_weiterhin()
    {
        // OwnershipAwuFilter entscheidet, welche Schaechte ins NPK-135-Leistungs-
        // verzeichnis kommen. Er darf durch die Uebersetzung nichts verlieren.
        Assert.True(Next.Infrastructure.Costs.OwnershipAwuFilter.IsAwu(
            EigentumVokabular.Normalisieren("Abwasser Uri")));
        Assert.True(Next.Infrastructure.Costs.OwnershipAwuFilter.IsAwu(
            EigentumVokabular.Normalisieren("AWU")));
        Assert.False(Next.Infrastructure.Costs.OwnershipAwuFilter.IsAwu(
            EigentumVokabular.Normalisieren("Privat")));
    }
}
