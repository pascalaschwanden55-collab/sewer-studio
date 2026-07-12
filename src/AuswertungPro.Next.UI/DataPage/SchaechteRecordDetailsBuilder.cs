using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.DataPage.SchaechteColumnPolicy;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Baut die editierbare Schacht-Detailansicht aus freien Projektfeldern.
/// Gruppierung und Eingabetypen bleiben dadurch aus dem Seiten-Code heraus.
/// </summary>
internal sealed class SchaechteRecordDetailsBuilder
{
    private readonly Func<string, IEnumerable<string>> _resolveOptions;
    private readonly Func<string, ICommand?> _resolveCommand;
    private readonly Action<SchachtRecord, KonsolidiertesSchachtFeld, string?> _commit;
    private readonly Func<bool> _canResolveDropdowns;

    internal SchaechteRecordDetailsBuilder(
        Func<string, IEnumerable<string>> resolveOptions,
        Func<string, ICommand?> resolveCommand,
        Action<SchachtRecord, KonsolidiertesSchachtFeld, string?> commit,
        Func<bool>? canResolveDropdowns = null)
    {
        _resolveOptions = resolveOptions ?? throw new ArgumentNullException(nameof(resolveOptions));
        _resolveCommand = resolveCommand ?? throw new ArgumentNullException(nameof(resolveCommand));
        _commit = commit ?? throw new ArgumentNullException(nameof(commit));
        _canResolveDropdowns = canResolveDropdowns ?? (() => true);
    }

    internal List<RecordDetailGroup> Build(
        IEnumerable<string> templateColumns,
        SchachtRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var groups = new List<RecordDetailGroup>();
        var buckets = new Dictionary<string, List<RecordDetailItem>>(StringComparer.Ordinal)
        {
            ["Stammdaten"] = [],
            ["Zustand und Inspektion"] = [],
            ["Sanierung und Kosten"] = [],
            ["Dokumente und Medien"] = [],
            ["Weitere Angaben"] = []
        };

        var consolidated = SchachtDetailFeldKonsolidierer.Konsolidiere(
            templateColumns,
            record.Fields);
        RecordDetailItem? renovationSwitch = null;
        var renovationDependents = new List<RecordDetailItem>();

        foreach (var field in consolidated)
        {
            var groupName = ResolveSchachtDetailGroup(field.AnzeigeName);
            var item = CreateItem(field, record);
            buckets[groupName].Add(item);

            if (string.Equals(ResolveOptionField(field.AnzeigeName), "Sanieren_JaNein", StringComparison.Ordinal))
                renovationSwitch = item;
            else if (string.Equals(groupName, "Sanierung und Kosten", StringComparison.Ordinal))
                renovationDependents.Add(item);
        }

        WireRenovationVisibility(renovationSwitch, renovationDependents);
        AddGroup(groups, buckets, "Stammdaten", "Identifikation und Lage des Schachts.");
        AddGroup(groups, buckets, "Zustand und Inspektion", "Bewertung, Schaeden und Pruefresultate.");
        AddGroup(groups, buckets, "Sanierung und Kosten", "Massnahmen, Kosten und Mengenangaben.");
        AddGroup(groups, buckets, "Dokumente und Medien", "Verknuepfte Dateien, PDFs und Links.");
        AddGroup(groups, buckets, "Weitere Angaben", "Felder ohne klare Zuordnung.");
        return groups;
    }

    internal static void WireRenovationVisibility(
        RecordDetailItem? renovationSwitch,
        IReadOnlyList<RecordDetailItem> dependents)
    {
        if (renovationSwitch is null || dependents.Count == 0)
            return;

        void Apply()
        {
            var visible = !string.Equals(
                renovationSwitch.Value?.Trim(),
                "Nein",
                StringComparison.OrdinalIgnoreCase);
            foreach (var item in dependents)
                item.IsVisible = visible;
        }

        Apply();
        renovationSwitch.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RecordDetailItem.Value))
                Apply();
        };
    }

    private RecordDetailItem CreateItem(
        KonsolidiertesSchachtFeld field,
        SchachtRecord record)
    {
        var label = GetDisplayHeader(field.AnzeigeName);
        var highlightKind = RecordDetailHighlightPolicy.Resolve(field.AnzeigeName);
        void Commit(string? value) => _commit(record, field, value);

        if (_canResolveDropdowns() && TryResolveDropdownColumnSpec(field.AnzeigeName, out var spec))
        {
            return new RecordDetailItem(
                label,
                field.Wert,
                commitValue: Commit,
                isCombo: true,
                allowFreeText: spec.AllowFreeText,
                options: _resolveOptions(spec.ItemsSourcePath),
                editOptionsCommand: spec.Managed ? _resolveCommand(spec.EditCommand) : null,
                previewOptionsCommand: spec.Managed ? _resolveCommand(spec.PreviewCommand) : null,
                resetOptionsCommand: spec.Managed ? _resolveCommand(spec.ResetCommand) : null,
                addOptionCommand: spec.Managed ? _resolveCommand(spec.AddCommand) : null,
                removeOptionCommand: spec.Managed ? _resolveCommand(spec.RemoveCommand) : null,
                highlightKind: highlightKind);
        }

        var normalized = Normalize(field.AnzeigeName);
        var isMultiline = IsPrimaryDamagesColumn(field.AnzeigeName)
                          || normalized.Contains("bemerk", StringComparison.Ordinal);
        if (IsZustandsklasseColumn(field.AnzeigeName))
        {
            return new RecordDetailItem(
                label,
                field.Wert,
                commitValue: Commit,
                isCombo: true,
                allowFreeText: false,
                options: ZustandsklasseColorPalette.SelectionOptions,
                highlightKind: highlightKind);
        }

        return new RecordDetailItem(
            label,
            field.Wert,
            commitValue: Commit,
            isMultiline: isMultiline,
            highlightKind: highlightKind);
    }

    private static void AddGroup(
        ICollection<RecordDetailGroup> groups,
        IReadOnlyDictionary<string, List<RecordDetailItem>> buckets,
        string title,
        string description)
    {
        if (buckets.TryGetValue(title, out var items) && items.Count > 0)
            groups.Add(new RecordDetailGroup(title, description, items));
    }
}
