using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Persoenliche Listenansicht und davon getrennte, datenfreie Gestaltungsvorschau.</summary>
internal static class AufklappDetailLayout
{
    public static IReadOnlyList<RecordDetailGroup> Vorschau(
        IReadOnlyList<RecordDetailGroup> standard, RecordDetailLayout layout)
    {
        // Keine Datensatz-Callbacks oder Nachschlagebefehle in der Gestaltungsvorschau.
        // Auch fachlich momentan unsichtbare Felder bleiben hier anordenbar.
        var kopien = standard.Select(g => g with
        {
            Kind = RecordDetailGroupKind.Additional,
            Items = g.Items.Select(i => new RecordDetailItem(i.Label, i.Value, _ => { },
                isReadOnly: true, isMultiline: i.IsMultiline)
            { FieldName = i.FieldName }).ToList()
        }).ToList();
        return RecordDetailLayoutApplier.Apply(kopien, layout);
    }

    public static IReadOnlyList<ThemaAnzeige> Themen(
        IReadOnlyList<RecordDetailGroup> standard, DetailLayoutSettings? settings)
    {
        var gruppen = RecordDetailLayoutApplier.Apply(standard, RecordDetailLayoutSettingsMapper.ToLayout(settings));
        // Versteckte Karten kosten weder Platz noch eine leere Themenspalte.
        // Dokumentfelder sind hier normale Eingaben: auch dorthin verschobene Felder
        // duerfen nicht vom Dokumentfilter des alten kompakten Detailfensters verschwinden.
        return HaltungThemenGruppierung.Bilde(gruppen.Select(g => g with
        {
            Kind = g.Kind == RecordDetailGroupKind.Documents ? RecordDetailGroupKind.Additional : g.Kind,
            Items = g.Items.Where(i => !i.IsHiddenByUser).ToList()
        }).ToList());
    }
}
