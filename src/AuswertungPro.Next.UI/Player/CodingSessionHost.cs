using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingSessionHost
{
    bool HasViewModel { get; }
    double CurrentMeter { get; }
    double EndMeter { get; }
    OverlayGeometry? CurrentOverlay { get; }
    ObservableCollection<CodingEvent>? EventCollection { get; }
    IEnumerable<CodingEvent> Events { get; }
    CodingEvent? SelectedDefect { get; }
    string? HaltungName { get; }
    string? VideoPath { get; }
    TimeSpan? CurrentVideoTime { get; }
    string SelectedCode { get; }
    string SelectedCodeDescription { get; }
    void SetCurrentVideoTime(TimeSpan videoTime);
    void SelectDefect(CodingEvent? codingEvent);
    void ClearSelectedDefect();
    void ClearCurrentOverlay();
    void ClearSelectedCode();
    bool ExecuteMoveNext();
    bool ExecuteMovePrevious();
    bool ExecuteAcceptDefect();
    bool ExecuteEditDefect();
}

public sealed class CodingSessionHost : ICodingSessionHost
{
    private static readonly CodingEvent[] EmptyEvents = [];
    private readonly Func<CodingSessionViewModel?> _resolveViewModel;

    public CodingSessionHost(Func<CodingSessionViewModel?> resolveViewModel)
    {
        ArgumentNullException.ThrowIfNull(resolveViewModel);
        _resolveViewModel = resolveViewModel;
    }

    private CodingSessionViewModel? ViewModel => _resolveViewModel();

    public bool HasViewModel => ViewModel is not null;

    public double CurrentMeter => ViewModel?.CurrentMeter ?? 0;

    public double EndMeter => ViewModel?.EndMeter ?? 0;

    public OverlayGeometry? CurrentOverlay => ViewModel?.CurrentOverlay;

    public ObservableCollection<CodingEvent>? EventCollection => ViewModel?.Events;

    public IEnumerable<CodingEvent> Events
    {
        get
        {
            var eventCollection = EventCollection;
            return eventCollection is null ? EmptyEvents : eventCollection;
        }
    }

    public string SelectedCode => ViewModel?.SelectedCode ?? string.Empty;

    public string SelectedCodeDescription => ViewModel?.SelectedCodeDescription ?? string.Empty;

    public CodingEvent? SelectedDefect => ViewModel?.SelectedDefect;

    public string? HaltungName => ViewModel?.HaltungName;

    public string? VideoPath => ViewModel?.VideoPath;

    public TimeSpan? CurrentVideoTime => ViewModel?.CurrentVideoTime;

    public void SetCurrentVideoTime(TimeSpan videoTime)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return;

        viewModel.CurrentVideoTime = videoTime;
    }

    public void SelectDefect(CodingEvent? codingEvent)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return;

        viewModel.SelectedDefect = codingEvent;
    }

    public void ClearSelectedDefect()
        => SelectDefect(null);

    public void ClearCurrentOverlay()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return;

        viewModel.CurrentOverlay = null;
    }

    public void ClearSelectedCode()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return;

        viewModel.SelectedCode = string.Empty;
        viewModel.SelectedCodeDescription = string.Empty;
    }

    public bool ExecuteMoveNext()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return false;

        viewModel.MoveNextCommand.Execute(null);
        return true;
    }

    public bool ExecuteMovePrevious()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return false;

        viewModel.MovePreviousCommand.Execute(null);
        return true;
    }

    public bool ExecuteAcceptDefect()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return false;

        viewModel.AcceptDefectCommand.Execute(null);
        return true;
    }

    public bool ExecuteEditDefect()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
            return false;

        viewModel.EditDefectCommand.Execute(null);
        return true;
    }
}
