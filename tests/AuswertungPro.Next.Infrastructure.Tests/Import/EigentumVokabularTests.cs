using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Das Eigentum entscheidet in beiden Excel-Berichten ueber die Farbe. Die Vorlagen
/// pruefen auf exakte Gleichheit — seit 2026-08-31 tragen sie je eine Regel fuer den
/// amtlichen Begriff UND fuer die Kurzform: ="Abwasser Uri", ="AWU", ="Kanton Uri",
/// ="Kanton", ="Bund", ="Gemeinde", ="Privat".
///
/// Deshalb darf der eingelesene Wert stehen bleiben. Frueher uebersetzte das Vokabular
/// "Abwasser Uri" nach "AWU", weil nur die Kurzform gefaerbt wurde; das schrieb eine
/// Kundenangabe um. Gespeicherte Kurzformen aus Altprojekten bleiben gueltig.
/// </summary>
public sealed class EigentumVokabularTests
{
    [Theory]
    [InlineData("Abwasser Uri", "Abwasser Uri")]
    [InlineData("abwasser uri", "Abwasser Uri")]
    [InlineData("Abwasser Uri (AWU)", "Abwasser Uri")]
    [InlineData("AWU", "Abwasser Uri")]
    [InlineData("Kanton Uri", "Kanton Uri")]
    [InlineData("Kanton", "Kanton Uri")]
    [InlineData("Privat", "Privat")]
    [InlineData("privat", "Privat")]
    [InlineData("Gemeinde", "Gemeinde")]
    [InlineData("Bund", "Bund")]
    public void Bekannte_Schreibweisen_werden_zum_amtlichen_Begriff(string gelesen, string erwartet)
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

        // Und die Gegenrichtung: Jede Farbe der Vorlage muss erreichbar bleiben.
        // Die Kurzformen stehen nicht mehr zur Auswahl, sind aber weiterhin
        // lesbar — ihr normalisierter Begriff traegt dieselbe Farbe.
        foreach (var wert in gefaerbt)
            Assert.Contains(EigentumVokabular.Normalisieren(wert), EigentumVokabular.Auswahl);
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
