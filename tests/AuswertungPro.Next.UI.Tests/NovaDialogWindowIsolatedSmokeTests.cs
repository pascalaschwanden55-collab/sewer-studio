using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Der Nova-Dialog (Aufgabe 1, Optikanalyse 28.09.2026) wird wirklich gezeichnet: Knopfleisten je
/// Art, Standard-/Abbrechen-Knopf, Rueckgabewerte per programmatischem Klick und die Darstellung im
/// Dunkeltheme - im eigenen Prozess mit echtem WPF (Muster wie <see cref="ListenErgaenzungWindowIsolatedSmokeTests"/>).
/// </summary>
[Collection("IsolatedWpf")]
public sealed class NovaDialogWindowIsolatedSmokeTests
{
    [Fact]
    public async Task NovaDialogWindow_laesst_sich_in_eigenem_Wpf_Prozess_pruefen()
    {
        var result = await WpfIsolatedTestProcess.RunAsync(
            typeof(NovaDialogWindowIsolatedSmokeTests).FullName + "." + nameof(Kindprozess),
            TimeSpan.FromSeconds(60));
        Assert.False(result.TimedOut, result.DescribeFailure());
        Assert.True(result.ExitCode == 0 && result.ChildScenarioCompleted, result.DescribeFailure());
    }

    [IsolatedWpfFact]
    public void Kindprozess()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Null(System.Windows.Application.Current);
            var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var resource in new[] { "Theme/Theme.xaml", "Theme/Controls.xaml" })
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/SewerStudio;component/" + resource, UriKind.Relative) });

            // Dunkles CardBrush/AccentBrush/WarningBrush/DangerBrush zum Vergleich merken - das ist
            // das Theme.xaml (dunkel); App.xaml selbst merged standardmaessig ThemeLight.xaml.
            var dunklesCard = (SolidColorBrush)app.Resources["CardBrush"];
            var accentBrush = (SolidColorBrush)app.Resources["AccentBrush"];
            var warningBrush = (SolidColorBrush)app.Resources["WarningBrush"];
            var dangerBrush = (SolidColorBrush)app.Resources["DangerBrush"];
            var primaryButtonStyle = (Style)app.Resources["PrimaryButton"];
            var secondaryButtonStyle = (Style)app.Resources["SecondaryButton"];

            // ── 1) Info/Warn/Error teilen denselben Ok-Knopfsatz; Fehler zeigt zusaetzlich den
            //      Kopieren-Knopf, Info/Warnung nicht. Am Fehler-Fenster gleich das Dunkeltheme
            //      pruefen (Hintergrund = CardBrush des Dunkeltheme). ──
            var fehlerFenster = Erzeuge(NovaDialogArt.Fehler, NovaDialogKnopfsatz.Ok, false, "Fehler", "Ein Testfehler ist aufgetreten.\nZweite Zeile.");
            Assert.Equal(dunklesCard.Color, ((SolidColorBrush)fehlerFenster.Background).Color);
            Assert.Equal("SewerStudio — Fehler", fehlerFenster.Title);

            var titelBlock = Assert.IsType<TextBlock>(fehlerFenster.FindName("TitelBlock"));
            Assert.Equal("Fehler", titelBlock.Text);
            var textInhalt = Assert.IsType<TextBox>(fehlerFenster.FindName("TextInhalt"));
            Assert.Equal("Ein Testfehler ist aufgetreten.\nZweite Zeile.", textInhalt.Text);
            Assert.True(textInhalt.IsReadOnly);

            var artSymbol = Assert.IsType<FluentIcon>(fehlerFenster.FindName("ArtSymbol"));
            Assert.Equal(dangerBrush.Color, ((SolidColorBrush)artSymbol.Foreground).Color);

            var hauptOk = Assert.IsType<Button>(fehlerFenster.FindName("HauptButton"));
            var neinOk = Assert.IsType<Button>(fehlerFenster.FindName("NeinButton"));
            var abbrechenOk = Assert.IsType<Button>(fehlerFenster.FindName("AbbrechenButton"));
            var kopierenOk = Assert.IsType<Button>(fehlerFenster.FindName("KopierenButton"));

            Assert.Equal("OK", hauptOk.Content);
            Assert.Same(primaryButtonStyle, hauptOk.Style);
            Assert.True(hauptOk.IsDefault, "OK muss der Standardknopf sein (Enter).");
            Assert.True(hauptOk.IsCancel, "OK schliesst auch bei Esc.");
            Assert.Equal(Visibility.Collapsed, neinOk.Visibility);
            Assert.Equal(Visibility.Collapsed, abbrechenOk.Visibility);
            Assert.Equal(Visibility.Visible, kopierenOk.Visibility); // nur bei Fehlern sichtbar

            // Der Kopieren-Knopf darf trotz evtl. gesperrter Zwischenablage nicht werfen.
            Klicke(kopierenOk, fehlerFenster);
            Assert.True(fehlerFenster.IsVisible, "Kopieren darf den Dialog nicht schliessen.");

            Klicke(hauptOk, fehlerFenster);
            Assert.False(fehlerFenster.IsVisible, "OK muss den Dialog schliessen.");

            // Info: Ok-Knopfsatz, aber KEIN Kopieren-Knopf, Symbol in AccentBrush.
            var infoFenster = Erzeuge(NovaDialogArt.Info, NovaDialogKnopfsatz.Ok, false, "Hinweis", "Ein Hinweistext.");
            Assert.Equal(Visibility.Collapsed, Button(infoFenster, "KopierenButton").Visibility);
            Assert.Equal(accentBrush.Color, ((SolidColorBrush)FluentIcon(infoFenster, "ArtSymbol").Foreground).Color);

            // Warnung (Ok-Knopfsatz, z. B. Warn()): auch kein Kopieren-Knopf, Symbol in WarningBrush.
            var warnFenster = Erzeuge(NovaDialogArt.Warnung, NovaDialogKnopfsatz.Ok, false, "Warnung", "Ein Warntext.");
            Assert.Equal(Visibility.Collapsed, Button(warnFenster, "KopierenButton").Visibility);
            Assert.Equal(warningBrush.Color, ((SolidColorBrush)FluentIcon(warnFenster, "ArtSymbol").Foreground).Color);

            // ── 2) Normale Ja/Nein-Bestaetigung (Confirm): [Nein][Ja], «Ja» ist Standardknopf. ──
            var confirmJa = Erzeuge(NovaDialogArt.Frage, NovaDialogKnopfsatz.JaNein, false, "Bestätigung", "Wirklich fortfahren?");
            var neinConfirm = Button(confirmJa, "NeinButton");
            var jaConfirm = Button(confirmJa, "HauptButton");
            Assert.Equal(Visibility.Collapsed, Button(confirmJa, "AbbrechenButton").Visibility);
            Assert.Equal("Nein", neinConfirm.Content);
            Assert.Equal("Ja", jaConfirm.Content);
            Assert.Same(secondaryButtonStyle, neinConfirm.Style);
            Assert.Same(primaryButtonStyle, jaConfirm.Style);
            Assert.False(neinConfirm.IsDefault);
            Assert.True(neinConfirm.IsCancel, "Nein schliesst bei Esc.");
            Assert.True(jaConfirm.IsDefault, "Ja ist bei einer normalen Bestaetigung der Standardknopf.");
            PruefeReihenfolgeVorDemHaupt(confirmJa, neinConfirm);
            Klicke(jaConfirm, confirmJa);
            Assert.Equal(DialogConfirm.Yes, confirmJa.Ergebnis);

            var confirmNein = Erzeuge(NovaDialogArt.Frage, NovaDialogKnopfsatz.JaNein, false, "Bestätigung", "Wirklich fortfahren?");
            Klicke(Button(confirmNein, "NeinButton"), confirmNein);
            Assert.Equal(DialogConfirm.No, confirmNein.Ergebnis);

            // ── 3) Warnende Bestaetigung mit «Nein» als Standardknopf (ConfirmWarn defaultNo:true):
            //      Reihenfolge bleibt [Nein][Ja], aber «Nein» ist jetzt Standardknopf und «Ja» ist
            //      NICHT mehr PrimaryButton (das waere zwei "Hauptaktionen" nebeneinander). ──
            var warnConfirmNeinStandard = Erzeuge(NovaDialogArt.Warnung, NovaDialogKnopfsatz.JaNein, true, "Bestätigung", "Wirklich verwerfen?");
            var neinWarn = Button(warnConfirmNeinStandard, "NeinButton");
            var jaWarn = Button(warnConfirmNeinStandard, "HauptButton");
            Assert.True(neinWarn.IsDefault, "Bei defaultNo:true ist «Nein» der Standardknopf.");
            Assert.True(neinWarn.IsCancel);
            Assert.False(jaWarn.IsDefault, "«Ja» ist bei defaultNo:true NICHT der Standardknopf.");
            Assert.NotSame(primaryButtonStyle, jaWarn.Style);
            Assert.Same(secondaryButtonStyle, neinWarn.Style);
            PruefeReihenfolgeVorDemHaupt(warnConfirmNeinStandard, neinWarn); // Ja bleibt rechts

            var warnConfirmNeinKlick = Erzeuge(NovaDialogArt.Warnung, NovaDialogKnopfsatz.JaNein, true, "Bestätigung", "Wirklich verwerfen?");
            Klicke(Button(warnConfirmNeinKlick, "NeinButton"), warnConfirmNeinKlick);
            Assert.Equal(DialogConfirm.No, warnConfirmNeinKlick.Ergebnis);

            var warnConfirmJaKlick = Erzeuge(NovaDialogArt.Warnung, NovaDialogKnopfsatz.JaNein, true, "Bestätigung", "Wirklich verwerfen?");
            Klicke(Button(warnConfirmJaKlick, "HauptButton"), warnConfirmJaKlick);
            Assert.Equal(DialogConfirm.Yes, warnConfirmJaKlick.Ergebnis);

            // ── 4) Drei-Wege-Bestaetigung (ConfirmCancel): [Abbrechen][Nein][Ja], «Ja» ist der
            //      Standardknopf, «Abbrechen» schliesst bei Esc. ──
            var dreiWege = Erzeuge(NovaDialogArt.Frage, NovaDialogKnopfsatz.JaNeinAbbrechen, false, "Bestätigung", "Speichern vor dem Schliessen?");
            var abbrechenDrei = Button(dreiWege, "AbbrechenButton");
            var neinDrei = Button(dreiWege, "NeinButton");
            var jaDrei = Button(dreiWege, "HauptButton");
            Assert.Equal(Visibility.Visible, abbrechenDrei.Visibility);
            Assert.Equal(Visibility.Visible, neinDrei.Visibility);
            Assert.Equal("Abbrechen", abbrechenDrei.Content);
            Assert.Equal("Nein", neinDrei.Content);
            Assert.Equal("Ja", jaDrei.Content);
            Assert.True(abbrechenDrei.IsCancel, "Abbrechen schliesst bei Esc.");
            Assert.False(neinDrei.IsCancel);
            Assert.False(neinDrei.IsDefault);
            Assert.True(jaDrei.IsDefault);
            Assert.Same(primaryButtonStyle, jaDrei.Style);
            PruefeDreiWegeReihenfolge(dreiWege, abbrechenDrei, neinDrei, jaDrei);

            var dreiWegeAbbrechenKlick = Erzeuge(NovaDialogArt.Frage, NovaDialogKnopfsatz.JaNeinAbbrechen, false, "Bestätigung", "Speichern vor dem Schliessen?");
            Klicke(Button(dreiWegeAbbrechenKlick, "AbbrechenButton"), dreiWegeAbbrechenKlick);
            Assert.Equal(DialogConfirm.Cancel, dreiWegeAbbrechenKlick.Ergebnis);

            var dreiWegeNeinKlick = Erzeuge(NovaDialogArt.Frage, NovaDialogKnopfsatz.JaNeinAbbrechen, false, "Bestätigung", "Speichern vor dem Schliessen?");
            Klicke(Button(dreiWegeNeinKlick, "NeinButton"), dreiWegeNeinKlick);
            Assert.Equal(DialogConfirm.No, dreiWegeNeinKlick.Ergebnis);

            var dreiWegeJaKlick = Erzeuge(NovaDialogArt.Frage, NovaDialogKnopfsatz.JaNeinAbbrechen, false, "Bestätigung", "Speichern vor dem Schliessen?");
            Klicke(Button(dreiWegeJaKlick, "HauptButton"), dreiWegeJaKlick);
            Assert.Equal(DialogConfirm.Yes, dreiWegeJaKlick.Ergebnis);

            // ── 5) Feste Breite ~460 px, max. Texthoehe positiv und hoechstens der Bildschirm. ──
            Assert.Equal(460, fehlerFenster.Width);
            var textScroll = Assert.IsType<ScrollViewer>(fehlerFenster.FindName("TextScroll"));
            Assert.True(textScroll.MaxHeight > 0 && textScroll.MaxHeight <= SystemParameters.WorkArea.Height);

            WpfIsolatedTestProcess.MarkChildScenarioCompleted();
            app.Shutdown();
        });
    }

    private static NovaDialogWindow Erzeuge(
        NovaDialogArt art, NovaDialogKnopfsatz knoepfe, bool standardNein, string titel, string text)
    {
        var fenster = new NovaDialogWindow(art, knoepfe, titel, text, standardNein)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000
        };
        WindowFx.SetEntrance(fenster, false);
        fenster.Show();
        fenster.UpdateLayout();
        fenster.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        return fenster;
    }

    private static void Klicke(Button knopf, NovaDialogWindow fenster)
    {
        knopf.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        fenster.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static Button Button(NovaDialogWindow fenster, string name)
        => Assert.IsType<Button>(fenster.FindName(name));

    private static FluentIcon FluentIcon(NovaDialogWindow fenster, string name)
        => Assert.IsType<FluentIcon>(fenster.FindName(name));

    /// <summary>Prueft, dass der Nein-Knopf im gemeinsamen Elternpanel VOR dem Haupt-(Ja-)Knopf
    /// steht - «Ja» bleibt in jedem Fall ganz rechts.</summary>
    private static void PruefeReihenfolgeVorDemHaupt(NovaDialogWindow fenster, Button nein)
    {
        var haupt = Button(fenster, "HauptButton");
        var panel = Assert.IsType<StackPanel>(nein.Parent);
        Assert.Same(panel, haupt.Parent);
        var neinIndex = panel.Children.IndexOf(nein);
        var hauptIndex = panel.Children.IndexOf(haupt);
        Assert.True(neinIndex < hauptIndex, "«Nein» muss links von «Ja» stehen.");
    }

    /// <summary>Reihenfolge der Drei-Wege-Bestaetigung: [Abbrechen][Nein][Ja].</summary>
    private static void PruefeDreiWegeReihenfolge(NovaDialogWindow fenster, Button abbrechen, Button nein, Button ja)
    {
        var panel = Assert.IsType<StackPanel>(abbrechen.Parent);
        Assert.Same(panel, nein.Parent);
        Assert.Same(panel, ja.Parent);
        var abbrechenIndex = panel.Children.IndexOf(abbrechen);
        var neinIndex = panel.Children.IndexOf(nein);
        var jaIndex = panel.Children.IndexOf(ja);
        Assert.True(abbrechenIndex < neinIndex, "«Abbrechen» muss links von «Nein» stehen.");
        Assert.True(neinIndex < jaIndex, "«Nein» muss links von «Ja» stehen.");
    }
}
