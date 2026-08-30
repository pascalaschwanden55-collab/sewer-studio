namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Uebersetzt zwischen der gespeicherten Gestaltung in <c>settings.json</c> und dem
/// Modell, mit dem die Detailansicht arbeitet.
///
/// Eine unvollstaendige oder von Hand veraenderte Einstellungsdatei darf nichts
/// erzwingen: leere Titel, leere Feldnamen und fehlende Listen werden uebergangen. Bleibt
/// dabei nichts Brauchbares uebrig, gilt die Werkseinstellung.
/// </summary>
public static class RecordDetailLayoutSettingsMapper
{
    public static RecordDetailLayout ToLayout(DetailLayoutSettings? settings)
    {
        if (settings is null)
            return RecordDetailLayout.Empty;

        var columns = new List<RecordDetailLayoutColumn>();
        foreach (var column in settings.Columns ?? new List<DetailLayoutColumnSettings>())
        {
            if (column is null || string.IsNullOrWhiteSpace(column.Title))
                continue;

            var fields = (column.Fields ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            columns.Add(new RecordDetailLayoutColumn(column.Title, fields));
        }

        var hidden = (settings.HiddenFields ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return columns.Count == 0 && hidden.Count == 0
            ? RecordDetailLayout.Empty
            : new RecordDetailLayout(columns, hidden);
    }

    public static DetailLayoutSettings ToSettings(RecordDetailLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        return new DetailLayoutSettings
        {
            Columns = layout.Columns
                .Select(c => new DetailLayoutColumnSettings
                {
                    Title = c.Title,
                    Fields = new List<string>(c.Fields)
                })
                .ToList(),
            HiddenFields = new List<string>(layout.HiddenFields)
        };
    }
}
