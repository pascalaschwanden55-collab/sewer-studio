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
- `live_control_health`
- `live_set_resource_brush`
- `live_set_button_background`

Write operations such as retrying failed holdings or marking samples reviewed are
out of scope until the diagnostic run output is available.

Live-control tools are a narrow exception: they only talk to the running WPF app
over `127.0.0.1` and only work when the app was started with
`SEWERSTUDIO_LIVE_CONTROL=1`. They are intended for temporary UI tuning, not
persistent project data changes.

## Run

```powershell
dotnet run --project tools/SewerStudioMcpServer/SewerStudioMcpServer.csproj -- `
  --haltungen-root "D:\Haltungen" `
  --diagnostics-output "tools\ProtocolPipelineDiagnostics\output" `
  --knowledge-root "C:\KI_BRAIN" `
  --live-control-url "http://127.0.0.1:8765/"
```

The process speaks MCP over stdin/stdout. Do not write logs to stdout; stderr is
safe for diagnostics.

## Defaults

- `SEWERSTUDIO_HALTUNGEN_ROOT` or `D:\Haltungen`
- `SEWERSTUDIO_DIAGNOSTICS_OUTPUT` or `tools/ProtocolPipelineDiagnostics/output`
- `SEWERSTUDIO_KNOWLEDGE_ROOT` or `C:\KI_BRAIN`
- `SEWERSTUDIO_LIVE_CONTROL_URL` or `http://127.0.0.1:8765/`

Each tool also accepts an optional per-call path override for tests and ad-hoc
diagnostics.

## Live-Control

Start SewerStudio with:

```powershell
$env:SEWERSTUDIO_LIVE_CONTROL='1'
dotnet run --project src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj
```

Examples:

- `live_control_health`
- `live_set_resource_brush` with `{ "key": "AccentBrush", "color": "gelb" }`
- `live_set_button_background` with `{ "target": "Speichern", "color": "gelb" }`
