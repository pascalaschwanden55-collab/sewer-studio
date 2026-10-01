using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Schlusswelle (Item 7): Ein innerer <c>Erfasse</c>-Bereich fuer einen
/// dem aeusseren Bereich VOELLIG FREMDEN Datensatz gehoert nicht zum selben Schritt. Vorher zog
/// <see cref="DatenaenderungsVerlauf.Erfasse{T}"/> (ueber <c>Oeffne</c>) jede waehrend eines
/// offenen aeusseren Bereichs geoeffnete Erfassung bedingungslos in denselben Schritt - auch fuer
/// ein voellig unabhaengiges zweites Objekt. Ein Rueckgaengig haette dann zwei unabhaengige
/// Aenderungen gemeinsam zurueckgenommen. Die Regel bleibt unveraendert fuer DENSELBEN Datensatz
/// (abhaengiges Feld, Auswahlfeld in einer offenen Zelle - siehe <c>DatenaenderungsVerlaufTests</c>).
/// </summary>
public sealed class DatenaenderungsVerlaufFremderDatensatzTests
{
    private static readonly DatenaenderungsBereich H = DatenaenderungsBereich.Haltungen;

    private static (DatenaenderungsVerlauf Verlauf, Project Projekt, HaltungRecord A, HaltungRecord B) Aufbau()
    {
        var projekt = new Project();
        var a = new HaltungRecord();
        a.Fields[FieldKeys.HoldingName] = "10001-10002";
        var b = new HaltungRecord();
        b.Fields[FieldKeys.HoldingName] = "20001-20002";
        projekt.Data.Add(a);
        projekt.Data.Add(b);
        var verlauf = new DatenaenderungsVerlauf();
        verlauf.Binde(projekt);
        return (verlauf, projekt, a, b);
    }

    /// <summary>Ein waehrend eines offenen Bereichs fuer Haltung A geoeffneter innerer Bereich fuer
    /// die voellig unabhaengige Haltung B wird ignoriert: der Schreibvorgang selbst laeuft normal
    /// (B traegt den neuen Wert), aber Rueckgaengig nimmt NUR die Aenderung an A zurueck.</summary>
    [Fact]
    public void Ein_innerer_bereich_fuer_einen_fremden_datensatz_wird_nicht_in_den_aeusseren_schritt_gezogen()
    {
        var (verlauf, _, a, b) = Aufbau();

        using (verlauf.Erfasse(a, "Rohrmaterial"))
        {
            a.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, true);

            // Waehrend der aeussere Bereich (A) noch offen ist, oeffnet und schliesst ein
            // voellig unabhaengiger Schreibvorgang an B einen eigenen Bereich.
            using (verlauf.Erfasse(b, "Rohrmaterial"))
                b.SetFieldValue("Rohrmaterial", "Steinzeug", FieldSource.Manual, true);
        }

        // Der Schreibvorgang an B ist real passiert - der Verlauf ignoriert ihn nur.
        Assert.Equal("Steinzeug", b.GetFieldValue("Rohrmaterial"));

        Assert.True(verlauf.KannRueckgaengig(H));
        Assert.Equal("Rohrmaterial 10001-10002", verlauf.RueckgaengigBeschreibung(H));

        var ergebnis = verlauf.Rueckgaengig(H);

        Assert.True(ergebnis.Angewendet);
        Assert.Equal("", a.GetFieldValue("Rohrmaterial"));
        // B bleibt unveraendert - seine Aenderung war nie Teil dieses Schritts.
        Assert.Equal("Steinzeug", b.GetFieldValue("Rohrmaterial"));
        // Kein zweiter, unabhaengiger Schritt fuer B wurde angelegt.
        Assert.False(verlauf.KannRueckgaengig(H));
    }

    /// <summary>Oeffnet der aeussere Bereich direkt fuer MEHRERE Datensaetze (z. B. «Spalte
    /// leeren» ueber <c>ErfasseMehrere</c>), gehoeren sie alle von Anfang an zum selben Schritt -
    /// das ist keine "fremde" Nachtraeglichkeit, sondern dieselbe Benutzeraktion.</summary>
    [Fact]
    public void ErfasseMehrere_fasst_mehrere_datensaetze_weiterhin_in_einem_schritt_zusammen()
    {
        var (verlauf, _, a, b) = Aufbau();

        using (verlauf.ErfasseMehrere([a, b], "Spalte leeren"))
        {
            a.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, true);
            b.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, true);
        }

        Assert.True(verlauf.KannRueckgaengig(H));
        var ergebnis = verlauf.Rueckgaengig(H);
        Assert.True(ergebnis.Angewendet);
        Assert.Equal("", a.GetFieldValue("Rohrmaterial"));
        Assert.Equal("", b.GetFieldValue("Rohrmaterial"));
    }

    /// <summary>Ein zweiter Schreibvorgang fuer DENSELBEN Datensatz (nur ein anderes Feld) bleibt
    /// weiterhin Teil des aeusseren Schritts - die Fremd-Datensatz-Regel betrifft nur ein
    /// tatsaechlich anderes Objekt.</summary>
    [Fact]
    public void Ein_innerer_bereich_fuer_denselben_datensatz_bleibt_teil_des_aeusseren_schritts()
    {
        var (verlauf, _, a, _) = Aufbau();

        using (verlauf.Erfasse(a))
        {
            a.SetFieldValue("DN_mm", "300", FieldSource.Manual, true);
            using (verlauf.Erfasse(a, "Rohrmaterial"))
                a.SetFieldValue("Rohrmaterial", "PVC", FieldSource.Manual, true);
        }

        var ergebnis = verlauf.Rueckgaengig(H);
        Assert.True(ergebnis.Angewendet);
        Assert.Equal("", a.GetFieldValue("DN_mm"));
        Assert.Equal("", a.GetFieldValue("Rohrmaterial"));
    }
}
