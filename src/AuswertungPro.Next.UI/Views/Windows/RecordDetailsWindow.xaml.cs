using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class RecordDetailsWindow : Window
{
    public IReadOnlyList<RecordDetailGroup> Groups { get; }
    public string Header { get; }
    public string SubHeader { get; }
    public ICommand CloseCommand { get; }
    public ICommand? SuggestMeasuresCommand { get; }

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
        Loaded += (_, _) => EnsureVisibleOnScreen();
        // W01: Ein Konflikt aus diesem Fenster erscheint hier, nicht in Liste/Schublade dahinter.
        var konfliktAnzeige = FormularKonfliktAnzeige.Verbinde(Groups, text => Hinweis = text);
        Closed += (_, _) => konfliktAnzeige.Dispose();
    }

    public static readonly DependencyProperty HinweisProperty = DependencyProperty.Register(
        nameof(Hinweis), typeof(string), typeof(RecordDetailsWindow), new PropertyMetadata(string.Empty));

    /// <summary>Konflikthinweis (W01) unter dem Fensterkopf; leer = keiner.</summary>
    public string Hinweis
    {
        get => (string)GetValue(HinweisProperty);
        private set => SetValue(HinweisProperty, value);
    }

    private void EnsureVisibleOnScreen() => WindowBoundsHelper.EnsureVisibleOnScreen(this);

    private sealed class CloseWindowCommand : ICommand
    {
        private readonly Window _window;
        public CloseWindowCommand(Window window) => _window = window;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _window.Close();
    }
}
