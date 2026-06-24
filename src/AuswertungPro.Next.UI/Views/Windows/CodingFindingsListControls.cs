using System;
using System.Windows.Controls;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public static class CodingFindingsListControls
{
    public static void ShowFindings(
        ItemsControl findingsList,
        IEnumerable<LiveFrameFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findingsList);
        ArgumentNullException.ThrowIfNull(findings);

        findingsList.ItemsSource = AiFindingDisplayItemFactory.ForFindings(findings);
    }

    public static void ShowPossibleBoundary(
        ItemsControl findingsList,
        string? code,
        string label)
    {
        ArgumentNullException.ThrowIfNull(findingsList);

        findingsList.ItemsSource = AiFindingDisplayItemFactory.ForPossibleBoundary(code, label);
    }

    public static void ShowBoundary(
        ItemsControl findingsList,
        string? code,
        string label)
    {
        ArgumentNullException.ThrowIfNull(findingsList);

        findingsList.ItemsSource = AiFindingDisplayItemFactory.ForBoundary(code, label);
    }

    public static void ShowResolvedFinding(
        ItemsControl findingsList,
        LiveFrameFinding finding,
        string resolvedCode)
    {
        ArgumentNullException.ThrowIfNull(findingsList);

        findingsList.ItemsSource = AiFindingDisplayItemFactory.ForResolvedFinding(finding, resolvedCode);
    }
}
