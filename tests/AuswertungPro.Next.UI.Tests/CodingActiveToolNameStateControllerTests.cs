using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingActiveToolNameStateControllerTests
{
    [Fact]
    public void Set_updates_active_tool_name()
    {
        var state = new CodingActiveToolNameStateController();

        state.Set("BtnPipeBend");

        Assert.Equal("BtnPipeBend", state.ActiveToolName);
    }

    [Fact]
    public void Clear_removes_active_tool_name()
    {
        var state = new CodingActiveToolNameStateController();
        state.Set("BtnCodingCalibrate");

        state.Clear();

        Assert.Null(state.ActiveToolName);
    }
}
