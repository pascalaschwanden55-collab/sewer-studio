namespace AuswertungPro.Next.Application.Diagnostics;

public sealed record LogTailReadResult(
    bool FileExists,
    IReadOnlyList<string> Lines,
    string? UserMessage);

/// <summary>Liest einen begrenzten Ausschnitt des aktuellen Tageslogs.</summary>
public interface ILogTailReader
{
    LogTailReadResult ReadToday(int maximumLines = 200);
}
