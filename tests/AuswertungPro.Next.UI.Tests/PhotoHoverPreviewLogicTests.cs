using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PhotoHoverPreviewLogicTests
{
    // ── ResolveExistingPhotos ──

    [Fact]
    public void ResolveExistingPhotos_keeps_existing_absolute_paths()
    {
        var photos = PhotoHoverPreviewLogic.ResolveExistingPhotos(
            [@"C:\photos\a.jpg"],
            projectRoot: @"C:\project",
            fileExists: p => p == @"C:\photos\a.jpg");

        Assert.Equal([@"C:\photos\a.jpg"], photos);
    }

    [Fact]
    public void ResolveExistingPhotos_resolves_relative_against_root()
    {
        var photos = PhotoHoverPreviewLogic.ResolveExistingPhotos(
            [@"Fotos\a.jpg"],
            projectRoot: @"C:\project",
            fileExists: p => p == @"C:\project\Fotos\a.jpg");

        Assert.Equal([@"C:\project\Fotos\a.jpg"], photos);
    }

    [Fact]
    public void ResolveExistingPhotos_drops_missing_and_whitespace()
    {
        var photos = PhotoHoverPreviewLogic.ResolveExistingPhotos(
            [@"C:\photos\a.jpg", "   ", "", @"C:\photos\missing.jpg"],
            projectRoot: @"C:\project",
            fileExists: p => p == @"C:\photos\a.jpg");

        Assert.Equal([@"C:\photos\a.jpg"], photos);
    }

    [Fact]
    public void ResolveExistingPhotos_without_root_keeps_only_absolute_existing()
    {
        var photos = PhotoHoverPreviewLogic.ResolveExistingPhotos(
            [@"Fotos\a.jpg", @"C:\photos\a.jpg"],
            projectRoot: null,
            fileExists: p => p == @"C:\photos\a.jpg" || p == @"C:\project\Fotos\a.jpg");

        Assert.Equal([@"C:\photos\a.jpg"], photos);
    }

    [Fact]
    public void ResolveExistingPhotos_deduplicates_case_insensitive()
    {
        var photos = PhotoHoverPreviewLogic.ResolveExistingPhotos(
            [@"C:\photos\a.jpg", @"C:\photos\A.JPG"],
            projectRoot: @"C:\project",
            fileExists: _ => true);

        Assert.Single(photos);
    }

    // ── NextIndex ──

    [Theory]
    [InlineData(2, 3, +1, 0)]   // vorwaerts mit Umlauf
    [InlineData(0, 3, -1, 2)]   // rueckwaerts mit Umlauf
    [InlineData(0, 1, +1, 0)]   // count 1 bleibt 0
    [InlineData(0, 0, +1, 0)]   // count 0 -> 0
    public void NextIndex_wraps_in_both_directions(int current, int count, int delta, int expected)
    {
        Assert.Equal(expected, PhotoHoverPreviewLogic.NextIndex(current, count, delta));
    }

    // ── CounterText ──

    [Fact]
    public void CounterText_is_one_based()
    {
        Assert.Equal("1/3", PhotoHoverPreviewLogic.CounterText(0, 3));
    }

    // ── MaxBoxFromScreen ──

    [Fact]
    public void MaxBoxFromScreen_takes_quarter_of_screen()
    {
        var (maxWidth, maxHeight) = PhotoHoverPreviewLogic.MaxBoxFromScreen(1920, 1080);
        Assert.Equal(480d, maxWidth);
        Assert.Equal(270d, maxHeight);
    }

    // ── FitPreserveAspect ──

    [Fact]
    public void FitPreserveAspect_landscape_fits_by_height()
    {
        var (width, height) = PhotoHoverPreviewLogic.FitPreserveAspect(4000, 3000, 480, 270);
        Assert.Equal(360d, width, 3);
        Assert.Equal(270d, height, 3);
    }

    [Fact]
    public void FitPreserveAspect_portrait_fits_by_height()
    {
        var (width, height) = PhotoHoverPreviewLogic.FitPreserveAspect(1080, 1920, 480, 270);
        Assert.Equal(151.875d, width, 3);
        Assert.Equal(270d, height, 3);
    }

    [Fact]
    public void FitPreserveAspect_small_image_is_not_upscaled()
    {
        var (width, height) = PhotoHoverPreviewLogic.FitPreserveAspect(200, 100, 480, 270);
        Assert.Equal(200d, width);
        Assert.Equal(100d, height);
    }

    [Fact]
    public void FitPreserveAspect_invalid_dimensions_return_zero()
    {
        var (width, height) = PhotoHoverPreviewLogic.FitPreserveAspect(0, 0, 480, 270);
        Assert.Equal(0d, width);
        Assert.Equal(0d, height);
    }
}
