using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoDisplayPathPolicyTests
{
    [Fact]
    public void BuildDisplayPhotoPaths_puts_existing_evidence_preview_first_and_deduplicates()
    {
        var paths = CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths(
            evidencePreviewPath: @"C:\work\evidence.png",
            photoPaths: [@"C:\work\PHOTO1.png", @"C:\work\evidence.png", @"C:\work\photo1.png", "relative.png"],
            fileExists: path => path.EndsWith("evidence.png", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            [@"C:\work\evidence.png", @"C:\work\PHOTO1.png", "relative.png"],
            paths);
    }

    [Fact]
    public void BuildDisplayPhotoPaths_ignores_missing_evidence_preview()
    {
        var paths = CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths(
            evidencePreviewPath: @"C:\work\missing.png",
            photoPaths: ["photo.png"],
            fileExists: _ => false);

        Assert.Equal(["photo.png"], paths);
    }

    [Fact]
    public void ResolveExistingPath_prefers_existing_rooted_path()
    {
        var path = CodingPhotoDisplayPathPolicy.ResolveExistingPath(
            @"C:\photos\a.png",
            projectFolder: @"C:\project",
            fileExists: existing => existing == @"C:\photos\a.png");

        Assert.Equal(@"C:\photos\a.png", path);
    }

    [Fact]
    public void ResolveExistingPath_resolves_relative_path_under_project_folder()
    {
        var path = CodingPhotoDisplayPathPolicy.ResolveExistingPath(
            @"Fotos\a.png",
            projectFolder: @"C:\project",
            fileExists: existing => existing == @"C:\project\Fotos\a.png");

        Assert.Equal(@"C:\project\Fotos\a.png", path);
    }
}
