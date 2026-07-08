using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

public partial class SchachtansichtView : UserControl
{
    private const double SchadenHeightMin = 80d;
    private const double SchadenHeightMax = 2000d;
    private AppSettings? _settings;
    private Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>>? _detailBuilder;
    private Func<SchachtRecord, IReadOnlyList<SchachtDamageLine>>? _damageLineBuilder;
    private SchachtRecord? _subscribedRecord;

    public SchachtansichtView()
    {
        InitializeComponent();
        RestoreSchadenHeight();
        IsVisibleChanged += (_, _) => RefreshAll();
        Unloaded += (_, _) => SubscribeSelectedRecord(null);
    }

    public AppSettings? Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            RestoreSchadenHeight();
        }
    }

    public Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>>? DetailBuilder
    {
        get => _detailBuilder;
        set
        {
            _detailBuilder = value;
            RefreshAll();
        }
    }

    public Func<SchachtRecord, IReadOnlyList<SchachtDamageLine>>? DamageLineBuilder
    {
        get => _damageLineBuilder;
        set
        {
            _damageLineBuilder = value;
            RefreshAll();
        }
    }

    public IReadOnlyList<string> ZustandsklasseOptions => ZustandsklasseColorPalette.SelectionOptions;

    public Action<string, SchachtRecord>? ActionRequested { get; set; }

    private void SchachtList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        SubscribeSelectedRecord(SchachtList.SelectedItem as SchachtRecord);
        RefreshAll();
    }

    private void SubscribeSelectedRecord(SchachtRecord? record)
    {
        if (_subscribedRecord is not null)
            _subscribedRecord.PropertyChanged -= SelectedRecord_PropertyChanged;

        _subscribedRecord = record;
        if (_subscribedRecord is not null)
            _subscribedRecord.PropertyChanged += SelectedRecord_PropertyChanged;
    }

    private void SelectedRecord_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshAll();
    }

    private void RefreshAll()
    {
        if (!IsVisible)
            return;

        if (SchachtList.SelectedItem is not SchachtRecord record || DetailBuilder is null)
        {
            Detail.Header = "Kein Schacht gewaehlt";
            Detail.SubHeader = "Links einen Schacht waehlen.";
            Detail.Groups = Array.Empty<RecordDetailGroup>();
            DamageList.ItemsSource = Array.Empty<SchachtDamageLine>();
            return;
        }

        var number = record.GetFieldValue("Schachtnummer");
        Detail.Header = string.IsNullOrWhiteSpace(number) ? "Schachtdetails" : $"Schacht {number}";
        Detail.SubHeader = "Alle Felder editierbar - Aenderungen erscheinen sofort in der Tabelle.";
        Detail.Groups = DetailBuilder(record);
        DamageList.ItemsSource = DamageLineBuilder?.Invoke(record) ?? Array.Empty<SchachtDamageLine>();
    }

    private void RestoreSchadenHeight()
    {
        var height = _settings?.SchachtansichtSchadenHeight ?? double.NaN;
        if (double.IsNaN(height) || height <= 0)
            return;

        SchadenRowDef.Height = new GridLength(
            Math.Clamp(height, SchadenHeightMin, SchadenHeightMax),
            GridUnitType.Pixel);
    }

    private void SchadenSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _ = sender;
        _ = e;
        var settings = _settings;
        if (settings is null)
            return;

        var height = Math.Clamp(SchadenRowDef.ActualHeight, SchadenHeightMin, SchadenHeightMax);
        if (Math.Abs(settings.SchachtansichtSchadenHeight - height) < 0.5d)
            return;

        settings.SchachtansichtSchadenHeight = height;
        settings.Save();
    }

    private void SchachtList_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        var dep = e.OriginalSource as DependencyObject;
        while (dep is not null and not ListBoxItem)
            dep = AuswertungPro.Next.UI.Behaviors.VisualTreeSafe.GetParentSafe(dep);

        if (dep is ListBoxItem { DataContext: SchachtRecord record })
            SchachtList.SelectedItem = record;
    }

    private void RaiseAction(string actionKey)
    {
        if (SchachtList.SelectedItem is SchachtRecord record)
            ActionRequested?.Invoke(actionKey, record);
    }

    private void DamageDetails_Click(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseAction("details");
    }

    private void DamageList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        _ = e;
        RaiseAction("details");
    }

    private void ZustandsklasseValue_Click(object sender, RoutedEventArgs e)
    {
        _ = e;
        if (sender is Button { Tag: string value })
            RaiseAction($"zustandsklasse:{value}");
    }

    private void CtxMoveUp_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("moveup"); }
    private void CtxMoveDown_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("movedown"); }
    private void CtxDetails_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("details"); }
    private void CtxOpenPdf_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("openpdf"); }
    private void CtxOpenFolder_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("openfolder"); }
    private void CtxDelete_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("delete"); }
}
