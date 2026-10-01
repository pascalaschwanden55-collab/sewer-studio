using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Nova-Fixwelle B5: Das geschlossene Auswahlfeld zeigte den Rohtext des Optionsobjekts
/// (<c>AutoSaveModeOption { Value = … }</c>) statt seiner Beschriftung.
///
/// Ursache (mit <c>ComboBoxAnzeigeIsolatedSmokeTests</c> nachgewiesen): WPF leitet
/// <see cref="ComboBox.SelectionBoxItemTemplate"/> NICHT aus <see cref="ItemsControl.DisplayMemberPath"/>
/// ab — die Eigenschaft bleibt null. Die eigene ComboBox-Vorlage bindet sie korrekt, hat damit
/// aber nichts in der Hand und faellt auf <c>ToString()</c> zurueck. In der aufgeklappten Liste
/// faellt das nicht auf: Dort sitzt der Textbaustein in einem echten Element-Container, und der
/// kennt seinen Besitzer.
///
/// Dieser Auswaehler schliesst genau diese Luecke: Er baut aus dem Pfad eine kleine
/// Anzeigevorlage. Ein ausdruecklicher <see cref="ItemsControl.ItemTemplateSelector"/> des
/// Auswahlfelds hat weiterhin Vorrang; ohne Pfad und ohne Auswaehler liefert er null und die
/// bisherige Standardanzeige bleibt.
/// </summary>
public sealed class ComboBoxAnzeigeTemplateSelector : DataTemplateSelector
{
    private static readonly Dictionary<string, DataTemplate> Vorlagen = new(StringComparer.Ordinal);

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is null)
            return null;

        var box = (container as FrameworkElement)?.TemplatedParent as ComboBox;
        if (box is null)
            return null;

        // Eine ausdrueckliche Auswahl des Bedieners geht vor.
        if (box.ItemTemplateSelector is { } eigener && eigener != this)
            return eigener.SelectTemplate(item, container);

        var pfad = box.DisplayMemberPath;
        if (string.IsNullOrWhiteSpace(pfad))
            return null;

        return VorlageFuer(pfad);
    }

    /// <summary>Eine je Pfad zwischengespeicherte, eingefrorene Anzeigevorlage.</summary>
    internal static DataTemplate VorlageFuer(string pfad)
    {
        lock (Vorlagen)
        {
            if (Vorlagen.TryGetValue(pfad, out var vorhanden))
                return vorhanden;

            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new Binding(pfad));
            // Die Tinte kommt vom Auswahlfeld; sonst schlaegt der implizite TextBlock-Stil zu.
            text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            var vorlage = new DataTemplate { VisualTree = text };
            vorlage.Seal();
            Vorlagen[pfad] = vorlage;
            return vorlage;
        }
    }
}
