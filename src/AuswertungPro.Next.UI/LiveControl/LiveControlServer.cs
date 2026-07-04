using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.QgisBridge;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.LiveControl;

public sealed class LiveControlServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>Maximale Body-Groesse eines Live-Control-Requests (Schutz vor Speicher-Missbrauch).</summary>
    private const int MaxBodyBytes = 64 * 1024;

    private readonly System.Windows.Application _app;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly int _port;
    private readonly string? _token;
    private readonly QgisBridgeRequestProcessor? _qgisProcessor;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;
    private Task? _loopTask;

    private LiveControlServer(
        System.Windows.Application app,
        Dispatcher dispatcher,
        ILogger logger,
        int port,
        string? token,
        QgisBridgeRequestProcessor? qgisProcessor)
    {
        _app = app;
        _dispatcher = dispatcher;
        _logger = logger;
        _port = port;
        _token = string.IsNullOrWhiteSpace(token) ? null : token;
        _qgisProcessor = qgisProcessor;
    }

    // internal statt public: der optionale QGIS-Processor ist ein interner Typ,
    // und gestartet wird der Server ohnehin nur aus App.xaml.cs (gleiche Assembly).
    internal static LiveControlServer? TryStartFromEnvironment(
        System.Windows.Application app,
        ILogger logger,
        QgisBridgeRequestProcessor? qgisProcessor = null)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("SEWERSTUDIO_LIVE_CONTROL"),
                "1",
                StringComparison.Ordinal))
        {
            return null;
        }

        var portText = Environment.GetEnvironmentVariable("SEWERSTUDIO_LIVE_CONTROL_PORT");
        var port = int.TryParse(portText, out var parsed) && parsed is >= 1024 and <= 65535
            ? parsed
            : 8765;

        // Pflicht-Token: ist keiner per Env gesetzt, erzeugen wir einen sicheren Zufallstoken und
        // legen ihn in einer Datei ab, die der lokale Aufrufer (MCP-Server) liest. So laeuft
        // Live-Control nie ohne Auth - frueher war der Token null, womit die Pruefung uebersprungen wurde.
        var token = ResolveOrCreateToken(logger);
        var server = new LiveControlServer(app, app.Dispatcher, logger, port, token, qgisProcessor);
        server.Start();
        return server;
    }

    /// <summary>
    /// Pfad der Token-Datei im AppData-Verzeichnis (gleiche Ableitung wie AppSettings.AppDataDir),
    /// damit der MCP-Client exakt denselben Pfad berechnen kann.
    /// </summary>
    internal static string TokenFilePath
        => Path.Combine(AppDataPathResolver.Resolve(AppIdentity.ProductName), ".live_control_token");

    /// <summary>
    /// Ermittelt den Live-Control-Token: bevorzugt aus der Env-Var, sonst wird ein neuer erzeugt
    /// und in <see cref="TokenFilePath"/> abgelegt. Gibt nie null/leer zurueck -> Auth ist immer aktiv.
    /// </summary>
    private static string ResolveOrCreateToken(ILogger logger)
    {
        var envToken = Environment.GetEnvironmentVariable("SEWERSTUDIO_LIVE_CONTROL_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken;

        var generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        try
        {
            var path = TokenFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, generated);
            logger.LogInformation("Live-Control-Token automatisch erzeugt und abgelegt: {Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Live-Control-Token konnte nicht gespeichert werden - Auth bleibt aktiv, lokaler Client muss den Env-Token nutzen.");
        }

        return generated;
    }

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _loopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        _logger.LogWarning("SewerStudio Live-Control aktiv auf http://127.0.0.1:{Port}/", _port);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
            return;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live-Control Accept fehlgeschlagen.");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        try
        {
            using var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
            if (request is null)
                return;

            var response = await DispatchAsync(request.Value).ConfigureAwait(false);
            await WriteResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live-Control Request fehlgeschlagen.");
        }
    }

    private async Task<LiveHttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine))
            return null;

        var parts = requestLine.Split(' ', 3);
        if (parts.Length < 2)
            return null;

        var contentLength = 0;
        string? token = null;
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var name = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                _ = int.TryParse(value, out contentLength);
            else if (string.Equals(name, "X-Live-Control-Token", StringComparison.OrdinalIgnoreCase))
                token = value;
        }

        // Body-Limit: schuetzt vor Speicher-Missbrauch durch riesige Content-Length.
        if (contentLength > MaxBodyBytes)
        {
            _logger.LogWarning("Live-Control Request abgelehnt: Body zu gross ({Len} Bytes).", contentLength);
            return null;
        }

        var body = "";
        if (contentLength > 0)
        {
            var buffer = new char[contentLength];
            var read = 0;
            while (read < contentLength)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(read, contentLength - read), cancellationToken)
                    .ConfigureAwait(false);
                if (count == 0)
                    break;
                read += count;
            }

            body = new string(buffer, 0, read);
        }

        return new LiveHttpRequest(parts[0].ToUpperInvariant(), parts[1], body, token);
    }

    private async Task<LiveHttpResponse> DispatchAsync(LiveHttpRequest request)
    {
        // QGIS-Bridge: rein lesende GET-Endpunkte OHNE Token, damit das QGIS-Plugin
        // (kennt keinen Token) seine Layer auch dann bekommt, wenn Live-Control
        // denselben Port haelt. Die Steuer-Endpunkte darunter bleiben Token-geschuetzt.
        if (_qgisProcessor is not null
            && request.Method == "GET"
            && QgisBridgeRequestProcessor.IsBridgePath(request.Path))
        {
            var bridge = await _qgisProcessor.HandleAsync(request.Path).ConfigureAwait(false);
            return new LiveHttpResponse(bridge.StatusCode, Payload: null, RawBody: bridge.Body, ContentType: bridge.ContentType);
        }

        // Pflicht-Token mit zeitkonstantem Vergleich (gegen Timing-Angriffe). _token ist beim Start
        // immer gesetzt (Env oder automatisch erzeugt), daher gibt es keinen Auth-freien Pfad mehr.
        var expectedToken = Encoding.UTF8.GetBytes(_token ?? "");
        var providedToken = Encoding.UTF8.GetBytes(request.Token ?? "");
        if (!CryptographicOperations.FixedTimeEquals(expectedToken, providedToken))
            return new LiveHttpResponse(401, new { ok = false, error = "Live-Control-Token fehlt oder ist falsch." });

        if (request.Method == "GET" && request.Path == "/health")
        {
            return Ok(new
            {
                ok = true,
                app = "SewerStudio",
                live_control = true,
                port = _port
            });
        }

        if (request.Method == "POST" && request.Path == "/resource/brush")
        {
            var command = JsonSerializer.Deserialize<SetResourceBrushRequest>(request.Body, JsonOptions)
                          ?? throw new InvalidOperationException("Request-Body fehlt.");
            var result = await _dispatcher.InvokeAsync(() => ApplyResourceBrush(command)).Task.ConfigureAwait(false);
            return Ok(result);
        }

        if (request.Method == "POST" && request.Path == "/buttons/background")
        {
            var command = JsonSerializer.Deserialize<SetButtonBackgroundRequest>(request.Body, JsonOptions)
                          ?? throw new InvalidOperationException("Request-Body fehlt.");
            var result = await _dispatcher.InvokeAsync(() => ApplyButtonBackground(command)).Task.ConfigureAwait(false);
            return Ok(result);
        }

        if (request.Method == "POST" && request.Path == "/pipeline/retry")
        {
            var command = JsonSerializer.Deserialize<RetryHoldingRequest>(request.Body, JsonOptions)
                          ?? throw new InvalidOperationException("Request-Body fehlt.");
            var result = await _dispatcher
                .InvokeAsync(() => LiveControlRetryBridge.Invoke(command.Haltungsname ?? ""))
                .Task.ConfigureAwait(false);
            return Ok(new { ok = result.Ok, message = result.Message, haltung = command.Haltungsname });
        }

        return new LiveHttpResponse(404, new { ok = false, error = "Unbekannter Live-Control-Endpunkt." });
    }

    private object ApplyResourceBrush(SetResourceBrushRequest command)
    {
        if (!LiveControlRequestValidator.IsSafeResourceKey(command.Key))
            return new { ok = false, error = "Resource-Key ist ungueltig oder unsicher." };
        if (!LiveControlColorParser.TryParse(command.Color, out var color))
            return new { ok = false, error = "Farbe ist ungueltig. Nutze z.B. gelb, yellow, #F59E0B." };

        var dictionary = FindDictionaryWithKey(_app.Resources, command.Key!);
        if (dictionary is null)
            return new { ok = false, error = $"Resource-Key '{command.Key}' nicht gefunden." };

        if (dictionary[command.Key!] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
        }
        else
        {
            dictionary[command.Key!] = new SolidColorBrush(color);
        }

        return new { ok = true, key = command.Key, color = color.ToString() };
    }

    private object ApplyButtonBackground(SetButtonBackgroundRequest command)
    {
        if (!LiveControlColorParser.TryParse(command.Color, out var color))
            return new { ok = false, error = "Farbe ist ungueltig. Nutze z.B. gelb, yellow, #F59E0B." };

        var maxMatches = command.MaxMatches is > 0 and <= 500 ? command.MaxMatches.Value : 50;
        var target = command.Target?.Trim();
        var brush = new SolidColorBrush(color);
        var matches = new List<string>();

        foreach (Window window in _app.Windows)
        {
            foreach (var button in FindVisualChildren<Button>(window))
            {
                if (!MatchesButton(button, target))
                    continue;

                button.Background = brush;
                button.BorderBrush = brush;
                matches.Add(DescribeButton(window, button));
                if (matches.Count >= maxMatches)
                    break;
            }

            if (matches.Count >= maxMatches)
                break;
        }

        return new
        {
            ok = true,
            target = target ?? "",
            color = color.ToString(),
            count = matches.Count,
            matches
        };
    }

    private static bool MatchesButton(Button button, string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return true;

        return Contains(button.Name, target)
               || Contains(button.Content?.ToString(), target)
               || Contains(AutomationProperties.GetName(button), target)
               || Contains(button.ToolTip?.ToString(), target);
    }

    private static bool Contains(string? value, string target)
        => !string.IsNullOrWhiteSpace(value)
           && value.Contains(target, StringComparison.OrdinalIgnoreCase);

    private static string DescribeButton(Window window, Button button)
    {
        var label = button.Name;
        if (string.IsNullOrWhiteSpace(label))
            label = button.Content?.ToString();
        if (string.IsNullOrWhiteSpace(label))
            label = AutomationProperties.GetName(button);
        if (string.IsNullOrWhiteSpace(label))
            label = "(button)";

        return $"{window.GetType().Name}:{label}";
    }

    private static ResourceDictionary? FindDictionaryWithKey(ResourceDictionary dictionary, string key)
    {
        if (dictionary.Keys.Cast<object>().Any(k => string.Equals(k?.ToString(), key, StringComparison.Ordinal)))
            return dictionary;

        foreach (var merged in dictionary.MergedDictionaries)
        {
            var found = FindDictionaryWithKey(merged, key);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T item)
                yield return item;

            foreach (var nested in FindVisualChildren<T>(child))
                yield return nested;
        }
    }

    private static LiveHttpResponse Ok(object payload) => new(200, payload);

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        LiveHttpResponse response,
        CancellationToken cancellationToken)
    {
        // Entweder vorgefertigter Body (QGIS-Bridge) oder JSON-Serialisierung des Payloads.
        var body = response.RawBody
                   ?? Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response.Payload, JsonOptions));
        var reason = response.StatusCode == 200 ? "OK" : "Error";
        var header = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {response.StatusCode} {reason}\r\nContent-Type: {response.ContentType}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
        _cts.Dispose();
    }

    private readonly record struct LiveHttpRequest(string Method, string Path, string Body, string? Token);
    private readonly record struct LiveHttpResponse(
        int StatusCode,
        object? Payload,
        byte[]? RawBody = null,
        string ContentType = "application/json; charset=utf-8");
    private sealed record SetResourceBrushRequest(string? Key, string? Color);
    private sealed record SetButtonBackgroundRequest(string? Target, string? Color, int? MaxMatches);
    private sealed record RetryHoldingRequest(string? Haltungsname);
}
