using System;
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

    /// <summary>Die Elemente der Seite als schlichte Controls; nur die Liste ist das echte Control.</summary>
    private sealed class Pruefstand
    {
        public Pruefstand()
        {
            Datensaetze = new ObservableCollection<HaltungRecord>(
                Enumerable.Range(1, 4).Select(Datensatz));
            Liste = new HaltungAufklappListe { ItemsSource = Datensaetze };
            Umschalter = new DataPageAnsichtUmschalter(
                new DataPageAnsichtUmschalter.Elemente(
                    Tabelle, AlteAnsicht, Liste, Spaltenchips, AlteSuche, NovaSuche,
                    AlteAnsichtSchalter, ListeSchalter, TabelleSchalter, AbdockenSchalter),
                () => Settings,
                () => Gespeichert++,
                (uebersicht, felder) => Arbeitsflaeche = (uebersicht, felder));
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
