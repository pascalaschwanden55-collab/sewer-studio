using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record VsaCodeExplorerInitialTimePresentation(
    string? TextBoxText,
    string? ViewModelZeit)
{
    public bool ShouldSetTextBox => TextBoxText is not null;

    public bool ShouldUpdateViewModel => ViewModelZeit is not null;
}

public static class VsaCodeExplorerInitialTimeWorkflow
{
    public static VsaCodeExplorerInitialTimePresentation Build(
        string? existingZeit,
        TimeSpan? currentVideoTime)
    {
        if (!string.IsNullOrWhiteSpace(existingZeit))
            return new VsaCodeExplorerInitialTimePresentation(existingZeit, ViewModelZeit: null);

        if (currentVideoTime is { } time && time > TimeSpan.Zero)
        {
            var formatted = ProtocolEntryInputNormalizer.FormatTime(time);
            return new VsaCodeExplorerInitialTimePresentation(formatted, formatted);
        }

        return new VsaCodeExplorerInitialTimePresentation(TextBoxText: null, ViewModelZeit: null);
    }
}
