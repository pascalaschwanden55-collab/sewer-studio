using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Gemeinsamer Rueckgaengig-/Wiederholen-Bereich fuer die Texteingaben des
/// Dossiers. Er merkt das zuletzt aktive Textfeld zentral, damit nicht jedes
/// dynamisch erzeugte Feld eigene Knopf-Ereignisse braucht.
/// </summary>
internal sealed class DossierTextUndoController
{
    private readonly Button _rueckgaengig;
    private readonly Button _wiederholen;

    public DossierTextUndoController(UIElement wurzel)
    {
        ArgumentNullException.ThrowIfNull(wurzel);

        _rueckgaengig = ErzeugeKnopf(
            "\uE7A7",
            "Rückgängig (Strg+Z)",
            ApplicationCommands.Undo);
        _wiederholen = ErzeugeKnopf(
            "\uE7A6",
            "Wiederholen (Strg+Y)",
            ApplicationCommands.Redo);

        var leiste = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        leiste.Children.Add(new TextBlock
        {
            Text = "Text:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        });
        leiste.Children.Add(_rueckgaengig);
        leiste.Children.Add(_wiederholen);
        View = leiste;

        wurzel.AddHandler(
            Keyboard.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(BehandleFokus),
            handledEventsToo: true);
        wurzel.AddHandler(
            TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(BehandleTextaenderung),
            handledEventsToo: true);

        SetzeZiel(null);
    }

    /// <summary>Die sichtbare Leiste fuer den Kopf des Feldbereichs.</summary>
    public FrameworkElement View { get; }

    /// <summary>
    /// Vergisst das aktive Feld vor einem Neuaufbau. Sonst koennte ein Knopf
    /// auf eine bereits aus dem Baum entfernte Texteingabe zeigen.
    /// </summary>
    public void Reset() => SetzeZiel(null);

    private void BehandleFokus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is TextBoxBase textfeld)
            SetzeZiel(textfeld);
    }

    private static void BehandleTextaenderung(object sender, TextChangedEventArgs e)
        => CommandManager.InvalidateRequerySuggested();

    private void SetzeZiel(TextBoxBase? textfeld)
    {
        _rueckgaengig.CommandTarget = textfeld;
        _wiederholen.CommandTarget = textfeld;
        _rueckgaengig.IsEnabled = textfeld is not null;
        _wiederholen.IsEnabled = textfeld is not null;
        CommandManager.InvalidateRequerySuggested();
    }

    private static Button ErzeugeKnopf(
        string symbol,
        string beschreibung,
        RoutedUICommand befehl)
    {
        var knopf = new Button
        {
            Content = new FluentIcon { Glyph = symbol, FontSize = 15 },
            Command = befehl,
            Focusable = false,
            MinWidth = 34,
            MinHeight = 28,
            Padding = new Thickness(7, 1, 7, 2),
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = beschreibung
        };
        AutomationProperties.SetName(knopf, beschreibung);
        return knopf;
    }
}
