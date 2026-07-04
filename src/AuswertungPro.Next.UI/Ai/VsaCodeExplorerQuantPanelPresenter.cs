using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerQuantPanelPresentation(
    bool ShowNoQuant,
    VsaCodeExplorerQuantFieldPresentation Q1,
    VsaCodeExplorerQuantFieldPresentation Q2);

public enum VsaCodeExplorerQuantBrushRole
{
    Danger
}

public sealed record VsaCodeExplorerQuantRequiredBadgePresentation(
    string Text,
    VsaCodeExplorerQuantBrushRole BrushRole,
    double BackgroundOpacity);

public sealed record VsaCodeExplorerQuantFieldPresentation(
    bool ShowPanel,
    string LabelText,
    string UnitText,
    string RangeText,
    bool ShowRequiredBadge,
    VsaCodeExplorerQuantRequiredBadgePresentation? RequiredBadge);

public static class VsaCodeExplorerQuantPanelPresenter
{
    public static VsaCodeExplorerQuantPanelPresentation Build(QuantField? q1, QuantField? q2)
    {
        var noQuant = q1 is null && q2 is null;
        return new VsaCodeExplorerQuantPanelPresentation(
            ShowNoQuant: noQuant,
            Q1: BuildField("Q1", q1, includeRange: true),
            Q2: BuildField("Q2", q2, includeRange: false));
    }

    private static VsaCodeExplorerQuantFieldPresentation BuildField(
        string prefix,
        QuantField? field,
        bool includeRange)
    {
        if (field is null)
            return new VsaCodeExplorerQuantFieldPresentation(false, "", "", "", false, null);

        var requiredBadge = field.Pflicht == "P"
            ? new VsaCodeExplorerQuantRequiredBadgePresentation(
                Text: "PFLICHT",
                BrushRole: VsaCodeExplorerQuantBrushRole.Danger,
                BackgroundOpacity: 0.12)
            : null;

        return new VsaCodeExplorerQuantFieldPresentation(
            ShowPanel: true,
            LabelText: $"{prefix}: {field.Label}",
            UnitText: field.Einheit ?? "",
            RangeText: includeRange ? FormatRange(field) : "",
            ShowRequiredBadge: requiredBadge is not null,
            RequiredBadge: requiredBadge);
    }

    private static string FormatRange(QuantField field)
    {
        var rangeText = "";
        if (field.Min.HasValue && field.Max.HasValue)
            rangeText = $"[{field.Min}\u2013{field.Max}]";
        else if (field.Min.HasValue)
            rangeText = $">= {field.Min}";
        else if (field.Max.HasValue)
            rangeText = $"<= {field.Max}";

        if (field.Hint is not null)
            rangeText += (rangeText.Length > 0 ? " " : "") + field.Hint;

        return rangeText;
    }
}
