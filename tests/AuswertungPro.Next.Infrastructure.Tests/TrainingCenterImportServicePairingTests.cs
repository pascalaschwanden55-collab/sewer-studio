using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TrainingCenterImportServicePairingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ap-pairing-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ResolvePair_keeps_unambiguous_one_to_one_case()
    {
        var video = WriteFile("video.mp4", 10);
        var proto = WriteFile("bericht.pdf", 10);

        var pair = TrainingCenterImportService.ResolvePair([video], [proto], "24379-41412");

        Assert.Equal(video, pair.VideoPath);
        Assert.Equal(proto, pair.ProtocolPath);
    }

    [Fact]
    public void ResolvePair_prefers_id_matching_video_over_largest_video()
    {
        var matchingVideo = WriteFile("H_06.24379-41412.mp4", 10);
        var largeWrongVideo = WriteFile("H_99999-88888.mp4", 200);
        var proto = WriteFile("protokoll_24379-41412.pdf", 10);

        var pair = TrainingCenterImportService.ResolvePair(
            [matchingVideo, largeWrongVideo], [proto], "24379-41412");

        Assert.Equal(matchingVideo, pair.VideoPath);
        Assert.Equal(proto, pair.ProtocolPath);
    }

    [Fact]
    public void ResolvePair_prefers_id_matching_protocol_over_best_protocol_keyword()
    {
        var video = WriteFile("H_24379-41412.mp4", 10);
        var wrongProtocol = WriteFile("bericht_99999-88888.pdf", 200);
        var matchingProtocol = WriteFile("bericht_24379-41412.pdf", 10);

        var pair = TrainingCenterImportService.ResolvePair(
            [video], [wrongProtocol, matchingProtocol], "24379-41412");

        Assert.Equal(video, pair.VideoPath);
        Assert.Equal(matchingProtocol, pair.ProtocolPath);
    }

    [Fact]
    public void ResolvePair_clears_wrong_protocol_when_video_matches_case()
    {
        var video = WriteFile("H_24379-41412.mp4", 10);
        var wrongProtocol = WriteFile("bericht_99999-88888.pdf", 10);
        var otherWrongProtocol = WriteFile("bericht_88888-77777.pdf", 20);

        var pair = TrainingCenterImportService.ResolvePair(
            [video], [wrongProtocol, otherWrongProtocol], "24379-41412");

        Assert.Equal(video, pair.VideoPath);
        Assert.Equal("", pair.ProtocolPath);
    }

    [Fact]
    public void ResolvePair_uses_normalized_haltung_key_for_area_prefixes()
    {
        var video = WriteFile("H_06.24379-41412.mp4", 10);
        var wrongLargeVideo = WriteFile("H_99999-88888.mp4", 200);
        var proto = WriteFile("protokoll_24379-41412.pdf", 10);

        var pair = TrainingCenterImportService.ResolvePair(
            [video, wrongLargeVideo], [proto], "06.24379-41412");

        Assert.Equal(video, pair.VideoPath);
        Assert.Equal(proto, pair.ProtocolPath);
    }

    [Fact]
    public void ResolveProtocolOnlyPair_keeps_protocol_and_clears_conflicting_video()
    {
        var wrongVideo = WriteFile("H_99999-88888.mp4", 10);
        var proto = WriteFile("protokoll_24379-41412.pdf", 10);

        var pair = TrainingCenterImportService.ResolveProtocolOnlyPair(
            [wrongVideo], [proto], "24379-41412");

        Assert.Equal("", pair.VideoPath);
        Assert.Equal(proto, pair.ProtocolPath);
    }

    [Fact]
    public async Task ScanAsync_uses_id_matching_pair_when_folder_is_ambiguous()
    {
        var caseDir = Path.Combine(_root, "24379-41412");
        Directory.CreateDirectory(caseDir);
        var matchingVideo = WriteFile(Path.Combine("24379-41412", "H_24379-41412.mp4"), 10);
        WriteFile(Path.Combine("24379-41412", "H_99999-88888.mp4"), 200);
        var proto = WriteFile(Path.Combine("24379-41412", "bericht_24379-41412.pdf"), 10);

        var cases = await new TrainingCenterImportService().ScanAsync(caseDir);

        var result = Assert.Single(cases);
        Assert.Equal(matchingVideo, result.VideoPath);
        Assert.Equal(proto, result.ProtocolPath);
    }

    [Fact]
    public async Task ScanProtocolOnlyAsync_keeps_protocol_and_drops_conflicting_video()
    {
        var caseDir = Path.Combine(_root, "24379-41412");
        Directory.CreateDirectory(caseDir);
        WriteFile(Path.Combine("24379-41412", "H_99999-88888.mp4"), 10);
        var proto = WriteFile(Path.Combine("24379-41412", "bericht_24379-41412.pdf"), 10);

        var cases = await new TrainingCenterImportService().ScanProtocolOnlyAsync(caseDir);

        var result = Assert.Single(cases);
        Assert.Equal("", result.VideoPath);
        Assert.Equal(proto, result.ProtocolPath);
    }

    private string WriteFile(string name, int bytes)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
