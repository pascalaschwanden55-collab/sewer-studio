namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerClockPresetResult(
    bool ShouldApply,
    string ClockVonText,
    string ClockBisText);

public static class VsaCodeExplorerClockPresetWorkflow
{
    public static VsaCodeExplorerClockPresetResult Resolve(string? tag)
    {
        if (tag is null)
            return Ignored();

        var parts = tag.Split(',');
        if (parts.Length != 2)
            return Ignored();

        return new VsaCodeExplorerClockPresetResult(
            ShouldApply: true,
            ClockVonText: parts[0],
            ClockBisText: parts[1]);
    }

    private static VsaCodeExplorerClockPresetResult Ignored()
        => new(false, "", "");
}
