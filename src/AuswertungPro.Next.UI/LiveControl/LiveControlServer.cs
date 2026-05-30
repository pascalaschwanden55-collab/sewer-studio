using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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

    private readonly System.Windows.Application _app;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly int _port;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;
    private Task? _loopTask;

    private LiveControlServer(System.Windows.Application app, Dispatcher dispatcher, ILogger logger, int port)
    {
        _app = app;
        _dispatcher = dispatcher;
        _logger = logger;
        _port = port;
    }

    public static LiveControlServer? TryStartFromEnvironment(System.Windows.Application app, ILogger logger)
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

        var server = new LiveControlServer(app, app.Dispatcher, logger, port);
        server.Start();
        return server;
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
            await WriteJsonResponseAsync(stream, response.StatusCode, response.Payload, cancellationToken)
                .ConfigureAwait(false);
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

        return new LiveHttpRequest(parts[0].ToUpperInvariant(), parts[1], body);
    }

    private async Task<LiveHttpResponse> DispatchAsync(LiveHttpRequest request)
    {
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

    private static async Task WriteJsonResponseAsync(
        NetworkStream stream,
        int statusCode,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var body = Encoding.UTF8.GetBytes(json);
        var reason = statusCode == 200 ? "OK" : "Error";
        var header = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
        _cts.Dispose();
    }

    private readonly record struct LiveHttpRequest(string Method, string Path, string Body);
    private readonly record struct LiveHttpResponse(int StatusCode, object Payload);
    private sealed record SetResourceBrushRequest(string? Key, string? Color);
    private sealed record SetButtonBackgroundRequest(string? Target, string? Color, int? MaxMatches);
    private sealed record RetryHoldingRequest(string? Haltungsname);
}
