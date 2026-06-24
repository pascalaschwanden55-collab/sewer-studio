using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventsRefreshWorkflowTests
{
    [Fact]
    public void RefreshListAndStatistics_sorts_events_rebinds_list_and_updates_statistics()
    {
        RunOnStaThread(() =>
        {
            var early = Event("EARLY", meter: 1.2, timestamp: TimeSpan.FromSeconds(5), confidence: 0.80);
            var late = Event("LATE", meter: 3.4, timestamp: TimeSpan.FromSeconds(2), confidence: null);
            var events = new ObservableCollection<CodingEvent> { late, early };
            var listBox = new ListBox { ItemsSource = events, SelectedItem = late };
            var listControls = new CodingEventsListControls(listBox);
            var total = new Run();
            var open = new Run();
            var autoAccepted = new TextBlock();
            var pending = new TextBlock();
            var reviewRequired = new TextBlock();
            var averageConfidence = new TextBlock();
            var statisticsControls = new CodingStatisticsControls(
                total,
                open,
                autoAccepted,
                pending,
                reviewRequired,
                averageConfidence);
            var method = FindRefreshListAndStatisticsMethod();
            Assert.NotNull(method);

            var refreshed = method.Invoke(null, [
                events,
                listControls,
                statisticsControls,
                new Func<CodingEvent, DefectStatus>(_ => DefectStatus.Pending)
            ]);

            Assert.Equal(true, refreshed);
            Assert.Equal(new[] { early, late }, events);
            Assert.Same(events, listBox.ItemsSource);
            Assert.Same(late, listBox.SelectedItem);
            Assert.Equal("2", total.Text);
            Assert.Equal("1", open.Text);
            Assert.Equal("0", autoAccepted.Text);
            Assert.Equal("1", pending.Text);
            Assert.Equal("0", reviewRequired.Text);
            Assert.Equal("80%", averageConfidence.Text);
        });
    }

    [Fact]
    public void RefreshStatistics_updates_statistics_without_reordering_events()
    {
        RunOnStaThread(() =>
        {
            var first = Event("FIRST", meter: 3.4, timestamp: TimeSpan.FromSeconds(2), confidence: null);
            var second = Event("SECOND", meter: 1.2, timestamp: TimeSpan.FromSeconds(5), confidence: 0.75);
            var events = new ObservableCollection<CodingEvent> { first, second };
            var total = new Run();
            var open = new Run();
            var autoAccepted = new TextBlock();
            var pending = new TextBlock();
            var reviewRequired = new TextBlock();
            var averageConfidence = new TextBlock();
            var statisticsControls = new CodingStatisticsControls(
                total,
                open,
                autoAccepted,
                pending,
                reviewRequired,
                averageConfidence);
            var method = FindRefreshStatisticsMethod();
            Assert.NotNull(method);

            var refreshed = method.Invoke(null, [
                events,
                statisticsControls,
                new Func<CodingEvent, DefectStatus>(_ => DefectStatus.AutoAccepted)
            ]);

            Assert.Equal(true, refreshed);
            Assert.Equal(new[] { first, second }, events);
            Assert.Equal("2", total.Text);
            Assert.Equal("0", open.Text);
            Assert.Equal("1", autoAccepted.Text);
            Assert.Equal("0", pending.Text);
            Assert.Equal("0", reviewRequired.Text);
            Assert.Equal("75%", averageConfidence.Text);
        });
    }

    [Fact]
    public void RefreshListAndStatistics_returns_false_without_events()
    {
        RunOnStaThread(() =>
        {
            var listControls = new CodingEventsListControls(new ListBox());
            var statisticsControls = EmptyStatisticsControls();
            var method = FindRefreshListAndStatisticsMethod();
            Assert.NotNull(method);

            var refreshed = method.Invoke(null, [
                null,
                listControls,
                statisticsControls,
                new Func<CodingEvent, DefectStatus>(_ => throw new InvalidOperationException("Should not resolve status."))
            ]);

            Assert.Equal(false, refreshed);
        });
    }

    private static CodingEvent Event(
        string code,
        double meter,
        TimeSpan timestamp,
        double? confidence)
        => new()
        {
            MeterAtCapture = meter,
            VideoTimestamp = timestamp,
            Entry = new ProtocolEntry { Code = code },
            AiContext = confidence.HasValue
                ? new CodingEventAiContext { Confidence = confidence.Value }
                : null
        };

    private static CodingStatisticsControls EmptyStatisticsControls()
        => new(new Run(), new Run(), new TextBlock(), new TextBlock(), new TextBlock(), new TextBlock());

    private static Type? WorkflowType
        => typeof(CodingStatisticsPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingEventsRefreshWorkflow");

    private static MethodInfo? FindRefreshListAndStatisticsMethod()
        => WorkflowType?.GetMethod(
            "RefreshListAndStatistics",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(ObservableCollection<CodingEvent>),
                typeof(CodingEventsListControls),
                typeof(CodingStatisticsControls),
                typeof(Func<CodingEvent, DefectStatus>)
            ],
            modifiers: null);

    private static MethodInfo? FindRefreshStatisticsMethod()
        => WorkflowType?.GetMethod(
            "RefreshStatistics",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(IEnumerable<CodingEvent>),
                typeof(CodingStatisticsControls),
                typeof(Func<CodingEvent, DefectStatus>)
            ],
            modifiers: null);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
