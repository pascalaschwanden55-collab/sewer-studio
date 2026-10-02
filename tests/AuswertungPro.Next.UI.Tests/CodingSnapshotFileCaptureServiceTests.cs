using System;
using System.Collections.Generic;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSnapshotFileCaptureServiceTests
{
    [Fact]
    public void CaptureSnapshot_creates_directory_invokes_snapshot_and_returns_path_when_file_is_ready()
    {
        var createdDirectories = new List<string>();
        var slept = 0;
        var snapshotPath = "";
        var target = new CodingSnapshotTarget("photos", "photos/frame.png");
        var service = new CodingSnapshotFileCaptureService(
            createdDirectories.Add,
            path => path == target.FilePath,
            path => path == target.FilePath ? 128 : 0,
            _ => slept++);

        var result = service.CaptureSnapshot(target, path => snapshotPath = path);

        Assert.Equal(target.FilePath, result);
        Assert.Equal(new[] { target.PhotoDirectory }, createdDirectories);
        Assert.Equal(target.FilePath, snapshotPath);
        Assert.Equal(1, slept);
    }

    [Fact]
    public void CaptureSnapshot_waits_until_snapshot_file_is_large_enough()
    {
        var attempts = 0;
        var target = new CodingSnapshotTarget("photos", "photos/frame.png");
        var service = new CodingSnapshotFileCaptureService(
            _ => { },
            _ => true,
            _ => ++attempts < 3 ? 10 : 128,
            _ => { });

        var result = service.CaptureSnapshot(target, _ => { });

        Assert.Equal(target.FilePath, result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public void CaptureSnapshot_returns_existing_small_file_after_wait_limit()
    {
        var target = new CodingSnapshotTarget("photos", "photos/frame.png");
        var service = new CodingSnapshotFileCaptureService(
            _ => { },
            _ => true,
            _ => 10,
            _ => { });

        var result = service.CaptureSnapshot(target, _ => { });

        Assert.Equal(target.FilePath, result);
    }

    [Fact]
    public void CaptureSnapshot_logs_and_returns_null_when_snapshot_fails()
    {
        var logs = new List<string>();
        var target = new CodingSnapshotTarget("photos", "photos/frame.png");
        var service = new CodingSnapshotFileCaptureService(
            _ => { },
            _ => false,
            _ => 0,
            _ => { },
            logs.Add);

        var result = service.CaptureSnapshot(
            target,
            _ => throw new InvalidOperationException("kaputt"));

        Assert.Null(result);
        var log = Assert.Single(logs);
        Assert.Contains("kaputt", log);
    }

    /// <summary>
    /// Deepscan 02.10.2026, R3: Ohne Video zeigt das Ziel in den Temp-Ordner. Befundfotos
    /// gehoeren nie dorthin; der Aufnahmeweg meldet dann «Foto konnte nicht aufgenommen werden».
    /// </summary>
    [Fact]
    public void CaptureSnapshot_nimmt_kein_foto_in_den_temp_ordner_auf()
    {
        var logs = new List<string>();
        var aufgenommen = 0;
        var ordnerAngelegt = 0;
        var target = new CodingSnapshotTarget("temp/Fotos", "temp/Fotos/frame.png", LiegtImTemp: true);
        var service = new CodingSnapshotFileCaptureService(
            _ => ordnerAngelegt++,
            _ => true,
            _ => 128,
            _ => { },
            logs.Add);

        var result = service.CaptureSnapshot(target, _ => aufgenommen++);

        Assert.Null(result);
        Assert.Equal(0, aufgenommen);
        Assert.Equal(0, ordnerAngelegt);
        Assert.Contains("Temp-Ordner", Assert.Single(logs));
    }
}
