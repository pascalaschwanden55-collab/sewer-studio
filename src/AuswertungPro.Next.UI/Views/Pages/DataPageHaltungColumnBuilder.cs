using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Baut die Spalten der Haltungstabelle: erst jedes Feld des Katalogs in seiner festen
/// Reihenfolge, danach im Nova-Layout die vier nur lesenden Statusspalten.
///
/// Die Seite haengt die fertigen Spalten nur noch ein — das haelt <c>DataPage</c> unter seiner
/// Groessengrenze (Waechter <c>MaintainabilityFitnessTests</c>) und macht den Aufbau ohne
/// Fenster pruefbar.
/// </summary>
internal static class DataPageHaltungColumnBuilder
{
    /// <summary>Eine fertige Spalte samt ihrem Feldnamen und der Standardausrichtung.</summary>
    internal sealed record Spalte(string Feld, DataGridColumn Column, DataPageColumnSetupResult Setup);

    /// <param name="novaLayout">
    /// Nova-Layout: Die Zustandsklasse traegt eine Marke statt einer ganzflaechig eingefaerbten
    /// Zelle, lange Texte werden auf drei Zeilen begrenzt, und die vier Statusspalten kommen
    /// dazu. Ist es aus, entsteht genau die bisherige Tabelle.
    /// </param>
    public static IReadOnlyList<Spalte> Baue(
        bool novaLayout,
        KeyboardFocusChangedEventHandler lostKeyboardFocus,
        SelectionChangedEventHandler selectionChanged)
    {
        var spalten = new List<Spalte>();

        foreach (var field in FieldCatalog.ColumnOrder)
        {
            var def = FieldCatalog.Get(field);
            var chipSpalte = novaLayout && string.Equals(field, FieldKeys.ConditionClass, StringComparison.Ordinal);
            var column = chipSpalte
                ? ZustandsklasseChipColumnFactory.Create(field, Grossschrift(def.Label))
                : DataPageColumnFactory.Create(field, def.Label, lostKeyboardFocus, selectionChanged, novaLayout);

            // Nova-Fixwelle 2b (P3): Im Nova-Layout traegt JEDE Spalte den Volltext oben im
            // Hinweis, nicht nur die Umbruchspalte — die Zellen kuerzen jetzt mit
            // Auslassungspunkten, und ein gekuerzter Wert muss ohne Verbreitern lesbar sein.
            // Die Herkunftszeile bleibt darunter unveraendert.
            var setup = DataPageColumnSetup.Apply(
                column,
                field,
                farbzelle: !chipSpalte,
                volltextHinweis: novaLayout);

            // Startbreite aus dem Prototyp; ein gespeichertes Spaltenlayout gewinnt, weil es
            // erst mit RestoreLayoutFromSettings gelesen wird.
            if (novaLayout && NovaSpaltenbreiten.Startbreite(field) is double breite)
                column.Width = new DataGridLength(breite);

            // Die GEONIS-Kennung ist nur Anzeige: Der Export liest das Geonis-Objekt des
            // Datensatzes, eine Handeingabe in der Zelle liefe daran vorbei.
            if (string.Equals(field, FieldKeys.GeonisId, StringComparison.Ordinal))
                column.IsReadOnly = true;

            spalten.Add(new Spalte(field, column, setup));
        }

        if (novaLayout)
        {
            Ergaenze(spalten, NovaStatusSpalten.Ki, HaltungStatusColumnFactory.Ki(Grossschrift("KI")));
            Ergaenze(spalten, NovaStatusSpalten.Pruefung, HaltungStatusColumnFactory.Pruefung(Grossschrift("Prüfung")));
            Ergaenze(spalten, NovaStatusSpalten.Video, HaltungStatusColumnFactory.Video(Grossschrift("Video")));
            Ergaenze(spalten, NovaStatusSpalten.Protokoll, HaltungStatusColumnFactory.Protokoll(Grossschrift("Protokoll")));
        }

        return spalten;
    }

    /// <summary>Spaltenkoepfe schreiben gross; dieselbe Regel wie in <see cref="DataPageColumnFactory"/>.</summary>
    public static string Grossschrift(string header)
        => GrossbuchstabenConverter.Anwenden(header) ?? header;

    private static void Ergaenze(List<Spalte> spalten, string schluessel, DataGridColumn column)
    {
        column.SetValue(FrameworkElement.TagProperty, schluessel);
        spalten.Add(new Spalte(
            schluessel,
            column,
            new DataPageColumnSetupResult(HorizontalAlignment.Left, VerticalAlignment.Center)));
    }
}
