namespace AuswertungPro.Tools.SewerStudioMcpServer;

public sealed record SewerStudioMcpOptions(
    string HaltungenRoot,
    string DiagnosticsOutputDir,
    string KnowledgeRoot,
    string LiveControlUrl)
{
    public static SewerStudioMcpOptions FromArgs(IReadOnlyList<string> args)
    {
        var haltungenRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_HALTUNGEN_ROOT")
                            ?? @"D:\Haltungen";
        var diagnosticsOutput = Environment.GetEnvironmentVariable("SEWERSTUDIO_DIAGNOSTICS_OUTPUT")
                                ?? Path.Combine(FindRepoRoot(), "tools", "ProtocolPipelineDiagnostics", "output");
        var knowledgeRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT")
                            ?? @"C:\KI_BRAIN";
        var liveControlUrl = Environment.GetEnvironmentVariable("SEWERSTUDIO_LIVE_CONTROL_URL")
                             ?? "http://127.0.0.1:8765/";

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--haltungen-root" when i + 1 < args.Count:
                    haltungenRoot = args[++i];
                    break;
                case "--diagnostics-output" when i + 1 < args.Count:
                    diagnosticsOutput = args[++i];
                    break;
                case "--knowledge-root" when i + 1 < args.Count:
                    knowledgeRoot = args[++i];
                    break;
                case "--live-control-url" when i + 1 < args.Count:
                    liveControlUrl = args[++i];
                    break;
            }
        }

        return new SewerStudioMcpOptions(haltungenRoot, diagnosticsOutput, knowledgeRoot, liveControlUrl);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
