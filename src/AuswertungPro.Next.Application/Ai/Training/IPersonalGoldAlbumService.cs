namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>Liest persoenlich bestaetigte Handlabels fuer das Goldstandard-Fotoalbum.</summary>
public interface IPersonalGoldAlbumService
{
    Task<PersonalGoldAlbumSnapshot> LoadAsync(
        string confirmedByUser,
        CancellationToken cancellationToken = default);
}

public sealed record PersonalGoldAlbumItem(
    string SampleId,
    string MainCode,
    string Code,
    string Beschreibung,
    string FramePath,
    DateTime? ConfirmedAtUtc,
    bool HasBbox,
    bool HasSegmentation,
    bool FileExists)
{
    public bool IsFullGold => HasBbox && HasSegmentation;

    public string GeometryStatus => (HasBbox, HasSegmentation) switch
    {
        (true, true) => "Box und Segmentierung vorhanden",
        (true, false) => "Segmentierung fehlt",
        (false, true) => "Box fehlt",
        _ => "Box und Segmentierung fehlen"
    };

    public string FileStatus => FileExists ? "Bilddatei vorhanden" : "Bilddatei fehlt";
}

public sealed record PersonalGoldAlbumGroup(
    string MainCode,
    int FullGoldCount,
    IReadOnlyList<PersonalGoldAlbumItem> Items);

public sealed record PersonalGoldAlbumSnapshot(
    IReadOnlyList<PersonalGoldAlbumGroup> Groups,
    int TotalSamples,
    int FullGoldSamples,
    int IncompleteSamples,
    int MissingFiles);
