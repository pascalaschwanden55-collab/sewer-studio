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
/// habe". Die erste Antwort waren Klapp-Abschnitte; die hat er nach dem
/// Ansehen wieder verworfen. Geblieben ist der bessere Weg: Ein Klick im Blatt
/// zeigt rechts genau dieses eine Feld, hebt es beidseitig hervor und setzt
/// den Schreibfokus hinein.
/// </summary>
public sealed class DossierFieldSectionTests
{
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
            DossierPreviewTarget? betont = null;
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
                target => betont = target,
                (_, _) => { },
                _ => Brushes.Black,
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
            var seitenZiel = DossierTocChapterPageClickMapper.PageTarget("Kapitel");
            Assert.True(panel.Kennt(seitenZiel));
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
                Assert.Equal(seitenZiel, betont);
            }
            finally
            {
                fenster.Close();
            }
        });
    }

    [Fact]
    public void Neuaufbau_einer_Zeilenliste_entfernt_Adressen_geloeschter_Zeilen()
    {
        RunOnSta(() =>
        {
            var area = new DossierAreaSettings();
            var dossier = new DossierDefinition
            {
                Owners =
                [
                    new DossierOwnerRow { Name = "Erste Person" },
                    new DossierOwnerRow { Name = "Zweite Person" }
                ]
            };
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
                _ => Brushes.Black,
                _ => { },
                () => new Window());
            var feld = new DossierPreviewField(
                "Eigentuemer",
                "Eigentümer",
                DossierPreviewFieldKind.Rows,
                () => string.Empty,
                null);

            var bauen = typeof(DossierPreviewFieldPanel).GetMethod(
                "BaueZeilenEditor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var fuellen = typeof(DossierPreviewFieldPanel).GetMethod(
                "FuelleZeilenEditor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(bauen);
            Assert.NotNull(fuellen);

            var wirt = Assert.IsType<StackPanel>(bauen.Invoke(panel, [feld]));
            var zweiteZeile = DossierPreviewTarget.Row("Eigentuemer", 1);
            var zweiteNamenszelle = DossierPreviewTarget.RowCell(
                "Eigentuemer", 1, "Eigentuemer_Zelle");
            Assert.True(panel.Kennt(zweiteZeile));
            Assert.True(panel.Kennt(zweiteNamenszelle));

            dossier.Owners.RemoveAt(1);
            fuellen.Invoke(panel, [wirt, feld]);

            Assert.False(panel.Kennt(zweiteZeile));
            Assert.False(panel.Kennt(zweiteNamenszelle));
            Assert.True(panel.Kennt(DossierPreviewTarget.Row("Eigentuemer", 0)));
        });
    }

    [Fact]
    public void Plansprung_zeigt_die_vorhandenen_Foto_Dreh_und_Zuschneidewerkzeuge()
    {
        RunOnSta(() =>
        {
            var host = new StackPanel();
            var dossier = new DossierDefinition();
            var panel = new DossierPreviewFieldPanel(
                host,
                new DossierAreaSettings(),
                dossier,
                System.IO.Path.GetTempPath(),
                new DossierPreviewDocument([]),
                new PlanImageConverterStub(),
                new PlanImageAdjusterStub(),
                () => new Dictionary<string, string>(),
                () => { },
                _ => { },
                (_, _) => { },
                _ => Brushes.Black,
                _ => { },
                () => new Window());
            var field = new DossierPreviewField(
                "Uebersichtsplan",
                "Werkleitungsplan (JPG, PNG oder PDF)",
                DossierPreviewFieldKind.File,
                () => dossier.OverviewPlanPath,
                value => dossier.OverviewPlanPath = value);
            var page = new DossierPreviewPage(
                3,
                "Übersichtsplan Werkleitungen",
                new DossierPreviewGeometry(794, 1123, DossierPreviewEdges.Zero),
                [],
                ["Uebersichtsplan"]);
            var target = DossierPreviewTarget.Field("Uebersichtsplan");

            panel.Baue(page, [field]);

            Assert.True(panel.SpringeZu(target));
            var buttons = Nachfahren(host)
                .OfType<Button>()
                .Select(button => button.Content?.ToString())
                .ToList();
            Assert.Contains("JPG / Plan wählen…", buttons);
            Assert.Contains("⟲", buttons);
            Assert.Contains("⟳", buttons);
            Assert.Contains("Zuschneiden…", buttons);
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

    // ── Nur das angeklickte Feld zeigen ───────────────────────────────────

    [Fact]
    public void Im_Fokus_bleibt_nur_die_angeklickte_Stelle_sichtbar()
    {
        // Pascals Modell: Klick in der Vorschau -> rechts geht genau dieses
        // Feld auf, alle anderen werden ausgeblendet.
        RunOnSta(() =>
        {
            var gesucht = new Border();
            var daneben = new Border();
            var andererAbschnitt = new Border();

            var abschnittA = new StackPanel { Children = { gesucht, daneben } };
            var abschnittB = new StackPanel { Children = { andererAbschnitt } };
            var wurzel = new StackPanel { Children = { abschnittA, abschnittB } };

            DossierFieldFocus.ZeigeNur([abschnittA, abschnittB, gesucht, daneben, andererAbschnitt], gesucht);

            Assert.Equal(Visibility.Visible, gesucht.Visibility);
            Assert.Equal(Visibility.Visible, abschnittA.Visibility);
            Assert.Equal(Visibility.Collapsed, daneben.Visibility);
            Assert.Equal(Visibility.Collapsed, abschnittB.Visibility);
            Assert.Equal(Visibility.Collapsed, andererAbschnitt.Visibility);
            Assert.NotNull(wurzel);
        });
    }

    [Fact]
    public void Was_in_der_gesuchten_Stelle_steckt_bleibt_sichtbar()
    {
        // Eine Karte enthaelt Beschriftung, Eingabe und Werkzeuge. Sie duerfen
        // nicht mit ausgeblendet werden, nur weil sie eigene Stellen sind.
        RunOnSta(() =>
        {
            var innen = new Border();
            var karte = new Border { Child = new StackPanel { Children = { innen } } };
            var wurzel = new StackPanel { Children = { karte } };

            DossierFieldFocus.ZeigeNur([karte, innen], karte);

            Assert.Equal(Visibility.Visible, karte.Visibility);
            Assert.Equal(Visibility.Visible, innen.Visibility);
            Assert.NotNull(wurzel);
        });
    }

    [Fact]
    public void Alles_zeigen_stellt_jede_Stelle_wieder_her()
    {
        // Der Rueckweg ist Pflicht: Ein leeres Feld hat in der Vorschau keinen
        // Text zum Anklicken — ohne ihn waere es unerreichbar.
        RunOnSta(() =>
        {
            var eine = new Border();
            var zwei = new Border();
            var wurzel = new StackPanel { Children = { eine, zwei } };

            DossierFieldFocus.ZeigeNur([eine, zwei], eine);
            DossierFieldFocus.ZeigeAlles([eine, zwei]);

            Assert.Equal(Visibility.Visible, eine.Visibility);
            Assert.Equal(Visibility.Visible, zwei.Visibility);
            Assert.NotNull(wurzel);
        });
    }
}
