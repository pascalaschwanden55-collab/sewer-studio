using System.Globalization;
using System.Windows.Data;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Dieselbe bauwerksabhaengige Auswahl fuer Tabelle und aufgeklapptes Formular.</summary>
internal sealed class SchachtNormoptionen : IMultiValueConverter
{
    internal static IReadOnlyList<string> Funktion(string? art, string? funktion)
        => AbwasserbauwerkVokabular.Klasse(art, funktion) switch
        {
            "Normschacht" => SchachtFunktionVokabular.Auswahl
                .Where(s => s is not ("Sickerschacht" or "Spezialbauwerk")).ToArray(),
            "Spezialbauwerk" => new[] { "" }.Concat(AbwasserbauwerkVokabular.Spezialfunktionen).ToArray(),
            _ => [""]
        };

    internal static BindingBase? FuerSpalte(string feld)
    {
        if (SchaechteColumnPolicy.ResolveOptionField(feld) != "Funktion") return null;
        return new MultiBinding
        {
            Converter = new SchachtNormoptionen(),
            Bindings = { new Binding("."), new Binding("Fields") }
        };
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.ElementAtOrDefault(0) is SchachtRecord record
            ? Funktion(Wert(record, FieldKeys.ShaftStructureType), Wert(record, "Funktion")) : new[] { "" };
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    internal static void Verbinde(IEnumerable<RecordDetailItem> felder, SchachtRecord record)
    {
        var liste = felder.ToArray();
        var funktion = liste.FirstOrDefault(i => SchaechteColumnPolicy.ResolveOptionField(i.FieldName) == "Funktion");
        if (funktion is null || !funktion.IsCombo) return;
        var art = liste.FirstOrDefault(i => SchaechteColumnPolicy.ResolveOptionField(i.FieldName) == FieldKeys.ShaftStructureType);
        void Aktualisiere() => funktion.ErsetzeOptionen(Funktion(
            art?.Value ?? Wert(record, FieldKeys.ShaftStructureType), funktion.Value));
        Aktualisiere();
        if (art is not null)
            art.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(RecordDetailItem.Value)) Aktualisiere(); };
    }

    private static string Wert(SchachtRecord record, string feld)
        => record.GetFieldValue(SchachtFeldnamen.Feld(record, feld));
}
