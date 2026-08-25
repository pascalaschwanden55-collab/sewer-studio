using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Models.Dossiers;
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

    [Fact]
    public void Formatleiste_bleibt_beim_Klick_auf_ihr_Werkzeug_sichtbar()
    {
        RunOnSta(() =>
        {
            var editor = new TextBox();
            var knopf = new Button { Content = "Fett" };
            var werkzeuge = new StackPanel { Children = { knopf } };
            var karte = new StackPanel { Children = { editor, werkzeuge } };
            var ausserhalb = new TextBox();
            var fenster = new Window
            {
                Content = new StackPanel { Children = { karte, ausserhalb } },
                Width = 300,
                Height = 180,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            DossierFieldHighlight.ZeigeWerkzeugeNurAmAktivenFeld(karte, werkzeuge);

            try
            {
                fenster.Show();
                Keyboard.Focus(editor);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(80));
                Assert.Equal(Visibility.Visible, werkzeuge.Visibility);

                Keyboard.Focus(knopf);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(80));
                Assert.Equal(Visibility.Visible, werkzeuge.Visibility);

                Keyboard.Focus(ausserhalb);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(80));
                Assert.Equal(Visibility.Collapsed, werkzeuge.Visibility);
            }
            finally
            {
                fenster.Close();
            }
        });
    }

    [Fact]
    public void Seitenzahl_der_Verzeichniszeile_zeigt_nicht_die_Formatleiste_des_Titels()
    {
        // Titel und Seitenzahl stehen in derselben Karte, sind aber zwei
        // verschiedene Eingaben. Beim Schreiben der Zahl darf die Leiste des
        // Titels nicht sichtbar bleiben und versehentlich dessen Text aendern.
        RunOnSta(() =>
        {
            var area = new DossierAreaSettings();
            var dossier = new DossierDefinition();
            var project = new Project();
            var request = new DossierExportRequest(
                project,
                string.Empty,
                area,
                dossier,
                DossierSnapshotBuilder.Build(dossier, project, null),
                string.Empty);
            var panel = new DossierPreviewFieldPanel(
                new StackPanel(),
                area,
                dossier,
                System.IO.Path.GetTempPath(),
                new DossierPreviewDocument([]),
                new PlanImageConverterStub(),
                new PlanImageAdjusterStub(),
                () => new Dictionary<string, string>(),
                () => { },
                _ => { },
                (_, _) => { },
                _ => new object(),
                _ => { },
                () => new Window());

            var methode = typeof(DossierPreviewFieldPanel).GetMethod(
                "BaueFesteTexte",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(methode);
            var wurzel = Assert.IsAssignableFrom<UIElement>(methode.Invoke(
                panel,
                new object[] { new[] { "Kapitel" }, new[] { "Kapitel" } }));

            var titel = Assert.Single(Nachfahren(wurzel).OfType<RichTextBox>());
            var seitenzahl = Assert.Single(Nachfahren(wurzel).OfType<TextBox>());
            var fett = Assert.Single(Nachfahren(wurzel).OfType<Button>()
                .Where(knopf => string.Equals(
                    knopf.Content as string, "Fett", StringComparison.Ordinal)));
            var zeile = Assert.IsType<WrapPanel>(LogicalTreeHelper.GetParent(fett));
            var werkzeuge = Assert.IsType<StackPanel>(LogicalTreeHelper.GetParent(zeile));
            var fenster = new Window
            {
                Content = wurzel,
                Width = 420,
                Height = 600,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                fenster.Show();
                Keyboard.Focus(titel);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(80));
                Assert.Equal(Visibility.Visible, werkzeuge.Visibility);

                Keyboard.Focus(seitenzahl);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(80));
                Assert.Equal(Visibility.Collapsed, werkzeuge.Visibility);
            }
            finally
            {
                fenster.Close();
            }
        });
    }

    private static IEnumerable<DependencyObject> Nachfahren(DependencyObject wurzel)
    {
        foreach (var kind in LogicalTreeHelper.GetChildren(wurzel)
                     .OfType<DependencyObject>())
        {
            yield return kind;
            foreach (var nachfahr in Nachfahren(kind))
                yield return nachfahr;
        }
    }

    private sealed class PlanImageConverterStub : IPlanImageConverter
    {
        public bool NeedsConversion(string? path) => false;

        public Task<PlanImageResult> ConvertAsync(
            string sourcePath,
            string targetFolder,
            CancellationToken ct = default)
            => Task.FromResult(PlanImageResult.Failed("Im Test nicht verwendet."));
    }

    private sealed class PlanImageAdjusterStub : IPlanImageAdjuster
    {
        public PlanImageResult Rotate(string? imagePath, string targetFolder, int degrees)
            => PlanImageResult.Failed("Im Test nicht verwendet.");

        public PlanImageResult Crop(
            string? imagePath,
            string targetFolder,
            int x,
            int y,
            int width,
            int height)
            => PlanImageResult.Failed("Im Test nicht verwendet.");
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

    [Fact]
    public void Der_Sprung_setzt_den_Schreibfokus_auch_in_einen_zugeklappten_Abschnitt()
    {
        // Pascals Anforderung: Ein Klick ins Blatt soll die Stelle rechts
        // AKTIVIEREN, nicht nur hinscrollen. Der Abschnitt ist dabei meist
        // zugeklappt — ein Fokus auf ein noch nicht dargestelltes Feld
        // verpufft, wenn man ihn zu frueh setzt.
        RunOnSta(() =>
        {
            var feld = new TextBox();
            var karte = new Border { Child = feld };
            var abschnitt = new Expander { Content = karte, IsExpanded = false };

            var fenster = new Window
            {
                Content = new StackPanel { Children = { abschnitt } },
                Width = 300,
                Height = 200,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                fenster.Show();

                abschnitt.IsExpanded = true;
                DossierFieldHighlight.AktiviereEingabe(karte);
                PumpDispatcherFor(TimeSpan.FromMilliseconds(300));

                Assert.True(feld.IsKeyboardFocused, "Das Feld hat den Schreibfokus nicht.");
            }
            finally
            {
                fenster.Close();
            }
        });
    }

    [Fact]
    public void Ohne_Eingabe_in_der_Karte_passiert_nichts()
    {
        RunOnSta(() => DossierFieldHighlight.AktiviereEingabe(
            new Border { Child = new TextBlock { Text = "Nur Text" } }));
    }
}
