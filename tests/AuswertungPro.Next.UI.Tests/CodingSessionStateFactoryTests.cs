using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionStateFactoryTests
{
    [Fact]
    public void Create_builds_session_overlay_and_view_model_with_video_path()
    {
        var state = CodingSessionStateFactory.Create(@"C:\videos\haltung.mp4");

        Assert.NotNull(state.SessionService);
        Assert.NotNull(state.OverlayService);
        Assert.NotNull(state.ViewModel);
        Assert.Equal(@"C:\videos\haltung.mp4", state.ViewModel.VideoPath);
    }
}
