using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 6: Alle Tastenkürzel an einem Ort - zuerst die globalen Kürzel
/// des Programmfensters (fest hier eingetragen, weil sie als <c>Window.InputBindings</c> in
/// <c>MainWindow.xaml</c> stehen und dort keine eigene, programmatisch auslesbare Liste bilden),
/// danach je eine Gruppe je Videoplayer-Kategorie - gelesen aus
/// <see cref="PlayerKeyboardShortcutPolicy.Beschreibungen"/>, NICHT hier abgeschrieben (Brief:
/// «eine Quelle»).
/// </summary>
public partial class TastenkuerzelWindow : Window
{
    private static TastenkuerzelWindow? _instanz;

    /// <summary>Nur fuer Tests (InternalsVisibleTo): das aktuell offene Einzelstueck, falls vorhanden.</summary>
    internal static TastenkuerzelWindow? Aktuelles => _instanz;

    public TastenkuerzelWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        GruppenListe.ItemsSource = BauGruppen();
    }

    /// <summary>Einzelstück wie <see cref="HandbuchWindow"/>: bereits offen -&gt; nur aktivieren.</summary>
    public static void ZeigeAn(Window? owner = null)
    {
        if (_instanz is { IsLoaded: true } offen)
        {
            offen.Activate();
            return;
        }

        var neu = new TastenkuerzelWindow();
        if (owner is not null)
            neu.Owner = owner;
        _instanz = neu;
        neu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_instanz, neu))
                _instanz = null;
        };
        neu.Show();
    }

    internal static IReadOnlyList<TastenkuerzelGruppe> BauGruppen()
    {
        var gruppen = new List<TastenkuerzelGruppe>
        {
            new("Allgemein",
            [
                new("F11", "Fokusmodus ein/aus"),
                new("Strg+N", "Neues Projekt"),
                new("Strg+O", "Projekt öffnen"),
                new("Strg+S", "Projekt speichern"),
                new("Strg+K", "Suche fokussieren"),
                new("F1", "Handbuch öffnen (Abschnitt der aktuellen Seite)"),
                new("Strg+F1", "Diese Tastenkürzel-Übersicht öffnen"),
            ]),
            new("Haltungen",
            [
                new("F3", "Sucheingabe der Haltungstabelle fokussieren"),
            ]),
        };

        // Player-Kürzel: eine Gruppe je Kategorie aus PlayerKeyboardShortcutPolicy, in deren
        // Reihenfolge - keine eigene Abschrift der Tasten/Beschreibungen.
        foreach (var kategorie in PlayerKeyboardShortcutPolicy.Beschreibungen
                     .Select(b => b.Gruppe)
                     .Distinct())
        {
            var kuerzel = PlayerKeyboardShortcutPolicy.Beschreibungen
                .Where(b => b.Gruppe == kategorie)
                .Select(b => new TastenkuerzelEintrag(b.Taste, b.Beschreibung))
                .ToList();
            gruppen.Add(new TastenkuerzelGruppe($"Videoplayer – {kategorie}", kuerzel));
        }

        return gruppen;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    public sealed record TastenkuerzelEintrag(string Taste, string Beschreibung);

    public sealed record TastenkuerzelGruppe(string Titel, IReadOnlyList<TastenkuerzelEintrag> Kuerzel);
}
