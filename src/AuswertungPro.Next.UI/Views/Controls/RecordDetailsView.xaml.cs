using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Controls;

public partial class RecordDetailsView : UserControl
{
    private static readonly Regex NonNumericRegex = new("[^0-9]", RegexOptions.Compiled);

    public RecordDetailsView() => InitializeComponent();

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(RecordDetailsView),
            new PropertyMetadata("Details"));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty SubHeaderProperty =
        DependencyProperty.Register(nameof(SubHeader), typeof(string), typeof(RecordDetailsView),
            new PropertyMetadata(string.Empty));

    public string SubHeader
    {
        get => (string)GetValue(SubHeaderProperty);
        set => SetValue(SubHeaderProperty, value);
    }

    public static readonly DependencyProperty GroupsProperty =
        DependencyProperty.Register(nameof(Groups), typeof(IReadOnlyList<RecordDetailGroup>), typeof(RecordDetailsView),
            new PropertyMetadata(null));

    public IReadOnlyList<RecordDetailGroup>? Groups
    {
        get => (IReadOnlyList<RecordDetailGroup>?)GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    public static readonly DependencyProperty SuggestMeasuresCommandProperty =
        DependencyProperty.Register(nameof(SuggestMeasuresCommand), typeof(ICommand), typeof(RecordDetailsView),
            new PropertyMetadata(null));

    public ICommand? SuggestMeasuresCommand
    {
        get => (ICommand?)GetValue(SuggestMeasuresCommandProperty);
        set => SetValue(SuggestMeasuresCommandProperty, value);
    }

    private void EditorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = e;
        UpdateComboBindingSource(sender as ComboBox);
    }

    private void EditorComboBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = e;
        UpdateComboBindingSource(sender as ComboBox);
    }

    private static void UpdateComboBindingSource(ComboBox? comboBox)
    {
        if (comboBox?.DataContext is not RecordDetailItem item)
            return;

        var property = item.AllowFreeText ? ComboBox.TextProperty : Selector.SelectedItemProperty;
        comboBox.GetBindingExpression(property)?.UpdateSource();
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not RecordDetailItem item || !item.DigitsOnly)
            return;

        e.Handled = NonNumericRegex.IsMatch(e.Text ?? string.Empty);
    }

    private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not RecordDetailItem item || !item.DigitsOnly)
            return;

        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        if (NonNumericRegex.IsMatch(text))
            e.CancelCommand();
    }
}
