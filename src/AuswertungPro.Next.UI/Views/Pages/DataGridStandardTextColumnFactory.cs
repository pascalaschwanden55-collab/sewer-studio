using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Theme;

namespace AuswertungPro.Next.UI.Views.Pages;

public static class DataGridStandardTextColumnFactory
{
    public static DataGridTextColumn Create(
        string fieldName,
        string header,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.LostFocus,
        bool hoeheBegrenzen = false)
    {
        var displayStyle = new Style(typeof(TextBlock), ApplicationStyleResolver.FindImplicit(typeof(TextBlock)));
        displayStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new Binding("Foreground")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridCell), 1)
        }));
        // Nova-Etappe 2b: hoechstens drei Zeilen je Zelle. Ein Feldwert mit Zeilenumbruechen
        // (etwa "Primaere Schaeden") zog sonst die ganze Tabellenzeile auf; der gespeicherte
        // Text, die Bearbeitung und der Export bleiben unveraendert vollstaendig. Gilt nur im
        // Nova-Layout — die alte Haltungsansicht bleibt unveraendert (Fix-Runde 1, F4).
        if (hoeheBegrenzen)
            displayStyle.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, DataPageColumnStyleRules.MaximaleZellenhoehe));
        if (DataPageColumnStyleRules.IstNamensspalte(fieldName))
            displayStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        if (DataPageColumnStyleRules.IstZahlenspalte(fieldName))
        {
            displayStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, System.Windows.Application.Current?.TryFindResource("FontMono") ?? new FontFamily("Consolas")));
            displayStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        }
        return new DataGridTextColumn
        {
            Header = header,
            ElementStyle = displayStyle,
            Binding = new Binding($"Fields[{fieldName}]")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = updateSourceTrigger
            },
            Width = DataGridLength.SizeToHeader
        };
    }
}
