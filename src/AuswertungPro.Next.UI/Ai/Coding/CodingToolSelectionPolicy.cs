using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingToolSelectionState(
    bool IsActive,
    string? ActiveToolName,
    OverlayToolType ActiveTool,
    SchemaType? ActiveSchemaType,
    LevelMode? LevelModeToApply,
    string LabelText);

public static class CodingToolSelectionPolicy
{
    public static CodingToolSelectionState Build(
        string? currentToolName,
        string buttonName,
        string? buttonLabel,
        OverlayToolType requestedTool,
        SchemaType? requestedSchemaType,
        LevelMode? requestedLevelMode)
    {
        var activate = !string.Equals(currentToolName, buttonName, StringComparison.Ordinal);
        var label = string.IsNullOrWhiteSpace(buttonLabel)
            ? requestedTool.ToString()
            : buttonLabel;

        return new CodingToolSelectionState(
            IsActive: activate,
            ActiveToolName: activate ? buttonName : null,
            ActiveTool: activate ? requestedTool : OverlayToolType.None,
            ActiveSchemaType: activate ? requestedSchemaType : null,
            LevelModeToApply: activate ? requestedLevelMode : null,
            LabelText: activate ? label : "");
    }
}
