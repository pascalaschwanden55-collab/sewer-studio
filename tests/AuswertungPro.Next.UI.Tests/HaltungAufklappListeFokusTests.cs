using System;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova, Aufklapp-Liste, Fix-Runde 2: Escape aus einem Eingabefeld mit ECHTEM Tastaturfokus.
///
/// Ohne gezeigtes Fenster gibt es keinen Tastaturfokus und damit auch kein LostFocus — der
/// Nachweis, dass die Eingabe beim Fokuswechsel auf die Zeile erhalten bleibt, waere sonst
/// keiner. Das Fenster steht bei -10000 ausserhalb des Bildschirms (Muster
/// <see cref="PopupFocusHelperTests"/>).
///
/// Bewusst OHNE <c>App</c> und ohne den Formularaufbau: Der Pruefpunkt ist der Fokuswechsel und
/// der Rueckschreibweg des Feldes, nicht die Darstellung. Das gepruefte Textfeld haengt genau so
/// am <see cref="RecordDetailItem"/> wie im echten Formular (TwoWay, UpdateSourceTrigger
/// LostFocus).
/// </summary>
public sealed class HaltungAufklappListeFokusTests
{
    private const string Bemerkungen = "Bemerkungen";

    [Fact]
    public void Escape_im_Eingabefeld_holt_den_Fokus_auf_die_Zeile_und_behaelt_die_Eingabe()
    {
        RunOnSta(() =>
        {
            var (window, liste, datensaetze, editor) = Szenario();

            Keyboard.Focus(editor);
            Settle();
            Assert.True(editor.IsKeyboardFocused, "Das Eingabefeld hat den Tastaturfokus nicht bekommen.");

            // Kein UpdateSource von Hand: Genau der Fokuswechsel soll den Wert schreiben.
            editor.Text = "Im Feld getippt";
            Assert.True(liste.VerarbeiteTaste(Key.Escape, editor));
            Settle();

            var zeile = Zeile(liste, datensaetze[0]);
            Assert.False(editor.IsKeyboardFocusWithin, "Der Fokus ist im Eingabefeld geblieben.");
            Assert.True(zeile.IsKeyboardFocused, "Der Fokus ist nicht auf die Zeile gegangen.");

            // Die Haltung bleibt offen, und die Eingabe ist ueber den normalen Weg im Datensatz.
            Assert.Same(datensaetze[0], liste.Aufgeklappt);
            Assert.Equal("Im Feld getippt", datensaetze[0].GetFieldValue(Bemerkungen));
            Assert.Equal(FieldSource.Manual, datensaetze[0].FieldMeta[Bemerkungen].Source);
            Assert.True(datensaetze[0].FieldMeta[Bemerkungen].UserEdited);

            // Erst der zweite Escape, jetzt auf der Zeile, klappt zu.
            Assert.True(liste.VerarbeiteTaste(Key.Escape, zeile));
            Settle();
            Assert.Null(liste.Aufgeklappt);

            window.Close();
        });
    }

    /// <summary>
    /// Die Liste mit einer aufgeklappten Haltung in einem gezeigten Fenster. Statt des echten
    /// Formulars steht ein Textfeld mit derselben Bindung im aufgeklappten Bereich — der
    /// Formularaufbau braucht die App-Ressourcen und ist im Kindprozess-Smoketest geprueft.
    /// </summary>
    private static (Window Window, HaltungAufklappListe Liste, ObservableCollection<HaltungRecord> Datensaetze, TextBox Editor) Szenario()
    {
        var datensaetze = new ObservableCollection<HaltungRecord>(Enumerable.Range(1, 4).Select(Datensatz));
        var fabrik = new DataPageDetailItemFactory(
            _ => null,
            (record, feld, wert) => record.SetFieldValue(feld, wert, FieldSource.Manual, userEdited: true));

        var liste = new HaltungAufklappListe { ItemsSource = datensaetze };
        var controller = new DataPageAufklappListeController(
            liste,
            () => null,
            // Leere Themenliste: Damit rendert der aufgeklappte Bereich nur seinen Rahmen und
            // erzeugt KEINEN RecordDetailsView — der braucht die Anwendungsressourcen (er loest
            // "HeaderBrush" schon beim Bauen seiner eigenen Resources auf, also vor dem Einhaengen
            // in den Baum). Geprueft wird hier der Fokusweg, nicht die Darstellung.
            _ => []);
        controller.Verdrahte();

        var window = new Window
        {
            Width = 1200,
            Height = 700,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            Content = liste
        };
        // Ohne App gibt es keine Anwendungsressourcen; die Themes kommen deshalb ans Fenster,
        // in derselben Reihenfolge wie in App.xaml.
        foreach (var teil in new[] { "Theme/ThemeLight.xaml", "Theme/Controls.xaml", "Controls/NovaPageHeader.xaml" })
        {
            window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/SewerStudio;component/{teil}", UriKind.Absolute)
            });
        }
        window.Show();
        Settle();

        liste.KlappeAuf(datensaetze[0]);
        Settle();
        Assert.Same(datensaetze[0], liste.Aufgeklappt);

        var editor = Editor(liste, fabrik);
        return (window, liste, datensaetze, editor);
    }

    /// <summary>
    /// Das Eingabefeld im aufgeklappten Bereich. Es haengt mit derselben Bindung am
    /// <see cref="RecordDetailItem"/> wie im echten Formular (TwoWay, Rueckschreiben beim
    /// Fokusverlust) und sitzt an genau derselben Stelle im Baum — nur die Darstellung des
    /// RecordDetailsView fehlt.
    /// </summary>
    private static TextBox Editor(HaltungAufklappListe liste, DataPageDetailItemFactory fabrik)
    {
        var vorhanden = Alle<TextBox>(liste)
            .FirstOrDefault(t => t.DataContext is RecordDetailItem { FieldName: Bemerkungen });
        if (vorhanden is not null)
            return vorhanden;

        var item = fabrik.Create(Bemerkungen, liste.Aufgeklappt!);
        var feld = new TextBox { DataContext = item, Width = 200 };
        feld.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(RecordDetailItem.Value))
        {
            Mode = System.Windows.Data.BindingMode.TwoWay,
            UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus
        });

        var zeile = Alle<ListBoxItem>(liste).First(z => ReferenceEquals(z.DataContext, liste.Aufgeklappt));
        var traeger = Alle<ContentControl>(zeile).First(c => c.Content is not null);
        traeger.Content = feld;
        traeger.ContentTemplate = null;
        Settle();
        return feld;
    }

    private static ListBoxItem Zeile(DependencyObject wurzel, HaltungRecord record)
        => Alle<ListBoxItem>(wurzel).First(z => ReferenceEquals(z.DataContext, record));

    private static HaltungRecord Datensatz(int nummer)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, $"1000{nummer}-1000{nummer + 1}", FieldSource.Pdf, false);
        record.SetFieldValue(Bemerkungen, "Alt", FieldSource.Pdf, false);
        return record;
    }

    private static List<T> Alle<T>(DependencyObject wurzel) where T : DependencyObject
    {
        var treffer = new List<T>();
        Sammle(wurzel, treffer);
        return treffer;
    }

    private static void Sammle<T>(DependencyObject knoten, List<T> treffer) where T : DependencyObject
    {
        var anzahl = System.Windows.Media.VisualTreeHelper.GetChildrenCount(knoten);
        for (var i = 0; i < anzahl; i++)
        {
            var kind = System.Windows.Media.VisualTreeHelper.GetChild(knoten, i);
            if (kind is T passend)
                treffer.Add(passend);
            Sammle(kind, treffer);
        }
    }

    private static void Settle()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(150) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "Der Fokus-Test wurde nicht innerhalb von 60 Sekunden fertig.");

        if (exception is not null)
            ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
