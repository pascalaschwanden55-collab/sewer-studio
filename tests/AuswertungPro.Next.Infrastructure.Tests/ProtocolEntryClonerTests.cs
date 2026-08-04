using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer ProtocolEntryCloner (IST-Verhalten von CloneLegacyProtocolEntry).
/// </summary>
public sealed class ProtocolEntryClonerTests
{
    [Fact]
    public void Clone_EinfacherEintrag_KopiertAlleSkalarFelder()
    {
        var source = new ProtocolEntry
        {
            EntryId = Guid.NewGuid(),
            Code = "BAB",
            Beschreibung = "Laengsriss",
            MeterStart = 5.5,
            MeterEnd = 6.0,
            IsStreckenschaden = true,
            Mpeg = "frame_042.jpg",
            Zeit = TimeSpan.FromSeconds(42),
            Source = ProtocolEntrySource.Ai,
            IsDeleted = false
        };

        var clone = ProtocolEntryCloner.CloneLegacyProtocolEntry(source);

        Assert.NotSame(source, clone);
        Assert.Equal(source.EntryId, clone.EntryId);
        Assert.Equal("BAB", clone.Code);
        Assert.Equal("Laengsriss", clone.Beschreibung);
        Assert.Equal(5.5, clone.MeterStart);
        Assert.Equal(6.0, clone.MeterEnd);
        Assert.True(clone.IsStreckenschaden);
        Assert.Equal("frame_042.jpg", clone.Mpeg);
        Assert.Equal(TimeSpan.FromSeconds(42), clone.Zeit);
        Assert.Equal(ProtocolEntrySource.Ai, clone.Source);
        Assert.False(clone.IsDeleted);
    }

    [Fact]
    public void Clone_FotoPathsWerdenTiefKopiert()
    {
        var source = new ProtocolEntry
        {
            FotoPaths = new List<string> { "foto1.jpg", "foto2.jpg" },
            OriginalFotoPaths = new List<string> { "original1.jpg", "original2.jpg" }
        };

        var clone = ProtocolEntryCloner.CloneLegacyProtocolEntry(source);

        Assert.NotSame(source.FotoPaths, clone.FotoPaths);
        Assert.Equal(new[] { "foto1.jpg", "foto2.jpg" }, clone.FotoPaths);
        Assert.NotSame(source.OriginalFotoPaths, clone.OriginalFotoPaths);
        Assert.Equal(new[] { "original1.jpg", "original2.jpg" }, clone.OriginalFotoPaths);

        // Aenderung am Original darf den Klon nicht beeinflussen
        source.FotoPaths.Add("foto3.jpg");
        source.OriginalFotoPaths.Add("original3.jpg");
        Assert.Equal(2, clone.FotoPaths.Count);
        Assert.Equal(2, clone.OriginalFotoPaths.Count);
    }

    [Fact]
    public void Clone_TrainingMetaWirdTiefKopiert()
    {
        var source = new ProtocolEntry
        {
            Training = new ProtocolEntryTrainingMeta
            {
                SkipAutomaticPersistence = true,
                SkipReason = "Fotoannotation bereits separat gespeichert",
                PhotoAnnotationSampleIds = new List<string> { "sample-1", "sample-2" }
            }
        };

        var clone = ProtocolEntryCloner.CloneLegacyProtocolEntry(source);

        Assert.NotNull(clone.Training);
        Assert.NotSame(source.Training, clone.Training);
        Assert.True(clone.Training!.SkipAutomaticPersistence);
        Assert.Equal("Fotoannotation bereits separat gespeichert", clone.Training.SkipReason);
        Assert.NotSame(source.Training.PhotoAnnotationSampleIds, clone.Training.PhotoAnnotationSampleIds);
        Assert.Equal(new[] { "sample-1", "sample-2" }, clone.Training.PhotoAnnotationSampleIds);

        source.Training.PhotoAnnotationSampleIds.Add("sample-3");
        Assert.Equal(2, clone.Training.PhotoAnnotationSampleIds.Count);
    }

    [Fact]
    public void Clone_CodeMetaWirdTiefKopiert()
    {
        var source = new ProtocolEntry
        {
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BAB",
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["char1"] = "A"
                },
                Severity = "3",
                Count = 1,
                Notes = "Pruefung noetig",
                UpdatedAt = DateTimeOffset.UnixEpoch
            }
        };

        var clone = ProtocolEntryCloner.CloneLegacyProtocolEntry(source);

        Assert.NotNull(clone.CodeMeta);
        Assert.NotSame(source.CodeMeta, clone.CodeMeta);
        Assert.Equal("BAB", clone.CodeMeta!.Code);
        Assert.Equal("3", clone.CodeMeta.Severity);
        Assert.Equal(1, clone.CodeMeta.Count);
        Assert.Equal("Pruefung noetig", clone.CodeMeta.Notes);
        Assert.Equal(DateTimeOffset.UnixEpoch, clone.CodeMeta.UpdatedAt);

        // Tiefkopie der Parameters-Dictionary
        Assert.NotSame(source.CodeMeta.Parameters, clone.CodeMeta.Parameters);
        Assert.Equal("A", clone.CodeMeta.Parameters["char1"]);
    }

    [Fact]
    public void Clone_AiMetaWirdTiefKopiert()
    {
        var source = new ProtocolEntry
        {
            Ai = new ProtocolEntryAiMeta
            {
                SuggestedCode = "BBA",
                Confidence = 0.87,
                Reason = "Wurzeleinwuchs sichtbar",
                Flags = new List<string> { "low_quality" },
                Accepted = true,
                FinalCode = "BBA",
                MeterSource = "LinearEstimate",
                IsMeterEstimated = true,
                CentralDecision = new AiDecisionAudit
                {
                    Outcome = "Review",
                    ReasonCode = "KbMissing",
                    PolicyVersion = "central-ai-release-v2",
                    Signals = new AiDecisionSignalAudit { Confidence = 0.87 },
                    QualityGateWeights = new Dictionary<string, double> { ["LlmCodeConf"] = 1.0 }
                },
                SuggestedAt = DateTimeOffset.UnixEpoch
            }
        };

        var clone = ProtocolEntryCloner.CloneLegacyProtocolEntry(source);

        Assert.NotNull(clone.Ai);
        Assert.NotSame(source.Ai, clone.Ai);
        Assert.Equal("BBA", clone.Ai!.SuggestedCode);
        Assert.Equal(0.87, clone.Ai.Confidence);
        Assert.Equal("Wurzeleinwuchs sichtbar", clone.Ai.Reason);
        Assert.True(clone.Ai.Accepted);
        Assert.Equal("BBA", clone.Ai.FinalCode);
        Assert.Equal("LinearEstimate", clone.Ai.MeterSource);
        Assert.True(clone.Ai.IsMeterEstimated);
        Assert.Equal(DateTimeOffset.UnixEpoch, clone.Ai.SuggestedAt);
        Assert.NotSame(source.Ai.CentralDecision, clone.Ai.CentralDecision);
        Assert.Equal("KbMissing", clone.Ai.CentralDecision!.ReasonCode);
        Assert.NotSame(
            source.Ai.CentralDecision!.QualityGateWeights,
            clone.Ai.CentralDecision.QualityGateWeights);

        // Tiefkopie der Flags-Liste
        Assert.NotSame(source.Ai.Flags, clone.Ai.Flags);
        Assert.Equal(new[] { "low_quality" }, clone.Ai.Flags);

        // Aenderung am Original darf den Klon nicht beeinflussen
        source.Ai.Flags.Add("extra");
        Assert.Single(clone.Ai.Flags);
    }

    [Fact]
    public void Clone_NullCodeMeta_BleibtNull()
    {
        var source = new ProtocolEntry { CodeMeta = null };
        var clone = ProtocolEntryCloner.CloneLegacyProtocolEntry(source);
        Assert.Null(clone.CodeMeta);
    }

    [Fact]
    public void Clone_NullAi_BleibtNull()
    {
        var source = new ProtocolEntry { Ai = null };
        var clone = ProtocolEntryCloner.CloneLegacyProtocolEntry(source);
        Assert.Null(clone.Ai);
    }

    [Fact]
    public void Clone_NullTraining_BleibtNull()
    {
        var source = new ProtocolEntry { Training = null };
        var clone = ProtocolEntryCloner.CloneLegacyProtocolEntry(source);
        Assert.Null(clone.Training);
    }
}
