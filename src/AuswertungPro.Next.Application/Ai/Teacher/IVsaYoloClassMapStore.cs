namespace AuswertungPro.Next.Application.Ai.Teacher;

/// <summary>Persistierte Zuordnung von VSA-Codes zu stabilen YOLO-Klassen-IDs.</summary>
public interface IVsaYoloClassMapStore
{
    int GetClassId(string vsaCode);

    Dictionary<string, int> GetFullMap();

    Task ExportClassesTxtAsync(string outputPath);
}
