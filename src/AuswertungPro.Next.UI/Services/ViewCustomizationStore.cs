using System;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Einziger Zugriffsweg auf die pro-Seite/-Fenster gespeicherten Anpassungen
/// (Spalten, Panelgroessen, gespeicherte Ansichten). Kapselt die AppSettings-Persistenz
/// und die Null-Guards. Aufbau bewusst analog <see cref="WindowStateManager"/>.
///
/// WICHTIG: Niemals eine eigene <c>AppSettings.Load()</c>-Instanz erzeugen — das umgeht
/// die Test-Isolation (SEWERSTUDIO_APPDATA_DIR) und die gehaertete Speicher-Kette.
/// Immer ueber die EINE beim Start via <see cref="Configure"/> uebergebene Instanz gehen.
/// </summary>
public static class ViewCustomizationStore
{
    private static AppSettings? _settings;

    public static void Configure(AppSettings settings)
        => _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Liefert (und erstellt bei Bedarf) den Anpassungs-Container einer Ansicht.</summary>
    public static ViewCustomization GetOrCreate(string viewKey)
    {
        var settings = _settings;
        if (settings is null || string.IsNullOrWhiteSpace(viewKey))
            return new ViewCustomization();

        settings.ViewCustomizations ??= new();
        if (!settings.ViewCustomizations.TryGetValue(viewKey, out var view) || view is null)
        {
            view = new ViewCustomization();
            settings.ViewCustomizations[viewKey] = view;
        }

        view.Grids ??= new();
        view.SplitterSizes ??= new();
        view.SavedViews ??= new();
        return view;
    }

    /// <summary>Liefert (und erstellt bei Bedarf) das Grid-Layout einer Ansicht.</summary>
    public static DataPageLayoutSettings GetOrCreateGrid(string viewKey, string gridKey)
    {
        var view = GetOrCreate(viewKey);
        if (string.IsNullOrWhiteSpace(gridKey))
            gridKey = "Grid";

        if (!view.Grids.TryGetValue(gridKey, out var grid) || grid is null)
        {
            grid = new DataPageLayoutSettings();
            view.Grids[gridKey] = grid;
        }

        grid.Columns ??= new();
        return grid;
    }

    /// <summary>
    /// Persistiert die aktuellen Anpassungen. Bewusst nur die DEBOUNCED Variante —
    /// laufende Tweaks (Spalten-Drag, Slider) duerfen keinen Full-File-Restore-Point je Ruck erzeugen.
    /// </summary>
    public static void Save() => _settings?.Save();

    /// <summary>Die gespeicherte Kachelgroesse der Foto-Galerie (Default 124).</summary>
    public static double GetPhotoGalleryTileSize()
        => _settings?.PhotoGalleryTileSize is { } size && double.IsFinite(size) ? size : 124d;

    /// <summary>
    /// Setzt die Kachelgroesse ueber die Live-Instanz und speichert sie. Ohne
    /// konfigurierte Live-Instanz (Configure nie gerufen, etwa im Designer) wird
    /// nichts geschrieben — eine eigene Dateikopie gibt es nicht (AP-2,
    /// Audit 2026-08-10): Eine Momentaufnahme wuerde beim Speichern alle
    /// seitdem geaenderten Einstellungen verwerfen.
    /// </summary>
    public static void SetPhotoGalleryTileSize(double value)
    {
        var settings = _settings;
        if (settings is null)
            return;
        settings.PhotoGalleryTileSize = value;
        settings.Save();
    }

    /// <summary>Nur fuer Tests: Store-Referenz zuruecksetzen.</summary>
    internal static void ResetForTests() => _settings = null;
}
