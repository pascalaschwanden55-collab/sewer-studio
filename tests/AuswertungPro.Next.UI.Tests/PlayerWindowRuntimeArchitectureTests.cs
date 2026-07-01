using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowRuntimeArchitectureTests
{
    [Fact]
    public void PlayerWindow_detection_confirmation_buffer_owns_pending_detection_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var bufferPath = Path.Combine(uiRoot, "Ai", "DetectionConfirmationBuffer.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");

        Assert.True(File.Exists(bufferPath), "Geteilter Detection-Pending-Zustand soll in einem eigenen Buffer liegen.");
        Assert.True(File.Exists(controllerPath), "LiveDetectionController soll den Detection-Pending-Zustand fuer PlayerWindow besitzen.");

        var playerWindowText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var buffer = File.ReadAllText(bufferPath);
        var controller = File.ReadAllText(controllerPath);

        Assert.DoesNotContain("private readonly DetectionConfirmationBuffer _detectionConfirmationBuffer", playerWindowText);
        Assert.Contains("private readonly DetectionConfirmationBuffer _confirmationBuffer = new();", controller);
        Assert.Contains("public void StoreConfirmationFindings", controller);
        Assert.Contains("public void StoreAnalyzedFrame", controller);
        Assert.Contains("public void ClearConfirmationBuffer", controller);
        Assert.DoesNotContain("_detectionPendingFindings", playerWindowText);
        Assert.DoesNotContain("_detectionPendingFrameBytes", playerWindowText);
        Assert.DoesNotContain("_detectionPendingTimestampSec", playerWindowText);
        Assert.Contains("public void StoreFindings", buffer);
        Assert.Contains("public void StoreAnalyzedFrame", buffer);
        Assert.Contains("public void Clear", buffer);
    }

    [Fact]
    public void PlayerWindow_service_provider_access_lives_behind_dependencies()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var dependenciesPath = Path.Combine(uiRoot, "Player", "PlayerWindowDependencies.cs");
        var protocolContextPath = Path.Combine(uiRoot, "Player", "PlayerWindowProtocolContext.cs");

        Assert.True(File.Exists(dependenciesPath), "PlayerWindow-Partials sollen nicht direkt am konkreten ServiceProvider haengen.");
        Assert.True(File.Exists(protocolContextPath), "PlayerWindow-Protokolldaten sollen in einem Kontext gebuendelt sein.");

        var offenders = Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs")
            .Where(path => !path.EndsWith("PlayerWindow.xaml.cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith("PlayerWindow.State.cs", StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = path,
                Lines = File.ReadLines(path)
                    .Select((line, index) => new { Line = line, Number = index + 1 })
                    .Where(item => item.Line.Contains("_serviceProvider", StringComparison.Ordinal))
                    .Select(item => item.Number)
                    .ToArray()
            })
            .Where(item => item.Lines.Length > 0)
            .Select(item => $"{Path.GetFileName(item.Path)}:{string.Join(",", item.Lines)}")
            .ToArray();

        var state = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.State.cs"));
        var windowRoot = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.xaml.cs"));
        var dependencies = File.ReadAllText(dependenciesPath);
        var protocolContext = File.ReadAllText(protocolContextPath);
        var playerWindowPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));

        Assert.True(
            offenders.Length == 0,
            "_serviceProvider darf nur im Konstruktor/State als Legacy-Bruecke stehen. Partials nutzen PlayerWindowDependencies:\n"
            + string.Join("\n", offenders));
        Assert.Contains("private readonly PlayerWindowProtocolContext _protocolContext", state);
        Assert.DoesNotContain("private readonly ServiceProvider? _serviceProvider", state);
        Assert.DoesNotContain("_serviceProvider = serviceProvider", windowRoot);
        Assert.DoesNotContain("_protocolContext.Dependencies.", playerWindowPartials);
        Assert.DoesNotContain("public PlayerWindowDependencies Dependencies", protocolContext);
        Assert.Contains("_protocolContext = PlayerWindowProtocolContext.From(", windowRoot);
        Assert.Contains("PlayerWindowDependencies.From(serviceProvider)", protocolContext);
        Assert.Contains("public AppSettings? Settings", protocolContext);
        Assert.Contains("public string? LastProjectPath", protocolContext);
        Assert.Contains("public bool HasCodeCatalog", protocolContext);
        Assert.Contains("public ServiceProvider? LegacyServiceProvider", dependencies);
        Assert.Contains("public string? LastProjectPath", dependencies);
    }
}
