# SewerStudioMcpServer

Read-only MCP stdio server for SewerStudio diagnostics and protocol data.

## Scope

This server is intentionally out-of-process. It does not modify
`src/AuswertungPro.Next.UI/App.xaml.cs`, does not register anything in the WPF
DI container, and does not write to the KnowledgeBase or UI state.

Implemented tools:

- `list_haltungen`
- `get_protocol_entries`
- `get_diagnostic_report`
- `list_training_samples`

Write operations such as retrying failed holdings or marking samples reviewed are
out of scope until the diagnostic run output is available.

## Run

```powershell
dotnet run --project tools/SewerStudioMcpServer/SewerStudioMcpServer.csproj -- `
  --haltungen-root "D:\Haltungen" `
  --diagnostics-output "tools\ProtocolPipelineDiagnostics\output" `
  --knowledge-root "C:\KI_BRAIN"
```

The process speaks MCP over stdin/stdout. Do not write logs to stdout; stderr is
safe for diagnostics.

## Defaults

- `SEWERSTUDIO_HALTUNGEN_ROOT` or `D:\Haltungen`
- `SEWERSTUDIO_DIAGNOSTICS_OUTPUT` or `tools/ProtocolPipelineDiagnostics/output`
- `SEWERSTUDIO_KNOWLEDGE_ROOT` or `C:\KI_BRAIN`

Each tool also accepts an optional per-call path override for tests and ad-hoc
diagnostics.
