using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerBreadcrumbPresentation(
    IReadOnlyList<VsaCodeExplorerBreadcrumbElement> Elements);

public sealed record VsaCodeExplorerBreadcrumbElement(
    bool IsSeparator,
    string Text,
    int Level,
    bool CanNavigate,
    bool IsCurrent);

public static class VsaCodeExplorerBreadcrumbPresenter
{
    public static VsaCodeExplorerBreadcrumbPresentation Build(IEnumerable<BreadcrumbItem> items)
    {
        var breadcrumbs = items.ToArray();
        var elements = new List<VsaCodeExplorerBreadcrumbElement>(breadcrumbs.Length * 2);

        for (var index = 0; index < breadcrumbs.Length; index++)
        {
            if (index > 0)
                elements.Add(new VsaCodeExplorerBreadcrumbElement(true, "\u203A", -1, false, false));

            var item = breadcrumbs[index];
            var isCurrent = index == breadcrumbs.Length - 1;
            elements.Add(new VsaCodeExplorerBreadcrumbElement(
                IsSeparator: false,
                Text: item.Label,
                Level: item.Level,
                CanNavigate: !isCurrent,
                IsCurrent: isCurrent));
        }

        return new VsaCodeExplorerBreadcrumbPresentation(elements);
    }
}
