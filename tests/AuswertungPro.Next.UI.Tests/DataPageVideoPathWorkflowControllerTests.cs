using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoPathWorkflowControllerTests
{
    [Fact]
    public void Resolve_returns_existing_resolved_link_and_saves_only_when_path_changed()
    {
        var record = new HaltungRecord();
        var saved = new List<(string Path, bool UserEdited)>();

        var result = DataPageVideoPathWorkflowController.Resolve(
            record,
            " C:\\Videos\\raw.mp4 ",
            "C:\\Initial",
            raw => raw == " C:\\Videos\\raw.mp4 " ? "C:\\Videos\\resolved.mp4" : null,
            _ => throw new InvalidOperationException("directory should not be checked"),
            (_, _) => throw new InvalidOperationException("video search should not run"),
            (_, _) => throw new InvalidOperationException("folder dialog should not open"),
            _ => throw new InvalidOperationException("folder should not be persisted"),
            (_, _) => throw new InvalidOperationException("info dialog should not be shown"),
            (_, _, _) => throw new InvalidOperationException("file dialog should not open"),
            (path, userEdited) =>
            {
                saved.Add((path, userEdited));
                return "saved-value";
            });

        Assert.Equal("C:\\Videos\\resolved.mp4", result);
        Assert.Equal(new[] { ("C:\\Videos\\resolved.mp4", false) }, saved);
    }

    [Fact]
    public void Resolve_returns_existing_link_without_saving_when_resolved_path_matches_trimmed_raw_link()
    {
        var record = new HaltungRecord();

        var result = DataPageVideoPathWorkflowController.Resolve(
            record,
            " C:\\Videos\\raw.mp4 ",
            "C:\\Initial",
            _ => "C:\\Videos\\raw.mp4",
            _ => throw new InvalidOperationException("directory should not be checked"),
            (_, _) => throw new InvalidOperationException("video search should not run"),
            (_, _) => throw new InvalidOperationException("folder dialog should not open"),
            _ => throw new InvalidOperationException("folder should not be persisted"),
            (_, _) => throw new InvalidOperationException("info dialog should not be shown"),
            (_, _, _) => throw new InvalidOperationException("file dialog should not open"),
            (_, _) => throw new InvalidOperationException("video link should not be saved"));

        Assert.Equal("C:\\Videos\\raw.mp4", result);
    }

    [Fact]
    public void Resolve_uses_initial_folder_search_before_opening_dialogs()
    {
        var record = new HaltungRecord();
        var events = new List<string>();

        var result = DataPageVideoPathWorkflowController.Resolve(
            record,
            "",
            "C:\\Initial",
            _ => null,
            folder =>
            {
                events.Add("exists:" + folder);
                return true;
            },
            (folder, rec) =>
            {
                Assert.Same(record, rec);
                events.Add("resolve:" + folder);
                return Success("C:\\Initial\\video.mp4");
            },
            (_, _) => throw new InvalidOperationException("folder dialog should not open"),
            _ => throw new InvalidOperationException("folder should not be persisted"),
            (_, _) => throw new InvalidOperationException("info dialog should not be shown"),
            (_, _, _) => throw new InvalidOperationException("file dialog should not open"),
            (path, userEdited) =>
            {
                events.Add($"save:{path}:{userEdited}");
                return path;
            });

        Assert.Equal("C:\\Initial\\video.mp4", result);
        Assert.Equal(
            new[]
            {
                "exists:C:\\Initial",
                "resolve:C:\\Initial",
                "save:C:\\Initial\\video.mp4:False"
            },
            events);
    }

    [Fact]
    public void Resolve_persists_selected_folder_then_shows_info_before_manual_file_picker()
    {
        var record = new HaltungRecord();
        var events = new List<string>();

        var result = DataPageVideoPathWorkflowController.Resolve(
            record,
            null,
            "C:\\Initial",
            _ => null,
            _ => false,
            (folder, rec) =>
            {
                Assert.Same(record, rec);
                events.Add("resolve:" + folder);
                return Failure("Kein eindeutiges Video gefunden.");
            },
            (title, initialFolder) =>
            {
                events.Add($"select:{title}:{initialFolder}");
                return "C:\\Selected";
            },
            folder => events.Add("persist:" + folder),
            (message, title) => events.Add($"info:{title}:{message}"),
            (title, filter, initialFolder) =>
            {
                Assert.Equal(MediaFileTypes.VideoDialogFilter, filter);
                events.Add($"open:{title}:{initialFolder}");
                return "C:\\Selected\\manual.mp4";
            },
            (path, userEdited) =>
            {
                events.Add($"save:{path}:{userEdited}");
                return path;
            });

        Assert.Equal("C:\\Selected\\manual.mp4", result);
        Assert.Equal(
            new[]
            {
                "select:Video-Ordner auswaehlen:C:\\Initial",
                "persist:C:\\Selected",
                "resolve:C:\\Selected",
                "info:Video:Kein eindeutiges Video gefunden.",
                "open:Video auswaehlen:C:\\Selected",
                "save:C:\\Selected\\manual.mp4:True"
            },
            events);
    }

    [Fact]
    public void Resolve_uses_selected_folder_search_result_before_manual_file_picker()
    {
        var record = new HaltungRecord();
        var events = new List<string>();

        var result = DataPageVideoPathWorkflowController.Resolve(
            record,
            "",
            null,
            _ => null,
            _ => false,
            (folder, _) =>
            {
                events.Add("resolve:" + folder);
                return Success("C:\\Selected\\found.mp4");
            },
            (_, _) =>
            {
                events.Add("select");
                return "C:\\Selected";
            },
            folder => events.Add("persist:" + folder),
            (_, _) => throw new InvalidOperationException("info dialog should not be shown"),
            (_, _, _) => throw new InvalidOperationException("file dialog should not open"),
            (path, userEdited) =>
            {
                events.Add($"save:{path}:{userEdited}");
                return path;
            });

        Assert.Equal("C:\\Selected\\found.mp4", result);
        Assert.Equal(
            new[]
            {
                "select",
                "persist:C:\\Selected",
                "resolve:C:\\Selected",
                "save:C:\\Selected\\found.mp4:False"
            },
            events);
    }

    [Fact]
    public void Resolve_returns_null_when_folder_selection_is_cancelled()
    {
        var record = new HaltungRecord();

        var result = DataPageVideoPathWorkflowController.Resolve(
            record,
            null,
            "C:\\Initial",
            _ => null,
            _ => false,
            (_, _) => throw new InvalidOperationException("video search should not run"),
            (title, initialFolder) =>
            {
                Assert.Equal("Video-Ordner auswaehlen", title);
                Assert.Equal("C:\\Initial", initialFolder);
                return "";
            },
            _ => throw new InvalidOperationException("folder should not be persisted"),
            (_, _) => throw new InvalidOperationException("info dialog should not be shown"),
            (_, _, _) => throw new InvalidOperationException("file dialog should not open"),
            (_, _) => throw new InvalidOperationException("video link should not be saved"));

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_returns_null_when_manual_file_selection_is_cancelled()
    {
        var record = new HaltungRecord();
        var events = new List<string>();

        var result = DataPageVideoPathWorkflowController.Resolve(
            record,
            null,
            null,
            _ => null,
            _ => false,
            (folder, _) =>
            {
                events.Add("resolve:" + folder);
                return Failure("Kein eindeutiges Video gefunden.");
            },
            (_, _) =>
            {
                events.Add("select");
                return "C:\\Selected";
            },
            folder => events.Add("persist:" + folder),
            (message, title) => events.Add($"info:{title}:{message}"),
            (_, _, _) =>
            {
                events.Add("open");
                return "";
            },
            (_, _) => throw new InvalidOperationException("video link should not be saved"));

        Assert.Null(result);
        Assert.Equal(
            new[]
            {
                "select",
                "persist:C:\\Selected",
                "resolve:C:\\Selected",
                "info:Video:Kein eindeutiges Video gefunden.",
                "open"
            },
            events);
    }

    private static VideoResolveResult Success(string path)
        => new(true, "OK", "12.34-56.78", null, null, path);

    private static VideoResolveResult Failure(string message)
        => new(false, message, "12.34-56.78", null, null, null);
}
