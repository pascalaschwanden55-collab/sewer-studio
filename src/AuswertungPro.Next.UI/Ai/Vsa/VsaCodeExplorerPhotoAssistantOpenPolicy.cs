using System.IO;

namespace AuswertungPro.Next.UI.Ai.Vsa;

public sealed record VsaCodeExplorerPhotoAssistantOpenDecision(
    bool CanOpen,
    string? PhotoPath,
    string Message,
    string Title);

public static class VsaCodeExplorerPhotoAssistantOpenPolicy
{
    public static VsaCodeExplorerPhotoAssistantOpenDecision Resolve(
        IReadOnlyList<string> photoPaths,
        int photoIndex)
        => Resolve(photoPaths, photoIndex, File.Exists);

    public static VsaCodeExplorerPhotoAssistantOpenDecision Resolve(
        IReadOnlyList<string> photoPaths,
        int photoIndex,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(photoPaths);
        ArgumentNullException.ThrowIfNull(fileExists);

        if (photoPaths.Count <= photoIndex)
            return Missing();

        var photoPath = photoPaths[photoIndex];
        if (string.IsNullOrEmpty(photoPath) || !fileExists(photoPath))
            return Missing();

        return new VsaCodeExplorerPhotoAssistantOpenDecision(
            CanOpen: true,
            PhotoPath: photoPath,
            Message: "",
            Title: "");
    }

    private static VsaCodeExplorerPhotoAssistantOpenDecision Missing()
        => new(
            CanOpen: false,
            PhotoPath: null,
            Message: "Kein Foto vorhanden. Bitte zuerst ein Foto aufnehmen.",
            Title: "PhotoAssistant");
}
