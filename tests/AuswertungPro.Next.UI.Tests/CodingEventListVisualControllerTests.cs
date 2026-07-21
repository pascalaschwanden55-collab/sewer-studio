using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventListVisualControllerTests
{
    [Fact]
    public void ColorizeCodingEvents_updates_event_visuals_and_both_match_lists()
    {
        RunOnStaThread(() =>
        {
            var codingEvent = CreateEvent("BAB", 0.876);
            var importEvent = CreateEvent("BCA", confidence: null);
            var codingEvents = CreateList(codingEvent);
            var importEvents = CreateList(
                importEvent,
                CreateEvent("BAA", confidence: null));
            var protocolMatchState = new CodingProtocolMatchStateController();

            var panel = new StackPanel();
            panel.Children.Add(codingEvents);
            panel.Children.Add(importEvents);
            var window = new Window
            {
                Width = 420,
                Height = 300,
                Content = panel
            };

            try
            {
                window.Show();
                window.Dispatcher.Invoke(DispatcherPriority.Loaded, () => { });
                window.UpdateLayout();
                codingEvents.UpdateLayout();
                importEvents.UpdateLayout();
                window.Dispatcher.Invoke(DispatcherPriority.ContextIdle, () => { });

                var importContainer = Assert.IsType<ListBoxItem>(
                    importEvents.ItemContainerGenerator.ContainerFromIndex(0));
                var secondImportContainer = Assert.IsType<ListBoxItem>(
                    importEvents.ItemContainerGenerator.ContainerFromIndex(1));
                importContainer.Background = Brushes.Red;
                importContainer.ToolTip = "alt";
                secondImportContainer.Background = Brushes.Red;
                secondImportContainer.ToolTip = "alt";

                var controller = new CodingEventListVisualController(
                    codingEvents,
                    importEvents,
                    protocolMatchState);
                protocolMatchState.Buckets[codingEvent.Entry.EntryId] =
                    CodingProtocolMatchBucket.TrainingGreen;

                controller.ColorizeCodingEvents();

                var codingContainer = Assert.IsType<ListBoxItem>(
                    codingEvents.ItemContainerGenerator.ContainerFromIndex(0));
                var zoneDot = FindRequired<Ellipse>(codingContainer, "ZoneDot");
                var confidence = FindRequired<TextBlock>(codingContainer, "TxtConfidence");
                var statusIcon = FindRequired<TextBlock>(codingContainer, "TxtStatusIcon");
                var codingBadge = FindRequired<Border>(codingContainer, "CodingMatchBadge");
                var codingBadgeText = FindRequired<TextBlock>(
                    codingContainer,
                    "TxtCodingMatchBadge");
                var importBadge = FindRequired<Border>(importContainer, "CodingMatchBadge");
                var secondImportBadge = FindRequired<Border>(
                    secondImportContainer,
                    "CodingMatchBadge");

                Assert.NotNull(zoneDot.Fill);
                Assert.Equal("88%", confidence.Text);
                Assert.Equal(
                    CodingDefectStatusDisplayPolicy.StatusIcon(
                        DefectStatusPolicy.GetStatus(codingEvent)),
                    statusIcon.Text);
                Assert.Equal(
                    CodingProtocolMatchDisplayPolicy.BackgroundColor(
                        CodingProtocolMatchBucket.TrainingGreen),
                    Assert.IsType<SolidColorBrush>(codingContainer.Background).Color);
                Assert.Equal(Visibility.Visible, codingBadge.Visibility);
                Assert.Equal(
                    CodingProtocolMatchDisplayPolicy.BadgeText(
                        CodingProtocolMatchBucket.TrainingGreen),
                    codingBadgeText.Text);
                Assert.Equal(
                    DependencyProperty.UnsetValue,
                    importContainer.ReadLocalValue(Control.BackgroundProperty));
                Assert.Equal(
                    DependencyProperty.UnsetValue,
                    importContainer.ReadLocalValue(FrameworkElement.ToolTipProperty));
                Assert.Equal(Visibility.Collapsed, importBadge.Visibility);
                Assert.Equal(
                    DependencyProperty.UnsetValue,
                    secondImportContainer.ReadLocalValue(Control.BackgroundProperty));
                Assert.Equal(
                    DependencyProperty.UnsetValue,
                    secondImportContainer.ReadLocalValue(FrameworkElement.ToolTipProperty));
                Assert.Equal(Visibility.Collapsed, secondImportBadge.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ApplyProtocolMatchHighlights_skips_unrealized_items_safely()
    {
        RunOnStaThread(() =>
        {
            var codingEvents = CreateList(CreateEvent("BAB", confidence: null));
            var importEvents = CreateList(CreateEvent("BCA", confidence: null));
            var controller = new CodingEventListVisualController(
                codingEvents,
                importEvents,
                new CodingProtocolMatchStateController());

            controller.ApplyProtocolMatchHighlights();
        });
    }

    private static ListBox CreateList(params CodingEvent[] codingEvents)
    {
        var template = Assert.IsType<DataTemplate>(XamlReader.Parse("""
            <DataTemplate
                xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <StackPanel>
                    <Ellipse x:Name="ZoneDot" Width="8" Height="8" />
                    <TextBlock x:Name="TxtConfidence" />
                    <TextBlock x:Name="TxtStatusIcon" />
                    <Border x:Name="CodingMatchBadge" />
                    <TextBlock x:Name="TxtCodingMatchBadge" />
                </StackPanel>
            </DataTemplate>
            """));

        var listBox = new ListBox
        {
            Width = 360,
            Height = 100,
            ItemTemplate = template
        };
        VirtualizingPanel.SetIsVirtualizing(listBox, false);
        foreach (var codingEvent in codingEvents)
            listBox.Items.Add(codingEvent);
        return listBox;
    }

    private static CodingEvent CreateEvent(string code, double? confidence)
        => new()
        {
            Entry = new ProtocolEntry
            {
                EntryId = Guid.NewGuid(),
                Code = code,
                Beschreibung = code
            },
            AiContext = confidence.HasValue
                ? new CodingEventAiContext { Confidence = confidence.Value }
                : null
        };

    private static T FindRequired<T>(DependencyObject root, string name)
        where T : FrameworkElement
        => Assert.IsType<T>(VisualTreeSafe.FindNamedDescendant<T>(root, name));

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
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
