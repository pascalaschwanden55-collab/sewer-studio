using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaTypeStateControllerTests
{
    [Fact]
    public void Set_updates_active_schema_type()
    {
        var state = new CodingSchemaTypeStateController();

        state.Set(SchemaType.PipeBend);

        Assert.Equal(SchemaType.PipeBend, state.ActiveSchemaType);
    }

    [Fact]
    public void Clear_removes_active_schema_type()
    {
        var state = new CodingSchemaTypeStateController();
        state.Set(SchemaType.FillLevel);

        state.Clear();

        Assert.Null(state.ActiveSchemaType);
    }
}
