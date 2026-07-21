using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchachtStammdatenResultApplierTests
{
    [Fact]
    public void Apply_fills_missing_fields_in_order_and_builds_base_summary()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Notiz", "bleibt");
        var changedFields = new List<string>();
        record.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName?.StartsWith("Fields[", StringComparison.Ordinal) == true)
                changedFields.Add(args.PropertyName);
        };

        var result = SchachtStammdatenResultApplier.Apply(
            [record],
            Result(
                additions:
                [
                    new SchachtStammdatenErgaenzung(
                        record.Id,
                        "protokoll.pdf",
                        "  rund  ",
                        " 1200 ",
                        " 2.40 ")
                ],
                pdfFound: 4,
                pdfMissing: 2,
                unreadable: 1,
                alreadyComplete: 3));

        Assert.Equal("rund", record.GetFieldValue("Schachtform"));
        Assert.Equal("1200", record.GetFieldValue("Dimension"));
        Assert.Equal("2.40", record.GetFieldValue("Schachttiefe"));
        Assert.Equal("bleibt", record.GetFieldValue("Notiz"));
        Assert.Equal(
            ["Fields[Schachtform]", "Fields[Dimension]", "Fields[Schachttiefe]"],
            changedFields);
        Assert.Equal(1, result.ChangedShaftCount);
        Assert.Equal(3, result.AddedFieldCount);
        Assert.Equal(
            "Ergaenzt: 1 Schaechte / 3 Felder. PDF gefunden: 4, ohne PDF: 2, " +
            "kein passendes Schachtprotokoll: 1, bereits vollstaendig: 3.",
            result.Summary);
        Assert.Empty(result.Details);
        Assert.Equal(result.Summary, result.DialogText);
    }

    [Fact]
    public void Apply_preserves_existing_values_skips_blank_values_and_unknown_records()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtform", "eckig");
        record.SetFieldValue("Schachttiefe", "   ");

        var result = SchachtStammdatenResultApplier.Apply(
            [record],
            Result(
                additions:
                [
                    new SchachtStammdatenErgaenzung(
                        record.Id,
                        "known.pdf",
                        "rund",
                        "   ",
                        " 2.00 "),
                    new SchachtStammdatenErgaenzung(
                        Guid.NewGuid(),
                        "unknown.pdf",
                        "oval",
                        "800",
                        "1.80")
                ]));

        Assert.Equal("eckig", record.GetFieldValue("Schachtform"));
        Assert.Empty(record.GetFieldValue("Dimension"));
        Assert.Equal("2.00", record.GetFieldValue("Schachttiefe"));
        Assert.Equal(1, result.ChangedShaftCount);
        Assert.Equal(1, result.AddedFieldCount);
    }

    [Fact]
    public void Apply_preserves_entry_based_changed_shaft_count_for_multiple_additions()
    {
        var record = new SchachtRecord();

        var result = SchachtStammdatenResultApplier.Apply(
            [record],
            Result(
                additions:
                [
                    new SchachtStammdatenErgaenzung(record.Id, "one.pdf", "rund", null, null),
                    new SchachtStammdatenErgaenzung(record.Id, "two.pdf", null, "1000", null)
                ]));

        Assert.Equal(2, result.ChangedShaftCount);
        Assert.Equal(2, result.AddedFieldCount);
    }

    [Fact]
    public void Apply_reports_zero_changes_when_known_record_needs_no_field_update()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtform", "rund");
        record.SetFieldValue("Dimension", "1000");
        record.SetFieldValue("Schachttiefe", "2.00");

        var result = SchachtStammdatenResultApplier.Apply(
            [record],
            Result(
                additions:
                [
                    new SchachtStammdatenErgaenzung(
                        record.Id,
                        "known.pdf",
                        "oval",
                        "   ",
                        null)
                ]));

        Assert.Equal("rund", record.GetFieldValue("Schachtform"));
        Assert.Equal("1000", record.GetFieldValue("Dimension"));
        Assert.Equal("2.00", record.GetFieldValue("Schachttiefe"));
        Assert.Equal(0, result.ChangedShaftCount);
        Assert.Equal(0, result.AddedFieldCount);
        Assert.StartsWith("Ergaenzt: 0 Schaechte / 0 Felder.", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_limits_details_to_twelve_messages_and_reports_remainder()
    {
        var messages = Enumerable.Range(1, 14)
            .Select(index => $"Hinweis {index}")
            .ToArray();

        var result = SchachtStammdatenResultApplier.Apply(
            [],
            Result(messages: messages));

        Assert.Equal(
            "\n\nHinweise:\n" +
            string.Join("\n", messages.Take(12)) +
            "\n... und 2 weitere Hinweise.",
            result.Details);
        Assert.Equal(result.Summary + result.Details, result.DialogText);
        Assert.DoesNotContain("Hinweis 13\n", result.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("Hinweis 14\n", result.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(12, null)]
    [InlineData(13, "... und 1 weitere Hinweise.")]
    public void Apply_handles_exact_message_boundaries(int messageCount, string? expectedRemainder)
    {
        var messages = Enumerable.Range(1, messageCount)
            .Select(index => $"Hinweis {index}")
            .ToArray();

        var result = SchachtStammdatenResultApplier.Apply(
            [],
            Result(messages: messages));

        Assert.Contains("Hinweis 12", result.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("Hinweis 13\n", result.Details, StringComparison.Ordinal);
        if (expectedRemainder is null)
            Assert.DoesNotContain("weitere Hinweise", result.Details, StringComparison.Ordinal);
        else
            Assert.EndsWith(expectedRemainder, result.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_invokes_before_apply_after_indexing_and_before_field_changes()
    {
        var record = new SchachtRecord();
        var calls = new List<string>();
        record.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "Fields[Schachtform]")
                calls.Add("field");
        };

        SchachtStammdatenResultApplier.Apply(
            [record],
            Result(
                additions:
                [new SchachtStammdatenErgaenzung(record.Id, "one.pdf", "rund", null, null)]),
            beforeApply: () =>
            {
                Assert.Empty(record.GetFieldValue("Schachtform"));
                calls.Add("before");
            });

        Assert.Equal(["before", "field"], calls);
    }

    [Fact]
    public void Apply_indexes_records_before_invoking_before_apply()
    {
        var duplicateId = Guid.NewGuid();
        var records = new[]
        {
            new SchachtRecord { Id = duplicateId },
            new SchachtRecord { Id = duplicateId }
        };
        var beforeApplyCalled = false;

        Assert.Throws<ArgumentException>(() => SchachtStammdatenResultApplier.Apply(
            records,
            Result(),
            beforeApply: () => beforeApplyCalled = true));

        Assert.False(beforeApplyCalled);
    }

    [Fact]
    public void Apply_propagates_before_apply_failure_without_mutating_fields()
    {
        var record = new SchachtRecord();
        var expected = new InvalidOperationException("Sicherung fehlgeschlagen");

        var actual = Assert.Throws<InvalidOperationException>(() =>
            SchachtStammdatenResultApplier.Apply(
                [record],
                Result(
                    additions:
                    [new SchachtStammdatenErgaenzung(record.Id, "one.pdf", "rund", null, null)]),
                beforeApply: () => throw expected));

        Assert.Same(expected, actual);
        Assert.Empty(record.GetFieldValue("Schachtform"));
        Assert.Empty(record.Fields);
    }

    private static SchachtStammdatenErgaenzungsErgebnis Result(
        IReadOnlyList<SchachtStammdatenErgaenzung>? additions = null,
        IReadOnlyList<string>? messages = null,
        int pdfFound = 0,
        int pdfMissing = 0,
        int unreadable = 0,
        int alreadyComplete = 0)
        => new(
            Gesamt: 0,
            BereitsVollstaendig: alreadyComplete,
            PdfGefunden: pdfFound,
            MitErgaenzung: additions?.Count ?? 0,
            PdfNichtGefunden: pdfMissing,
            NichtLesbar: unreadable,
            Ergaenzungen: additions ?? [],
            Meldungen: messages ?? []);
}
