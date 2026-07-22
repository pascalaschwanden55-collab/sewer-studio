using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Container fuer einen datengetriebenen Bereich mit vier Zustaenden: Inhalt, Laedt, Leer, Fehler.
/// Der eigentliche Inhalt ist der <see cref="ContentControl.Content"/>; die anderen drei Zustaende
/// blendet das Template ueber <see cref="State"/> ein. Der Leerzustand nutzt das vorhandene
/// <see cref="EmptyStateControl"/>. So bekommt jede Seite denselben Lade-/Leer-/Fehler-Auftritt,
/// ohne ihn einzeln nachzubauen.
/// </summary>
public class StatusHost : ContentControl
{
    static StatusHost()
    {
        // Lookless-Control: der Default-Style/-Template liegt in Theme/Controls.xaml.
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(StatusHost), new FrameworkPropertyMetadata(typeof(StatusHost)));
    }

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(StatusHostState), typeof(StatusHost),
            new PropertyMetadata(StatusHostState.Content));

    public static readonly DependencyProperty LoadingTextProperty =
        DependencyProperty.Register(nameof(LoadingText), typeof(string), typeof(StatusHost),
            new PropertyMetadata("Laedt…"));

    public static readonly DependencyProperty EmptyIconProperty =
        DependencyProperty.Register(nameof(EmptyIcon), typeof(string), typeof(StatusHost),
            new PropertyMetadata(""));

    public static readonly DependencyProperty EmptyTitleProperty =
        DependencyProperty.Register(nameof(EmptyTitle), typeof(string), typeof(StatusHost),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyMessageProperty =
        DependencyProperty.Register(nameof(EmptyMessage), typeof(string), typeof(StatusHost),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyActionTextProperty =
        DependencyProperty.Register(nameof(EmptyActionText), typeof(string), typeof(StatusHost),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyActionCommandProperty =
        DependencyProperty.Register(nameof(EmptyActionCommand), typeof(ICommand), typeof(StatusHost),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ErrorTitleProperty =
        DependencyProperty.Register(nameof(ErrorTitle), typeof(string), typeof(StatusHost),
            new PropertyMetadata("Laden fehlgeschlagen"));

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(StatusHost),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RetryTextProperty =
        DependencyProperty.Register(nameof(RetryText), typeof(string), typeof(StatusHost),
            new PropertyMetadata("Erneut versuchen"));

    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(nameof(RetryCommand), typeof(ICommand), typeof(StatusHost),
            new PropertyMetadata(null));

    /// <summary>Welcher der vier Zustaende gerade sichtbar ist.</summary>
    public StatusHostState State
    {
        get => (StatusHostState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Text unter dem Ladebalken.</summary>
    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    /// <summary>Fluent-Icon-Glyph fuer den Leerzustand.</summary>
    public string EmptyIcon
    {
        get => (string)GetValue(EmptyIconProperty);
        set => SetValue(EmptyIconProperty, value);
    }

    /// <summary>Titel des Leerzustands.</summary>
    public string EmptyTitle
    {
        get => (string)GetValue(EmptyTitleProperty);
        set => SetValue(EmptyTitleProperty, value);
    }

    /// <summary>Erklaerender Text des Leerzustands.</summary>
    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    /// <summary>Beschriftung eines optionalen Knopfes im Leerzustand (z. B. „Aktualisieren").</summary>
    public string EmptyActionText
    {
        get => (string)GetValue(EmptyActionTextProperty);
        set => SetValue(EmptyActionTextProperty, value);
    }

    /// <summary>Optionaler Befehl im Leerzustand; ohne Befehl bleibt der Knopf ausgeblendet.</summary>
    public ICommand? EmptyActionCommand
    {
        get => (ICommand?)GetValue(EmptyActionCommandProperty);
        set => SetValue(EmptyActionCommandProperty, value);
    }

    /// <summary>Titel des Fehlerzustands.</summary>
    public string ErrorTitle
    {
        get => (string)GetValue(ErrorTitleProperty);
        set => SetValue(ErrorTitleProperty, value);
    }

    /// <summary>Fehlermeldung (z. B. aus dem ViewModel).</summary>
    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    /// <summary>Beschriftung des Wiederholen-Knopfes im Fehlerzustand.</summary>
    public string RetryText
    {
        get => (string)GetValue(RetryTextProperty);
        set => SetValue(RetryTextProperty, value);
    }

    /// <summary>Optionaler Befehl fuer den Wiederholen-Knopf; ohne Befehl bleibt er ausgeblendet.</summary>
    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }
}
