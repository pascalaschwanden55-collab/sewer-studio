using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die UI-freien Foto-Pfad-Selektoren der generalisierten Hover-Foto-Vorschau.
/// Kernregel: ein Selektor liefert bei fremdem Typ null (dann greift Fallback/leer),
/// bei eigenem Typ die (ggf. leere) Pfadliste. Der Dispatcher ExtractPhotoPaths setzt
/// Selektor VOR ProtocolEntry-Fallback VOR leer.
/// </summary>
public sealed class PhotoHoverPreviewSelectorsTests
{
    // ── ExtractPhotoPaths (Dispatcher) ──

    [Fact]
    public void ExtractPhotoPaths_uses_selector_result_over_entry_fallback()
    {
        var entry = new ProtocolEntry { FotoPaths = { @"C:\fallback.jpg" } };

        var paths = PhotoHoverPreviewSelectors.ExtractPhotoPaths(
            entry, _ => new[] { @"C:\selector.jpg" });

        Assert.Equal([@"C:\selector.jpg"], paths);
    }

    [Fact]
    public void ExtractPhotoPaths_falls_back_to_protocol_entry_without_selector()
    {
        var entry = new ProtocolEntry { FotoPaths = { @"C:\a.jpg", @"C:\b.jpg" } };

        var paths = PhotoHoverPreviewSelectors.ExtractPhotoPaths(entry, selector: null);

        Assert.Equal([@"C:\a.jpg", @"C:\b.jpg"], paths);
    }

    [Fact]
    public void ExtractPhotoPaths_returns_empty_for_null_item()
    {
        Assert.Empty(PhotoHoverPreviewSelectors.ExtractPhotoPaths(null, selector: null));
    }

    [Fact]
    public void ExtractPhotoPaths_returns_empty_for_unknown_type_without_selector()
    {
        Assert.Empty(PhotoHoverPreviewSelectors.ExtractPhotoPaths(new object(), selector: null));
    }

    [Fact]
    public void ExtractPhotoPaths_honours_empty_selector_result_over_entry_fallback()
    {
        // Selektor gewinnt auch dann, wenn er bewusst leer liefert.
        var entry = new ProtocolEntry { FotoPaths = { @"C:\a.jpg" } };

        var paths = PhotoHoverPreviewSelectors.ExtractPhotoPaths(entry, _ => Array.Empty<string>());

        Assert.Empty(paths);
    }

    // ── CodingEvent ──

    [Fact]
    public void CodingEventPhotos_returns_entry_foto_paths()
    {
        var ev = new CodingEvent();
        ev.Entry.FotoPaths.Add(@"C:\c.jpg");

        Assert.Equal([@"C:\c.jpg"], PhotoHoverPreviewSelectors.CodingEventPhotos(ev));
    }

    [Fact]
    public void CodingEventPhotos_returns_null_for_other_type()
    {
        Assert.Null(PhotoHoverPreviewSelectors.CodingEventPhotos(new object()));
    }

    // ── MediaMatchRow ──

    [Fact]
    public void MediaMatchRowPhotos_returns_match_foto_paths()
    {
        var match = new MediaMatch(
            new HaltungRecord(), "H",
            MediaMatchStatus.NotFound, null, null,
            MediaMatchStatus.NotFound, null, null,
            MediaMatchStatus.Found, new List<string> { @"C:\m.jpg" }, false);
        var row = new MediaMatchRow(match);

        Assert.Equal([@"C:\m.jpg"], PhotoHoverPreviewSelectors.MediaMatchRowPhotos(row));
    }

    [Fact]
    public void MediaMatchRowPhotos_returns_null_for_other_type()
    {
        Assert.Null(PhotoHoverPreviewSelectors.MediaMatchRowPhotos(new object()));
    }

    // ── TrainingSample ──

    [Fact]
    public void TrainingSamplePhotos_orders_frame_evidence_then_additional()
    {
        var sample = new TrainingSample
        {
            FramePath = @"C:\frame.jpg",
            EvidenceFramePath = @"C:\evidence.jpg",
            AdditionalFramePaths = new List<string> { @"C:\extra1.jpg", @"C:\extra2.jpg" }
        };

        Assert.Equal(
            [@"C:\frame.jpg", @"C:\evidence.jpg", @"C:\extra1.jpg", @"C:\extra2.jpg"],
            PhotoHoverPreviewSelectors.TrainingSamplePhotos(sample));
    }

    [Fact]
    public void TrainingSamplePhotos_drops_empty_frame_and_null_sources()
    {
        var sample = new TrainingSample
        {
            FramePath = "",
            EvidenceFramePath = null,
            AdditionalFramePaths = null
        };

        Assert.Empty(PhotoHoverPreviewSelectors.TrainingSamplePhotos(sample)!);
    }

    [Fact]
    public void TrainingSamplePhotos_returns_null_for_other_type()
    {
        Assert.Null(PhotoHoverPreviewSelectors.TrainingSamplePhotos(new object()));
    }

    // ── ReviewQueueItem (Review-Queue-Liste bindet ReviewQueueItem, nicht ReviewCardViewModel) ──

    [Fact]
    public void ReviewQueueItemPhotos_returns_self_training_frame_path()
    {
        var item = new ReviewQueueItem("id", null, 0.5, default) { SelfTrainingFramePath = @"C:\r.jpg" };

        Assert.Equal([@"C:\r.jpg"], PhotoHoverPreviewSelectors.ReviewQueueItemPhotos(item));
    }

    [Fact]
    public void ReviewQueueItemPhotos_returns_empty_when_frame_path_null()
    {
        var item = new ReviewQueueItem("id", null, 0.5, default) { SelfTrainingFramePath = null };

        Assert.Empty(PhotoHoverPreviewSelectors.ReviewQueueItemPhotos(item)!);
    }

    [Fact]
    public void ReviewQueueItemPhotos_returns_null_for_other_type()
    {
        Assert.Null(PhotoHoverPreviewSelectors.ReviewQueueItemPhotos(new object()));
    }

    // ── TeacherAnnotation ──

    [Fact]
    public void TeacherAnnotationPhotos_prefers_cropped_over_full()
    {
        var annotation = new TeacherAnnotation
        {
            CroppedRegionPath = @"C:\crop.jpg",
            FullFramePath = @"C:\full.jpg"
        };

        Assert.Equal([@"C:\crop.jpg"], PhotoHoverPreviewSelectors.TeacherAnnotationPhotos(annotation));
    }

    [Fact]
    public void TeacherAnnotationPhotos_falls_back_to_full_when_no_crop()
    {
        var annotation = new TeacherAnnotation
        {
            CroppedRegionPath = null,
            FullFramePath = @"C:\full.jpg"
        };

        Assert.Equal([@"C:\full.jpg"], PhotoHoverPreviewSelectors.TeacherAnnotationPhotos(annotation));
    }

    [Fact]
    public void TeacherAnnotationPhotos_returns_empty_when_both_null()
    {
        var annotation = new TeacherAnnotation { CroppedRegionPath = null, FullFramePath = null };

        Assert.Empty(PhotoHoverPreviewSelectors.TeacherAnnotationPhotos(annotation)!);
    }

    [Fact]
    public void TeacherAnnotationPhotos_returns_null_for_other_type()
    {
        Assert.Null(PhotoHoverPreviewSelectors.TeacherAnnotationPhotos(new object()));
    }
}
