using System.IO;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingPhotoSlotUpdate(int SlotNumber, bool Replaced, string OverlayText);

public static class CodingPhotoSlotPolicy
{
    public static CodingPhotoSlotUpdate Apply(IList<string> photoPaths, string photoPath)
    {
        if (photoPaths.Count >= 2)
        {
            photoPaths[1] = photoPath;
            return new CodingPhotoSlotUpdate(
                SlotNumber: 2,
                Replaced: true,
                OverlayText: $"Foto 2 ersetzt: {Path.GetFileName(photoPath)}");
        }

        photoPaths.Add(photoPath);
        return new CodingPhotoSlotUpdate(
            SlotNumber: photoPaths.Count,
            Replaced: false,
            OverlayText: $"Foto {photoPaths.Count}: {Path.GetFileName(photoPath)}");
    }
}
