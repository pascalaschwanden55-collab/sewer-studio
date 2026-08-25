using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.UI.Views.Windows;

using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Wie sich die Eingabeseite der Dossier-Vorschau verhält.
///
/// Pascals Befund war: „es ist eher verwirrend bis ich die Felder gefunden
/// habe". Die Ursache war nicht die Menge der Felder, sondern dass alle
/// Abschnitte gleichzeitig offen standen und ein Sprung aus dem Blatt nur den
/// Schreibfokus setzte — sichtbar geblinkt hat allein das Blatt. Man landete
/// irgendwo und musste erst suchen, wo.
/// </summary>
public sealed class DossierFieldSectionTests
{
    [Fact]
    public void Wer_einen_Abschnitt_aufklappt_klappt_die_uebrigen_zu()
    {
        RunOnSta(() =>
        {
            var ordnung = new DossierFieldSectionAccordion();
            var erster = new Expander { IsExpanded = true };
            var zweiter = new Expander();
            var dritter = new Expander();

            ordnung.Merke(erster);
            ordnung.Merke(zweiter);
            ordnung.Merke(dritter);

            zweiter.IsExpanded = true;

            Assert.False(erster.IsExpanded);
            Assert.True(zweiter.IsExpanded);
            Assert.False(dritter.IsExpanded);
        });
    }

    [Fact]
    public void Nach_dem_Aufbau_steht_genau_ein_Abschnitt_offen()
    {
        // Die Verzeichnisseite baute drei Abschnitte als „wichtig" auf. Alle
        // drei standen offen, und die gesuchte Eingabe lag irgendwo dazwischen.
        RunOnSta(() =>
        {
            var ordnung = new DossierFieldSectionAccordion();
            var angaben = new Expander { IsExpanded = true };
            var verzeichnis = new Expander { IsExpanded = true };
            var beilagen = new Expander { IsExpanded = true };

            ordnung.Merke(angaben);
            ordnung.Merke(verzeichnis);
            ordnung.Merke(beilagen);

            ordnung.OeffneNurDenErsten();

            Assert.True(angaben.IsExpanded);
            Assert.False(verzeichnis.IsExpanded);
            Assert.False(beilagen.IsExpanded);
        });
    }

    [Fact]
    public void Sind_alle_zu_oeffnet_der_erste()
    {
        RunOnSta(() =>
        {
            var ordnung = new DossierFieldSectionAccordion();
            var erster = new Expander();
            var zweiter = new Expander();

            ordnung.Merke(erster);
            ordnung.Merke(zweiter);
            ordnung.OeffneNurDenErsten();

            Assert.True(erster.IsExpanded);
            Assert.False(zweiter.IsExpanded);
        });
    }

    [Fact]
    public void Eine_neue_Seite_vergisst_die_Abschnitte_der_alten()
    {
        // Ohne das Leeren wuerde ein Abschnitt der vorigen Seite weiter
        // mitgeordnet — und ein Klick auf der neuen Seite ihn zuklappen wollen.
        RunOnSta(() =>
        {
            var ordnung = new DossierFieldSectionAccordion();
            var alt = new Expander { IsExpanded = true };
            ordnung.Merke(alt);

            ordnung.Leere();

            var neu = new Expander();
            ordnung.Merke(neu);
            neu.IsExpanded = true;

            Assert.True(alt.IsExpanded);
            Assert.True(neu.IsExpanded);
        });
    }

    [Fact]
    public void Der_Sprung_laesst_die_Stelle_aufblinken_und_gibt_die_Farbe_zurueck()
    {
        // Gegenstueck zum Blinken im Blatt: Nach einem Sprung muss man auf
        // BEIDEN Seiten sehen, wo man gelandet ist.
        RunOnSta(() =>
        {
            var vorher = new SolidColorBrush(Colors.White);
            var karte = new Border { Background = vorher };
            var feld = new TextBox();
            karte.Child = feld;

            DossierFieldHighlight.LasseAufblinken(feld);

            Assert.NotSame(vorher, karte.Background);
            var jetzt = Assert.IsType<SolidColorBrush>(karte.Background);
            Assert.True(jetzt.Color.R > jetzt.Color.B, "Die Stelle blinkt nicht rot.");

            PumpDispatcherFor(TimeSpan.FromMilliseconds(1400));

            Assert.Same(vorher, karte.Background);
        });
    }

    [Fact]
    public void Ohne_einfaerbbare_Flaeche_passiert_nichts()
    {
        RunOnSta(() => DossierFieldHighlight.LasseAufblinken(new TextBox()));
    }

    [Fact]
    public void Die_Formatwerkzeuge_starten_unsichtbar()
    {
        // Vorher stand unter jedem Textfeld dauerhaft eine Leiste; das machte
        // jede Karte doppelt so hoch und die Seite unuebersichtlich.
        RunOnSta(() =>
        {
            var karte = new StackPanel();
            var werkzeuge = new StackPanel();

            DossierFieldHighlight.ZeigeWerkzeugeNurAmAktivenFeld(karte, werkzeuge);

            Assert.Equal(Visibility.Collapsed, werkzeuge.Visibility);
        });
    }

    [Theory]
    [InlineData(true, Visibility.Visible)]
    [InlineData(false, Visibility.Collapsed)]
    public void Sichtbar_ist_die_Leiste_genau_solange_in_der_Karte_gearbeitet_wird(
        bool fokusInDerKarte, Visibility erwartet)
    {
        // Massgeblich ist der Fokus in der GANZEN Karte, nicht nur im Textfeld
        // — sonst verschwaende die Leiste in dem Moment, in dem man einen ihrer
        // Knoepfe anklickt.
        Assert.Equal(erwartet, DossierFieldHighlight.SichtbarkeitFuer(fokusInDerKarte));
    }

    private static void PumpDispatcherFor(TimeSpan duration)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action test)
    {
        ExceptionDispatchInfo? fehler = null;

        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                fehler = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        fehler?.Throw();
    }
}
