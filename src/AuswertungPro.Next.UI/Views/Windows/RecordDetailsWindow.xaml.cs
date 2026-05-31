using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
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

    private sealed class CloseWindowCommand : ICommand
    {
        private readonly Window _window;
        public CloseWindowCommand(Window window) => _window = window;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _window.Close();
    }
}
