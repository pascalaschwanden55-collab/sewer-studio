using System;
using System.Collections.Generic;
using System.Globalization;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuswertungPro.Next.Application.Reports;

public sealed class AiDocumentationPdfBuilder
{
    public byte[] BuildPdf(DateTimeOffset? generatedAt = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var created = (generatedAt ?? DateTimeOffset.Now).ToLocalTime();
        var createdText = created.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        var flowSteps = new List<string>
        {
            "UI: Button \"KI-Vorschlag\" im Protokolleintrag.",
            "AiInput bauen (Meter, Code/Text, AllowedCodes, Video/Zeit, Fotos).",
            "ProtocolAi Service: OllamaProtocolAiService oder Noop (deaktiviert).",
            "Optional: Frame aus Video (ffmpeg) oder erstes Foto.",
            "Vision-Findings + Meter (Ollama Vision).",
            "Prompt + Training-Samples (protocol_training.json).",
            "Text-LLM (Ollama) -> AiSuggestion, UI/Entry aktualisieren."
        };

        var enableSteps = new List<string>
        {
            "AUSWERTUNGPRO_AI_ENABLED=1",
            "Ollama laeuft (AUSWERTUNGPRO_OLLAMA_URL, Default http://localhost:11434)",
            "Modelle: AUSWERTUNGPRO_AI_VISION_MODEL (qwen2.5vl:7b)",
            "Modelle: AUSWERTUNGPRO_AI_TEXT_MODEL (gpt-oss:20b)",
            "FFmpeg verfuegbar (AUSWERTUNGPRO_FFMPEG oder PATH)",
            "VSA Code-Katalog vorhanden (XML/JSON)"
        };

        var runtimeSteps = new List<string>
        {
            "MeterStart/MeterEnd/Zeit gueltig (keine Formatfehler).",
            "Code-Katalog geladen (AllowedCodes != leer).",
            "Video- oder Foto-Pfad vorhanden, falls Bildanalyse gewuenscht."
        };

        var logSteps = new List<string>
        {
            "%LOCALAPPDATA%\\SewerStudio\\logs",
            "%LOCALAPPDATA%\\SewerStudio\\data\\protocol_training.json",
            "%LOCALAPPDATA%\\SewerStudio\\data\\measures_learning.json",
            "Data\\measures-model.zip (Massnahmen-KI Modell)"
        };

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(26);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("AuswertungPro - KI Uebersicht").FontSize(18).Bold();
                    col.Item().Text($"Stand: {createdText}").FontSize(9).FontColor(Colors.Grey.Darken2);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text("Flussdiagramm (KI-Vorschlag Protokolleintrag)").FontSize(12).Bold();
                    col.Item().Element(c => ComposeNumberedList(c, flowSteps));

                    col.Item().LineHorizontal(0.5f);

                    col.Item().Text("Checkliste – Enable Steps").FontSize(12).Bold();
                    col.Item().Element(c => ComposeBulletList(c, enableSteps));

                    col.Item().PaddingTop(4).Text("Checkliste - Laufzeit Voraussetzungen").FontSize(12).Bold();
                    col.Item().Element(c => ComposeBulletList(c, runtimeSteps));

                    col.Item().PaddingTop(4).Text("Logs & Datenablage").FontSize(12).Bold();
                    col.Item().Element(c => ComposeBulletList(c, logSteps));

                    col.Item().PaddingTop(4).Text("Hinweis Massnahmen-KI (Lernlogik)").FontSize(12).Bold();
                    col.Item().Text("Empfehlungen werden aus gelernten Faellen erzeugt (MeasureRecommendationService). " +
                                   "Training erfolgt beim Speichern; Status ueber \"KI-Status\".").FontSize(9);
                });

                page.Footer().AlignRight().Text("AuswertungPro – KI").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf();
    }

    private static void ComposeNumberedList(IContainer container, IReadOnlyList<string> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn();
            });

            for (var i = 0; i < items.Count; i++)
            {
                table.Cell().PaddingVertical(2).Text($"{i + 1}.").SemiBold();
                table.Cell().PaddingVertical(2).Text(items[i]);
            }
        });
    }

    private static void ComposeBulletList(IContainer container, IReadOnlyList<string> items)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(12);
                columns.RelativeColumn();
            });

            foreach (var item in items)
            {
                table.Cell().PaddingVertical(2).Text("-");
                table.Cell().PaddingVertical(2).Text(item);
            }
        });
    }
}
