using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Symbol und Farbe des <see cref="NovaDialogWindow"/>.</summary>
public enum NovaDialogArt { Info, Warnung, Fehler, Frage }

/// <summary>Welche Knoepfe der Dialog zeigt.</summary>
public enum NovaDialogKnopfsatz { Ok, JaNein, JaNeinAbbrechen }

/// <summary>
/// Der eine Meldungs-/Rueckfragedialog im Nova-Design - ersetzt die Windows-MessageBox
/// (Ziel «Nova-Dialog statt Windows-MessageBox», Optikanalyse 28.09.2026). Wird nicht direkt
/// erzeugt; Einstieg ist die statische Fassade <see cref="NovaDialog"/> beziehungsweise
/// <see cref="Services.IDialogService"/>.
/// </summary>
public partial class NovaDialogWindow : Window
{
    /// <summary>
    /// Ergebnis des Dialogs. Schon vor einem Knopfklick auf den Wert gesetzt, den ein Schliessen
    /// ueber das Fenster-X oder Alt+F4 haben soll (dieselbe Bedeutung wie Esc): «Abbrechen» bei
    /// drei Knoepfen, sonst «Nein».
    /// </summary>
    public DialogConfirm Ergebnis { get; private set; }

    /// <summary>Der Knopf, den Enter ausloest und der beim Oeffnen den Tastaturfokus erhaelt.</summary>
    private Button? StandardKnopf { get; set; }

    public NovaDialogWindow(NovaDialogArt art, NovaDialogKnopfsatz knoepfe, string titel, string text, bool standardNein)
    {
        InitializeComponent();

        Title = "SewerStudio — " + titel;
        TitelBlock.Text = titel;
        TextInhalt.Text = text;
        Ergebnis = knoepfe == NovaDialogKnopfsatz.JaNeinAbbrechen ? DialogConfirm.Cancel : DialogConfirm.No;

        SetzeSymbol(art);
        BaueKnoepfe(knoepfe, standardNein);
        KopierenButton.Visibility = art == NovaDialogArt.Fehler ? Visibility.Visible : Visibility.Collapsed;

        // Max. Hoehe ~60 % des Bildschirms - danach uebernimmt der ScrollViewer.
        var maxHoehe = SystemParameters.WorkArea.Height * 0.6;
        TextScroll.MaxHeight = maxHoehe > 0 ? maxHoehe : 400;

        Loaded += (_, _) => StandardKnopf?.Focus();
    }

    private void SetzeSymbol(NovaDialogArt art)
    {
        var (glyph, brushKey) = art switch
        {
            NovaDialogArt.Info => ("", "AccentBrush"),
            NovaDialogArt.Warnung => ("", "WarningBrush"),
            NovaDialogArt.Fehler => ("", "DangerBrush"),
            NovaDialogArt.Frage => ("", "AccentBrush"),
            _ => ("", "AccentBrush")
        };
        ArtSymbol.Glyph = glyph;
        ArtSymbol.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
    }

    private void BaueKnoepfe(NovaDialogKnopfsatz knoepfe, bool standardNein)
    {
        switch (knoepfe)
        {
            case NovaDialogKnopfsatz.Ok:
                AbbrechenButton.Visibility = Visibility.Collapsed;
                NeinButton.Visibility = Visibility.Collapsed;
                HauptButton.Content = "OK";
                HauptButton.ToolTip = "OK";
                AutomationProperties.SetName(HauptButton, "OK");
                HauptButton.SetResourceReference(StyleProperty, "PrimaryButton");
                HauptButton.IsDefault = true;
                HauptButton.IsCancel = true;
                StandardKnopf = HauptButton;
                break;

            case NovaDialogKnopfsatz.JaNein:
                AbbrechenButton.Visibility = Visibility.Collapsed;
                NeinButton.Visibility = Visibility.Visible;
                HauptButton.Content = "Ja";
                HauptButton.ToolTip = "Ja";
                AutomationProperties.SetName(HauptButton, "Ja");
                NeinButton.IsCancel = true;

                if (standardNein)
                {
                    // Ambiguitaetsentscheid Aufgabe 1: Die Reihenfolge bleibt [Nein][Ja], «Ja»
                    // bleibt rechts, aber NICHT mehr PrimaryButton - sonst staende dort eine blau
                    // gefuellte Hauptaktion neben dem eigentlichen Standardknopf. «Nein» wird zum
                    // Standardknopf (Enter + Anfangsfokus), «Ja» ist die gewarnte Option.
                    NeinButton.IsDefault = true;
                    HauptButton.IsDefault = false;
                    HauptButton.SetResourceReference(StyleProperty, "NovaDialogDangerButton");
                    StandardKnopf = NeinButton;
                }
                else
                {
                    NeinButton.IsDefault = false;
                    HauptButton.IsDefault = true;
                    HauptButton.SetResourceReference(StyleProperty, "PrimaryButton");
                    StandardKnopf = HauptButton;
                }
                break;

            case NovaDialogKnopfsatz.JaNeinAbbrechen:
                AbbrechenButton.Visibility = Visibility.Visible;
                NeinButton.Visibility = Visibility.Visible;
                HauptButton.Content = "Ja";
                HauptButton.ToolTip = "Ja";
                AutomationProperties.SetName(HauptButton, "Ja");
                HauptButton.SetResourceReference(StyleProperty, "PrimaryButton");
                AbbrechenButton.IsCancel = true;
                NeinButton.IsCancel = false;
                NeinButton.IsDefault = false;
                HauptButton.IsDefault = true;
                StandardKnopf = HauptButton;
                break;
        }
    }

    private void OnHaupt(object sender, RoutedEventArgs e)
    {
        Ergebnis = DialogConfirm.Yes;
        Close();
    }

    private void OnNein(object sender, RoutedEventArgs e)
    {
        Ergebnis = DialogConfirm.No;
        Close();
    }

    private void OnAbbrechen(object sender, RoutedEventArgs e)
    {
        Ergebnis = DialogConfirm.Cancel;
        Close();
    }

    private void OnKopieren(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TextInhalt.Text ?? string.Empty);
        }
        catch (Exception)
        {
            // Die Zwischenablage kann in mancher Umgebung gesperrt sein (z. B. eine
            // Remote-Sitzung ohne Weiterleitung) - das darf den Dialog nicht zum Absturz bringen.
        }
    }
}
