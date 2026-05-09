using System.Collections.Generic;
using System.Globalization;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

// QuestPDF-Tabellen fuer Beobachtungs-Listen (Standard, Section, Long-List).
// Aus dem Hauptdatei extrahiert (Slice 1g).
public sealed partial class ProtocolPdfExporter
{
    private static void ComposeObservationTable(IContainer container, IReadOnlyList<ProtocolEntry> entries)
    {
        static IContainer HeaderCell(IContainer c)
            => c.Background("#E6F3F8").PaddingVertical(3).PaddingHorizontal(4);

        static IContainer BodyCell(IContainer c)
            => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(30);
                columns.ConstantColumn(60);
                columns.ConstantColumn(70);
                columns.ConstantColumn(95);
                columns.RelativeColumn(3);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Nr.").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Code").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Meter (m)").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zeit").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Beschreibung").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Parameter").FontSize(10).SemiBold();
            });

            var index = 1;
            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(index.ToString(CultureInfo.InvariantCulture)).FontSize(10);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationMeterText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationTimeText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(entry.Beschreibung ?? "").FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildParameterShortText(entry)).FontSize(10);
                index++;
            }
        });
    }

    private static void ComposeSectionObservationTable(IContainer container, IReadOnlyList<ProtocolEntry> entries)
    {
        static IContainer HeaderCell(IContainer c)
            => c.Background(Colors.Grey.Lighten3).PaddingVertical(3).PaddingHorizontal(4);

        static IContainer BodyCell(IContainer c)
            => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(55); // m+
                columns.ConstantColumn(70); // OP Kuerzel
                columns.RelativeColumn(5);  // Zustand
                columns.ConstantColumn(70); // MPEG
                columns.ConstantColumn(45); // Foto
                columns.ConstantColumn(45); // Stufe
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("m+").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("OP Kuerzel").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zustand").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("MPEG").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Foto").FontSize(10).SemiBold();
                header.Cell().Element(HeaderCell).Text("Stufe").FontSize(10).SemiBold();
            });

            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(BuildObservationMeterStartText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(10);
                table.Cell().Element(BodyCell).Text(entry.Beschreibung ?? "").FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationMpegText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationPhotoText(entry)).FontSize(10);
                table.Cell().Element(BodyCell).Text(BuildObservationStufeText(entry)).FontSize(10);
            }
        });
    }

    private static void ComposeObservationListTable(
        IContainer container,
        IReadOnlyList<ProtocolEntry> entries,
        IReadOnlyDictionary<ProtocolEntry, string>? photoNumbers,
        string? headerBackground = null)
    {
        var headerBg = string.IsNullOrWhiteSpace(headerBackground) ? "#EAF5F9" : headerBackground;

        IContainer HeaderCell(IContainer c)
            => c.Background(headerBg).PaddingVertical(3).PaddingHorizontal(4);

        static IContainer BodyCell(IContainer c)
            => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(4);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(38); // m+
                columns.ConstantColumn(38); // m-
                columns.ConstantColumn(55); // OP
                columns.RelativeColumn(6);  // Zustand
                columns.ConstantColumn(45); // Foto
                columns.ConstantColumn(55); // MPEG
                columns.ConstantColumn(45); // Zeit
                columns.RelativeColumn(2);  // Bemerkung
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("m+").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("m-").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("OP Kürzel").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zustand").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Foto").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("MPEG").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Zeit").FontSize(9).SemiBold();
                header.Cell().Element(HeaderCell).Text("Bemerkung").FontSize(9).SemiBold();
            });

            foreach (var entry in entries)
            {
                table.Cell().Element(BodyCell).Text(FmtMeterValue(entry.MeterStart)).FontSize(9);
                table.Cell().Element(BodyCell).Text(FmtMeterValue(entry.MeterEnd)).FontSize(9);
                table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(entry.Code) ? "-" : entry.Code.Trim()).FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationZustandTextLong(entry)).FontSize(9);
                table.Cell().Element(BodyCell).Text(ResolvePhotoNumberText(entry, photoNumbers)).FontSize(9);
                table.Cell().Element(BodyCell).Text(entry.Mpeg?.Trim() ?? "-").FontSize(9);
                table.Cell().Element(BodyCell).Text(entry.Zeit.HasValue ? FormatTime(entry.Zeit.Value) : "-").FontSize(9);
                table.Cell().Element(BodyCell).Text(BuildObservationNotesText(entry)).FontSize(9);
            }
        });
    }
}
