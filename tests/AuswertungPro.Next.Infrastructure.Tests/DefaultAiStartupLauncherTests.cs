using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.Infrastructure.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class DefaultAiStartupLauncherTests
{
    [Fact]
    public void CreateStartInfo_applies_process_specific_environment()
    {
        var request = new AiStartupProcessRequest("ollama", "serve", null, Hidden: true)
        {
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["OLLAMA_HOST"] = "127.0.0.1:11434"
            }
        };

        var startInfo = DefaultAiStartupLauncher.CreateStartInfo(request);

        Assert.False(startInfo.UseShellExecute);
        Assert.Equal("127.0.0.1:11434", startInfo.Environment["OLLAMA_HOST"]);
    }
}
