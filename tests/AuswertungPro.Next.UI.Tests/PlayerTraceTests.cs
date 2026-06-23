using System.Collections.Generic;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerTraceTests
{
    [Fact]
    public void WriteLine_uses_supplied_sink()
    {
        var messages = new List<string>();

        PlayerTrace.WriteLine("hello", messages.Add);

        Assert.Equal(new[] { "hello" }, messages);
    }
}
