using AuswertungPro.Tools.SewerStudioMcpServer;

if (args.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine("""
    SewerStudio MCP Server (read-only)

    Runs an MCP stdio server exposing:
      - list_haltungen
      - get_protocol_entries
      - get_diagnostic_report
      - list_training_samples

    Options:
      --haltungen-root <path>       Default: SEWERSTUDIO_HALTUNGEN_ROOT or D:\Haltungen
      --diagnostics-output <path>   Default: SEWERSTUDIO_DIAGNOSTICS_OUTPUT or tools/ProtocolPipelineDiagnostics/output
      --knowledge-root <path>       Default: SEWERSTUDIO_KNOWLEDGE_ROOT or C:\KI_BRAIN

    Important: stdout is reserved for MCP JSON-RPC messages. Diagnostics go to stderr.
    """);
    return 0;
}

var options = SewerStudioMcpOptions.FromArgs(args);
var registry = new SewerStudioToolRegistry(options);
var handler = new McpRequestHandler(registry);
var server = new StdioMcpServer(handler, Console.In, Console.Out, Console.Error);
await server.RunAsync().ConfigureAwait(false);
return 0;
