using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 2): Der Ansichtswechsel der Haltungsseite an echten
/// WPF-Elementen — aber ohne <c>App</c> und ohne Fenster. Geprueft wird, was der Umschalter
/// tatsaechlich setzt: Sichtbarkeiten, Haken, gesperrte Menuepunkte und die gespeicherte Wahl.
///
/// Gespeichert wird ueber einen Rueckruf, nicht ueber <c>AppSettings.Save</c> — sonst schriebe
/// dieser Test die echte settings.json des Benutzers.
/// </summary>
public sealed class DataPageAnsichtUmschalterTests
{
    [Fact]
    public void Standard_ist_die_Aufklapp_Liste_ohne_Spaltenchips_und_ohne_Eingabefelder()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();

            p.Umschalter.WendeAn();

            Assert.Equal(Visibility.Visible, p.Liste.Visibility);
            Assert.Equal(Visibility.Collapsed, p.Tabelle.Visibility);
            Assert.Equal(Visibility.Collapsed, p.AlteAnsicht.Visibility);
            Assert.Equal(Visibility.Collapsed, p.Spaltenchips.Visibility);
            // Die Uebersicht rechts bleibt, die Eingabefelder-Schublade verschwindet.
            Assert.Equal((true, false), p.Arbeitsflaeche);
            // Die Suchpille gehoert zu beiden Nova-Ansichten.
            Assert.Equal(Visibility.Visible, p.NovaSuche.Visibility);
            Assert.Equal(Visibility.Collapsed, p.AlteSuche.Visibility);

            Assert.True(p.ListeSchalter.IsChecked);
            Assert.False(p.TabelleSchalter.IsChecked);
            Assert.False(p.AbdockenSchalter.IsEnabled);
            Assert.Equal(DataPageAnsichtUmschalter.AbdockenNurTabelle, p.AbdockenSchalter.ToolTip);
        });
    }

    [Fact]
    public void Ein_Klick_auf_Tabelle_speichert_die_Wahl_und_wendet_sie_sofort_an()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();
            p.Umschalter.WendeAn();

            // Genau der Weg des Menuepunkts: der Schluessel steht im Tag.
            p.Umschalter.Waehle(p.TabelleSchalter);

            Assert.Equal("tabelle", p.Settings.HaltungenAnsicht);
            Assert.Equal(1, p.Gespeichert);
            Assert.Equal(Visibility.Visible, p.Tabelle.Visibility);
            Assert.Equal(Visibility.Collapsed, p.Liste.Visibility);
            Assert.Equal(Visibility.Visible, p.Spaltenchips.Visibility);
            Assert.Equal((true, true), p.Arbeitsflaeche);
            Assert.True(p.TabelleSchalter.IsChecked);
            Assert.False(p.ListeSchalter.IsChecked);
            Assert.True(p.AbdockenSchalter.IsEnabled);
            Assert.Equal(DataPageAnsichtUmschalter.AbdockenTabelle, p.AbdockenSchalter.ToolTip);
        });
    }

    [Fact]
    public void Dieselbe_Ansicht_nochmals_zu_waehlen_speichert_nicht_erneut()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();

            p.Umschalter.Waehle("liste");
            p.Umschalter.Waehle("liste");

            Assert.Equal(0, p.Gespeichert);
            Assert.True(p.ListeSchalter.IsChecked);
        });
    }

    [Fact]
    public void Beim_Wechsel_bleibt_die_gewaehlte_Haltung_erhalten()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();
            p.Liste.SelectedItem = p.Datensaetze[1];

            p.Umschalter.Waehle("tabelle");
            p.Umschalter.Waehle("liste");

            Assert.Same(p.Datensaetze[1], p.Liste.SelectedItem);
            Assert.Equal(Visibility.Visible, p.Liste.Visibility);
        });
    }

    [Fact]
    public void Die_alte_Haltungsansicht_blendet_beide_Nova_Ansichten_aus_und_merkt_sich_die_Wahl()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();
            p.Umschalter.Waehle("tabelle");

            p.AlteAnsichtSchalter.IsChecked = true;
            p.Umschalter.WendeAn();

            Assert.Equal(Visibility.Visible, p.AlteAnsicht.Visibility);
            Assert.Equal(Visibility.Collapsed, p.Liste.Visibility);
            Assert.Equal(Visibility.Collapsed, p.Tabelle.Visibility);
            Assert.Equal(Visibility.Collapsed, p.Spaltenchips.Visibility);
            Assert.Equal((false, false), p.Arbeitsflaeche);
            Assert.Equal(Visibility.Visible, p.AlteSuche.Visibility);
            Assert.Equal(Visibility.Collapsed, p.NovaSuche.Visibility);
            // Die gespeicherte Nova-Ansicht bleibt sichtbar angehakt und gilt beim Zurueck.
            Assert.True(p.TabelleSchalter.IsChecked);

            p.AlteAnsichtSchalter.IsChecked = false;
            p.Umschalter.WendeAn();
            Assert.Equal(Visibility.Visible, p.Tabelle.Visibility);
        });
    }

    [Fact]
    public void Solange_die_Tabelle_abgedockt_ist_bleibt_der_Wechsel_gesperrt()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();
            p.Umschalter.Waehle("tabelle");

            // Genau das tut GridDockingController.ApplyUndockedState beim Abdocken.
            p.AlteAnsichtSchalter.IsEnabled = false;
            p.Umschalter.WendeAn();

            Assert.False(p.ListeSchalter.IsEnabled);
            Assert.False(p.TabelleSchalter.IsEnabled);
            Assert.False(p.AbdockenSchalter.IsEnabled);

            p.AlteAnsichtSchalter.IsEnabled = true;
            p.Umschalter.WendeAn();
            Assert.True(p.ListeSchalter.IsEnabled);
            Assert.True(p.TabelleSchalter.IsEnabled);
            Assert.True(p.AbdockenSchalter.IsEnabled);
        });
    }

    [Fact]
    public void Ein_Sprung_von_aussen_klappt_die_Haltung_nur_in_der_Liste_auf()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();
            p.Umschalter.WendeAn();

            p.Umschalter.ZeigeHaltung(p.Datensaetze[2]);
            Assert.Same(p.Datensaetze[2], p.Liste.Aufgeklappt);
            Assert.Same(p.Datensaetze[2], p.Liste.SelectedItem);

            p.Liste.KlappeZu();
            p.Umschalter.Waehle("tabelle");
            p.Umschalter.ZeigeHaltung(p.Datensaetze[0]);
            Assert.Null(p.Liste.Aufgeklappt);
        });
    }

    /// <summary>
    /// Fix-Runde 1 (7): Eine gespeicherte Wahl gilt beim Seitenaufbau — der erste
    /// <c>WendeAn</c> liest die Einstellungen, nicht den Standard.
    /// </summary>
    [Fact]
    public void Eine_gespeicherte_Tabellenansicht_gilt_schon_beim_Aufbau()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();
            p.Settings.HaltungenAnsicht = "tabelle";

            p.Umschalter.WendeAn();

            Assert.Equal(Visibility.Visible, p.Tabelle.Visibility);
            Assert.Equal(Visibility.Collapsed, p.Liste.Visibility);
            Assert.True(p.TabelleSchalter.IsChecked);
            // Beim Aufbau wird nichts gespeichert.
            Assert.Equal(0, p.Gespeichert);
        });
    }

    /// <summary>
    /// Fix-Runde 1 (1): Der Konfliktrueckruf der Formularfabrik meldete immer in die
    /// Eingabefelder-Schublade — in der Listenansicht ist die gar nicht sichtbar, die verworfene
    /// Eingabe verschwand also spurlos. Gemeldet wird jetzt an das Formular, das sie gezeigt hat.
    /// </summary>
    [Fact]
    public void Ein_Konflikt_landet_in_dem_Formular_das_ihn_gezeigt_hat()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();
            p.Umschalter.WendeAn();

            // Liste sichtbar, aber nichts aufgeklappt: Das Formular steht nicht in der Liste.
            Assert.False(p.Umschalter.ListeZeigtFormular);
            p.Umschalter.MeldeKonflikt("Bemerkungen", "Neu", "Alt + Zusatz");
            Assert.Empty(p.KonflikteListe);
            Assert.Single(p.KonflikteSchublade);

            // Aufgeklappte Haltung: Der Hinweis gehoert in ihre Kopfzeile.
            p.Liste.KlappeAuf(p.Datensaetze[1]);
            Assert.True(p.Umschalter.ListeZeigtFormular);
            p.Umschalter.MeldeKonflikt("Bemerkungen", "Neu", "Alt + Zusatz");
            Assert.Equal(("Bemerkungen", "Neu", "Alt + Zusatz"), Assert.Single(p.KonflikteListe));
            Assert.Single(p.KonflikteSchublade);

            // Tabellenansicht: wieder die Schublade, auch wenn die Liste noch etwas offen haette.
            p.Umschalter.Waehle("tabelle");
            Assert.False(p.Umschalter.ListeZeigtFormular);
            p.Umschalter.MeldeKonflikt("Bemerkungen", "Neu", "Alt + Zusatz");
            Assert.Single(p.KonflikteListe);
            Assert.Equal(2, p.KonflikteSchublade.Count);
        });
    }

    /// <summary>
    /// Fix-Runde 1 (2): Die Mehrfachauswahl der Tabelle ueberlebte den Ansichtswechsel. In der
    /// Liste sah eine Haltung markiert aus, geloescht wurden fuenf.
    /// </summary>
    [Fact]
    public void Geloescht_wird_nur_was_die_sichtbare_Ansicht_markiert_hat()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();
            p.Tabelle.SelectionMode = DataGridSelectionMode.Extended;
            p.Tabelle.ItemsSource = p.Datensaetze;
            p.Umschalter.Waehle("tabelle");
            foreach (var record in p.Datensaetze)
                p.Tabelle.SelectedItems.Add(record);

            Assert.Equal(4, p.Umschalter.MarkierteZeilen(p.Datensaetze[0]).Count);

            // Wechsel auf die Liste: Die Tabellenauswahl wird auf die eine gewaehlte reduziert.
            p.Liste.SelectedItem = p.Datensaetze[2];
            p.Umschalter.Waehle("liste");

            Assert.Equal([p.Datensaetze[2]], p.Umschalter.MarkierteZeilen(p.Datensaetze[2]));
            Assert.Single(p.Tabelle.SelectedItems);
            // Ohne gewaehlte Haltung wird nichts geloescht.
            Assert.Empty(p.Umschalter.MarkierteZeilen(null));
        });
    }

    /// <summary>
    /// Fix-Runde 1 (2): Der Loeschpunkt im geteilten Zeilenmenue sagt, was er wirklich tut. In der
    /// Liste gilt kein Del — dann darf auch kein Tastenkuerzel danebenstehen.
    /// </summary>
    [Fact]
    public void Der_Loeschpunkt_nennt_die_richtige_Menge_und_das_richtige_Kuerzel()
    {
        RunOnSta(() =>
        {
            var p = new Pruefstand();

            p.Umschalter.Waehle("liste");
            Assert.Equal(DataPageAnsichtUmschalter.LoeschenListe, p.LoeschenSchalter.Header);
            Assert.Equal(string.Empty, p.LoeschenSchalter.InputGestureText);

            p.Umschalter.Waehle("tabelle");
            Assert.Equal(DataPageAnsichtUmschalter.LoeschenTabelle, p.LoeschenSchalter.Header);
            Assert.Equal("Del", p.LoeschenSchalter.InputGestureText);
        });
    }

    /// <summary>Die Elemente der Seite als schlichte Controls; nur die Liste ist das echte Control.</summary>
    private sealed class Pruefstand
    {
        public Pruefstand()
        {
            Datensaetze = new ObservableCollection<HaltungRecord>(
                Enumerable.Range(1, 4).Select(Datensatz));
            Liste = new HaltungAufklappListe { ItemsSource = Datensaetze };
            // Das geteilte Zeilenmenue traegt den Loeschpunkt; der Umschalter findet ihn ueber
            // seine Marke im Tag (im Ressourcenteil gibt es keinen x:Name).
            ZeilenMenue.Items.Add(LoeschenSchalter);
            Umschalter = new DataPageAnsichtUmschalter(
                new DataPageAnsichtUmschalter.Elemente(
                    Tabelle, AlteAnsicht, Liste, Spaltenchips, AlteSuche, NovaSuche,
                    AlteAnsichtSchalter, ListeSchalter, TabelleSchalter, AbdockenSchalter, ZeilenMenue),
                () => Settings,
                () => Gespeichert++,
                (uebersicht, felder) => Arbeitsflaeche = (uebersicht, felder),
                (feld, aktuell, eingabe) => KonflikteListe.Add((feld, aktuell, eingabe)),
                (feld, aktuell, eingabe) => KonflikteSchublade.Add((feld, aktuell, eingabe)));
        }

        public ObservableCollection<HaltungRecord> Datensaetze { get; }
        public HaltungAufklappListe Liste { get; }
        public DataPageAnsichtUmschalter Umschalter { get; }
        public AppSettings Settings { get; } = new();
        public int Gespeichert { get; private set; }
        public (bool Uebersicht, bool Eingabefelder) Arbeitsflaeche { get; private set; }
        public DataGrid Tabelle { get; } = new();
        public ContentControl AlteAnsicht { get; } = new();
        public ItemsControl Spaltenchips { get; } = new();
        public Border AlteSuche { get; } = new();
        public StackPanel NovaSuche { get; } = new();
        public MenuItem AlteAnsichtSchalter { get; } = new() { IsCheckable = true };
        public MenuItem ListeSchalter { get; } = new() { IsCheckable = true, Tag = "liste" };
        public MenuItem TabelleSchalter { get; } = new() { IsCheckable = true, Tag = "tabelle" };
        public MenuItem AbdockenSchalter { get; } = new();
        public MenuItem LoeschenSchalter { get; } = new() { Tag = DataPageAnsichtUmschalter.LoeschenMarke };
        public ContextMenu ZeilenMenue { get; } = new();
        public List<(string Feld, string Aktuell, string Eingabe)> KonflikteListe { get; } = [];
        public List<(string Feld, string Aktuell, string Eingabe)> KonflikteSchublade { get; } = [];

        private static HaltungRecord Datensatz(int nummer)
        {
            var record = new HaltungRecord();
            record.SetFieldValue(FieldKeys.HoldingName, $"1000{nummer}-1000{nummer + 1}", FieldSource.Pdf, false);
            return record;
        }
    }

    private static void RunOnSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Der Umschalter-Test wurde nicht innerhalb von 60 Sekunden fertig.");

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
