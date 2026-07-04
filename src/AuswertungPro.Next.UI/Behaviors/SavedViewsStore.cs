using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Reine, testbare Store-Logik fuer benannte Ansichten je ViewKey (ohne WPF).
/// Der WPF-Teil (Spalten-/Sortier-Erfassung) liegt im SavedViewsController.
/// </summary>
internal static class SavedViewsStore
{
    public static IReadOnlyList<string> Names(string viewKey)
        => ViewCustomizationStore.GetOrCreate(viewKey).SavedViews.Select(v => v.Name).ToList();

    public static SavedView? Get(string viewKey, string name)
        => ViewCustomizationStore.GetOrCreate(viewKey).SavedViews
            .FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Fuegt eine Ansicht ein oder ueberschreibt die gleichnamige (Rename via Speichern).</summary>
    public static void Upsert(string viewKey, SavedView view)
    {
        if (string.IsNullOrWhiteSpace(view.Name))
            return;

        var container = ViewCustomizationStore.GetOrCreate(viewKey);
        container.SavedViews.RemoveAll(v => string.Equals(v.Name, view.Name, StringComparison.OrdinalIgnoreCase));
        container.SavedViews.Add(view);
        ViewCustomizationStore.Save();
    }

    public static void Delete(string viewKey, string name)
    {
        var container = ViewCustomizationStore.GetOrCreate(viewKey);
        var removed = container.SavedViews.RemoveAll(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            ViewCustomizationStore.Save();
    }
}
