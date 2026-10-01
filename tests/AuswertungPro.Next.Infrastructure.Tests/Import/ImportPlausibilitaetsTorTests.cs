using System;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Das Stopptor. Es muss den Andermatt-Widerspruch erkennen ("5 Datenbanken gelesen,
/// 0 Haltungen"), aber einen ehrlich leeren Projektstand durchlassen.
/// </summary>
public sealed class ImportPlausibilitaetsTorTests
{
    private static QuellenwahlErgebnis Quellen(params (string Pfad, QuellenBefund Befund)[] eintraege)
        => new(
            eintraege
                .Where(e => e.Befund.Tauglichkeit != QuellenTauglichkeit.Untauglich)
                .Select(e => new QuellenVersuch(e.Pfad, e.Befund))
                .FirstOrDefault(),
            eintraege.Select(e => new QuellenVersuch(e.Pfad, e.Befund)).ToList());

    [Fact]
    public void OhneQuellenprotokoll_UrteiltDasTorNicht()
    {
        // Andere Importwege (PDF, XTF, IBAK) liefern kein Protokoll und bleiben unberuehrt.
        Assert.Equal(PlausibilitaetsStufe.Gruen, ImportPlausibilitaetsTor.Beurteile(null, 0).Stufe);
        Assert.Equal(
            PlausibilitaetsStufe.Gruen,
            ImportPlausibilitaetsTor.Beurteile(QuellenwahlErgebnis.Leer, 0).Stufe);
    }

    [Fact]
    public void KeineEinzigeLesbareQuelle_IstHarterAbbruch()
    {
        var urteil = ImportPlausibilitaetsTor.Beurteile(
            Quellen(
                (@"C:\p\DB\a.db3", QuellenBefund.Untauglich("nicht lesbar")),
                (@"C:\p\DB\b_Meta.db3", QuellenBefund.Untauglich("keine Haltungstabelle"))),
            bearbeiteteHaltungen: 0);

        Assert.Equal(PlausibilitaetsStufe.HartAbbruch, urteil.Stufe);
        Assert.False(urteil.BrauchtRueckfrage);
        Assert.Contains(PlausibilitaetsUrteil.AbbruchHinweis, urteil.Begruendung, StringComparison.Ordinal);
    }

    [Fact]
    public void AlleQuellenLesbarAberLeer_IstGruen()
    {
        // Ein frisches, noch leeres WinCan-Projekt ist ein gueltiger Zustand und
        // darf nicht wie ein Defekt behandelt werden.
        var urteil = ImportPlausibilitaetsTor.Beurteile(
            Quellen((@"C:\p\DB\a.db3", QuellenBefund.Leer("keine Haltungen"))),
            bearbeiteteHaltungen: 0);

        Assert.Equal(PlausibilitaetsStufe.Gruen, urteil.Stufe);
    }

    [Fact]
    public void QuellenMitDatenAberNullUebernommen_IstRueckfrage()
    {
        // Genau der Andermatt-Fall vom 2026-08-21.
        var urteil = ImportPlausibilitaetsTor.Beurteile(
            Quellen(
                (@"C:\p\DB\projekt.db3", QuellenBefund.Tauglich(15, "15 Haltungen")),
                (@"C:\p\DB\projekt_Meta.db3", QuellenBefund.Untauglich("keine Haltungstabelle"))),
            bearbeiteteHaltungen: 0);

        Assert.Equal(PlausibilitaetsStufe.Rueckfrage, urteil.Stufe);
        Assert.True(urteil.BrauchtRueckfrage);
        Assert.Contains("keine einzige Haltung", urteil.Begruendung, StringComparison.Ordinal);
        Assert.Contains(urteil.Quellenzeilen, z => z.Contains("projekt_Meta.db3", StringComparison.Ordinal));
    }

    [Fact]
    public void WenigerUebernommenAlsErwartet_IstRueckfrage()
    {
        var urteil = ImportPlausibilitaetsTor.Beurteile(
            Quellen(
                (@"C:\p1\DB\a.db3", QuellenBefund.Tauglich(8, "8 Haltungen")),
                (@"C:\p2\DB\b.db3", QuellenBefund.Tauglich(7, "7 Haltungen"))),
            bearbeiteteHaltungen: 8);

        Assert.Equal(PlausibilitaetsStufe.Rueckfrage, urteil.Stufe);
        Assert.Contains("7 fehlen", urteil.Begruendung, StringComparison.Ordinal);
    }

    [Fact]
    public void AllesUebernommen_IstGruen()
    {
        var urteil = ImportPlausibilitaetsTor.Beurteile(
            Quellen((@"C:\p\DB\a.db3", QuellenBefund.Tauglich(15, "15 Haltungen"))),
            bearbeiteteHaltungen: 15);

        Assert.Equal(PlausibilitaetsStufe.Gruen, urteil.Stufe);
    }

    [Fact]
    public void MehrUebernommenAlsErwartet_IstGruen()
    {
        // Ein vorhandener Bestand kann zusaetzliche Haltungen beisteuern.
        var urteil = ImportPlausibilitaetsTor.Beurteile(
            Quellen((@"C:\p\DB\a.db3", QuellenBefund.Tauglich(3, "3 Haltungen"))),
            bearbeiteteHaltungen: 5);

        Assert.Equal(PlausibilitaetsStufe.Gruen, urteil.Stufe);
    }

    [Fact]
    public void Zustimmung_GiltNurBeiUnveraenderterLage()
    {
        var lage = Quellen((@"C:\p\DB\a.db3", QuellenBefund.Tauglich(15, "15 Haltungen")));
        var vorschau = ImportPlausibilitaetsTor.Beurteile(lage, bearbeiteteHaltungen: 0);

        // Echtlauf findet dieselbe Lage -> nicht erneut fragen.
        var echtlaufGleich = ImportPlausibilitaetsTor.Beurteile(lage, bearbeiteteHaltungen: 0);
        Assert.True(ImportPlausibilitaetsTor.ZustimmungGiltNoch(vorschau.Fingerabdruck, echtlaufGleich));

        // Echtlauf findet etwas anderes -> erneut fragen.
        var echtlaufAnders = ImportPlausibilitaetsTor.Beurteile(lage, bearbeiteteHaltungen: 4);
        Assert.False(ImportPlausibilitaetsTor.ZustimmungGiltNoch(vorschau.Fingerabdruck, echtlaufAnders));

        var andereQuellen = ImportPlausibilitaetsTor.Beurteile(
            Quellen((@"C:\p\DB\anders.db3", QuellenBefund.Tauglich(15, "15 Haltungen"))),
            bearbeiteteHaltungen: 0);
        Assert.False(ImportPlausibilitaetsTor.ZustimmungGiltNoch(vorschau.Fingerabdruck, andereQuellen));
    }

    [Fact]
    public void OhneZustimmung_GiltNichts()
    {
        var urteil = ImportPlausibilitaetsTor.Beurteile(
            Quellen((@"C:\p\DB\a.db3", QuellenBefund.Tauglich(1, "1 Haltung"))),
            bearbeiteteHaltungen: 0);

        Assert.False(ImportPlausibilitaetsTor.ZustimmungGiltNoch(null, urteil));
        Assert.False(ImportPlausibilitaetsTor.ZustimmungGiltNoch("", urteil));
    }

    [Fact]
    public void Fingerabdruck_HaengtNichtAnDerPruefreihenfolge()
    {
        var a = new QuellenVersuch(@"C:\p\DB\a.db3", QuellenBefund.Tauglich(3, "3"));
        var b = new QuellenVersuch(@"C:\p\DB\b.db3", QuellenBefund.Tauglich(4, "4"));

        var vorwaerts = ImportPlausibilitaetsTor.Beurteile(new QuellenwahlErgebnis(a, new[] { a, b }), 7);
        var rueckwaerts = ImportPlausibilitaetsTor.Beurteile(new QuellenwahlErgebnis(a, new[] { b, a }), 7);

        Assert.Equal(vorwaerts.Fingerabdruck, rueckwaerts.Fingerabdruck);
    }
}
