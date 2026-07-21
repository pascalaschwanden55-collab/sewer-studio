using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.Inventory;

public sealed class TrainingDataInventoryJsonTests
{
    [Fact]
    public void Serialize_VerwendetStabileStringEnumsUndKeinQuarantineFlag()
    {
        var report = CreateReport();

        var jsonBytes = TrainingDataInventoryJson.SerializeToUtf8Bytes(report);
        using var json = JsonDocument.Parse(jsonBytes);

        var root = json.RootElement;
        Assert.Equal(TrainingDataInventoryReportSchema.CurrentSchemaVersion, root.GetProperty("schemaVersion").GetString());
        var teacher = root.GetProperty("teacherRecords")[0];
        Assert.Equal(JsonValueKind.String, teacher.GetProperty("disposition").ValueKind);
        Assert.Equal("quarantineOrigin", teacher.GetProperty("disposition").GetString());
        Assert.Equal("valid", teacher.GetProperty("boxState").GetString());
        Assert.Equal("existing", teacher.GetProperty("fullFrame").GetProperty("state").GetString());
        Assert.Equal("computed", teacher.GetProperty("fullFrame").GetProperty("hashState").GetString());
        Assert.False(teacher.TryGetProperty("quarantineFlag", out _));
        Assert.False(teacher.TryGetProperty("hasPositiveArea", out _));
        Assert.False(teacher.GetProperty("fullFrame").TryGetProperty("exists", out _));
        Assert.False(root.GetProperty("evalProtection").TryGetProperty("complete", out _));
        Assert.False(root.GetProperty("summary").GetProperty("triage").TryGetProperty("total", out _));
    }

    [Fact]
    public void Deserialize_LehntNumerischeEnumsAb()
    {
        var json = Encoding.UTF8.GetString(TrainingDataInventoryJson.SerializeToUtf8Bytes(CreateReport()));
        var numericEnumJson = json.Replace(
            "\"disposition\": \"quarantineOrigin\"",
            "\"disposition\": 1",
            StringComparison.Ordinal);
        Assert.NotEqual(json, numericEnumJson);

        Assert.Throws<JsonException>(() =>
            TrainingDataInventoryJson.Deserialize(Encoding.UTF8.GetBytes(numericEnumJson)));
    }

    [Fact]
    public void SerializeUndDeserialize_ErhaeltNeuePfadUndHashZustaende()
    {
        var source = CreateReport();

        var restored = TrainingDataInventoryJson.Deserialize(
            TrainingDataInventoryJson.SerializeToUtf8Bytes(source));

        var frame = Assert.Single(restored.TeacherRecords).FullFrame;
        Assert.Equal(TrainingInventoryPathState.Existing, frame.State);
        Assert.Equal(TrainingInventoryHashState.Computed, frame.HashState);
        Assert.True(frame.Exists);
        Assert.False(frame.IsProtected);
    }

    [Fact]
    public void Serialize_LehntWiderspruechlicheZusammenfassungAb()
    {
        var valid = CreateReport();
        var invalid = new TrainingDataInventoryReport
        {
            KnowledgeRoot = valid.KnowledgeRoot,
            GeneratedUtc = valid.GeneratedUtc,
            TeacherRecords = valid.TeacherRecords
        };

        Assert.Throws<InvalidDataException>(() =>
            TrainingDataInventoryJson.SerializeToUtf8Bytes(invalid));
    }

    [Fact]
    public void Deserialize_LehntBerechnetenHashOhneSha256Ab()
    {
        var json = Encoding.UTF8.GetString(TrainingDataInventoryJson.SerializeToUtf8Bytes(CreateReport()));
        var invalid = json.Replace(
            $"\"sha256\": \"{new string('a', 64)}\"",
            "\"sha256\": null",
            StringComparison.Ordinal);
        Assert.NotEqual(json, invalid);

        Assert.Throws<InvalidDataException>(() =>
            TrainingDataInventoryJson.Deserialize(Encoding.UTF8.GetBytes(invalid)));
    }

    [Fact]
    public void Serialize_LehntDoppelteRecordKeysAb()
    {
        var valid = CreateReport();
        var record = Assert.Single(valid.TeacherRecords);
        var records = new[] { record, record };
        var invalid = new TrainingDataInventoryReport
        {
            KnowledgeRoot = valid.KnowledgeRoot,
            GeneratedUtc = valid.GeneratedUtc,
            TeacherRecords = records,
            Summary = TrainingInventorySummaryBuilder.Build(records, [])
        };

        Assert.Throws<InvalidDataException>(() =>
            TrainingDataInventoryJson.SerializeToUtf8Bytes(invalid));
    }

    [Fact]
    public void Deserialize_LehntFehlendeSchemaversionAb()
    {
        var root = CreateMutableJson();
        Assert.True(root.Remove("schemaVersion"));

        Assert.Throws<JsonException>(() => Deserialize(root));
    }

    [Fact]
    public void Deserialize_LehntUnbekannteFelderAb()
    {
        var root = CreateMutableJson();
        root["unexpectedField"] = true;

        Assert.Throws<JsonException>(() => Deserialize(root));
    }

    [Fact]
    public void Deserialize_LehntNullStattPflichtlisteAb()
    {
        var root = CreateMutableJson();
        root["teacherRecords"] = null;

        Assert.Throws<InvalidDataException>(() => Deserialize(root));
    }

    [Fact]
    public void Deserialize_LehntManipulierteDispositionAb()
    {
        var root = CreateMutableJson();
        var teacher = root["teacherRecords"]!.AsArray()[0]!.AsObject();
        teacher["disposition"] = "evaluationNotChecked";

        Assert.Throws<InvalidDataException>(() => Deserialize(root));
    }

    [Fact]
    public void Deserialize_LehntPfadvorschlagAusserhalbDerSuchwurzelnAb()
    {
        var root = CreateMutableJson();
        var frame = root["teacherRecords"]!.AsArray()[0]!.AsObject()["fullFrame"]!.AsObject();
        frame["state"] = "suggestedForManualReview";
        frame["existingPath"] = null;
        frame["suggestedPath"] = @"C:\fremd\frame.png";
        frame["candidates"] = new JsonArray(@"C:\fremd\frame.png");
        frame["hashState"] = "notApplicable";
        frame["sha256"] = null;

        Assert.Throws<InvalidDataException>(() => Deserialize(root));
    }

    [Fact]
    public void Deserialize_LehntSauberenEvalStatusOhneBildHashAb()
    {
        var root = CreateMutableJson();
        var teacher = root["teacherRecords"]!.AsArray()[0]!.AsObject();
        var frame = teacher["fullFrame"]!.AsObject();
        frame["hashState"] = "notRequested";
        frame["sha256"] = null;
        teacher["holdingState"] = "explicit";
        teacher["evalState"] = "clean";
        teacher["disposition"] = "trainValCandidate";

        Assert.Throws<InvalidDataException>(() => Deserialize(root));
    }

    private static JsonObject CreateMutableJson()
        => JsonNode.Parse(TrainingDataInventoryJson.SerializeToUtf8Bytes(CreateReport()))!
            .AsObject();

    private static TrainingDataInventoryReport Deserialize(JsonObject root)
        => TrainingDataInventoryJson.Deserialize(Encoding.UTF8.GetBytes(root.ToJsonString()));

    private static TrainingDataInventoryReport CreateReport()
    {
        var fullFrame = new TrainingInventoryPathReference
        {
            StoredPath = @"teacher_images\frame.png",
            State = TrainingInventoryPathState.Existing,
            HashState = TrainingInventoryHashState.Computed,
            ExistingPath = @"C:\KI_BRAIN\teacher_images\frame.png",
            Sha256 = new string('a', 64)
        };
        var holding = new TeacherInventoryHoldingAssessment(
            TrainingInventoryHoldingState.Unknown,
            null,
            []);
        var disposition = TeacherInventoryPolicy.ClassifyDisposition(
            fullFrame,
            holding.State,
            TrainingInventoryBoxState.Valid,
            TrainingInventoryEvalState.NotChecked);
        var record = new TeacherInventoryRecord
        {
            RecordKey = "record-1",
            VsaCode = "BABBB",
            BoxState = TrainingInventoryBoxState.Valid,
            HoldingState = holding.State,
            Disposition = disposition,
            EvalState = TrainingInventoryEvalState.NotChecked,
            FullFrame = fullFrame,
            ReasonCodes = TeacherInventoryPolicy.BuildReasonCodes(
                fullFrame,
                TrainingInventoryBoxState.Valid,
                holding,
                TrainingInventoryEvalState.NotChecked,
                disposition)
        };
        var records = new[] { record };
        return new TrainingDataInventoryReport
        {
            KnowledgeRoot = @"C:\KI_BRAIN",
            GeneratedUtc = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero),
            TeacherRecords = records,
            Summary = TrainingInventorySummaryBuilder.Build(records, [])
        };
    }
}
