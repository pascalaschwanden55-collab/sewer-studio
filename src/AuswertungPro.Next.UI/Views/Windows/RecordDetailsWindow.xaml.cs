using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class RecordDetailsWindow : Window
{
    private static readonly Regex NonNumericRegex = new("[^0-9]", RegexOptions.Compiled);

    public IReadOnlyList<RecordDetailGroup> Groups { get; }
    public string Header { get; }
    public string SubHeader { get; }
    public ICommand CloseCommand { get; }
    public ICommand? SuggestMeasuresCommand { get; }
    public Visibility SuggestMeasuresVisibility => SuggestMeasuresCommand is not null ? Visibility.Visible : Visibility.Collapsed;

    public RecordDetailsWindow(
        string title,
        string header,
        string subHeader,
        IReadOnlyList<RecordDetailGroup> groups,
        ICommand? suggestMeasuresCommand = null)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        Title = string.IsNullOrWhiteSpace(title) ? "Details" : title;
        Header = string.IsNullOrWhiteSpace(header) ? "Details" : header;
        SubHeader = subHeader ?? string.Empty;
        Groups = groups ?? [];
        CloseCommand = new CloseWindowCommand(this);
        SuggestMeasuresCommand = suggestMeasuresCommand;
        DataContext = this;
        Loaded += (_, _) => EnsureVisibleOnScreen();
    }

    private void EnsureVisibleOnScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width) Width = area.Width - 20;
        if (Height > area.Height) Height = area.Height - 20;
        if (Left < area.Left) Left = area.Left;
        if (Top < area.Top) Top = area.Top;
        if (Left + Width > area.Right) Left = area.Right - Width;
        if (Top + Height > area.Bottom) Top = area.Bottom - Height;
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

    private sealed class CloseWindowCommand : ICommand
    {
        private readonly Window _window;

        public CloseWindowCommand(Window window)
        {
            _window = window;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            _ = parameter;
            return true;
        }

        public void Execute(object? parameter)
        {
            _ = parameter;
            _window.Close();
        }
    }
}
