using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Dossiers.Preview;
using AuswertungPro.Next.Domain.Models.Dossiers;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DossierEmptyListFieldTests
{
    [Fact]
    public void Leere_Eigentuemerliste_besitzt_sofort_alle_sechs_Zellfelder()
    {
        RunOnSta(() =>
        {
            var dossier = new DossierDefinition();
            var panel = CreatePanel(new DossierAreaSettings(), dossier);

            BuildPrivate(panel, "BaueZeilenEditor", RowsField("Eigentuemer"));

            Assert.Single(dossier.Owners);
            Assert.All(new[]
            {
                "Haus_Nr", "Pz_Nr", "Eigentuemer_Zelle",
                "Telefon", "Mail", "Objektbewohner"
            }, cell => Assert.True(panel.Kennt(
                DossierPreviewTarget.RowCell("Eigentuemer", 0, cell)), cell));
        });
    }

    [Fact]
    public void Leere_Themenliste_besitzt_sofort_Titel_und_Bemerkungsfeld()
    {
        RunOnSta(() =>
        {
            var area = new DossierAreaSettings();
            var dossier = new DossierDefinition();
            var panel = CreatePanel(area, dossier);

            BuildPrivate(panel, "BaueThemenEditor", RowsField("Themen"));

            Assert.Single(dossier.Topics);
            Assert.True(panel.Kennt(
                DossierPreviewTarget.RowCell("Themen", 0, "Thema")));
            Assert.True(panel.Kennt(
                DossierPreviewTarget.RowCell("Themen", 0, "Text")));
        });
    }

    [Fact]
    public void Schreiben_in_die_leere_Themenzeile_materialisiert_genau_diese_Zeile()
    {
        RunOnSta(() =>
        {
            var area = new DossierAreaSettings();
            var dossier = new DossierDefinition();
            var panel = CreatePanel(area, dossier);

            var editor = BuildPrivate(panel, "BaueThemenEditor", RowsField("Themen"));
            var boxes = Descendants(editor).OfType<RichTextBox>().ToList();
            Assert.Equal(2, boxes.Count);

            DossierTopicRichTextEditor.SetValue(boxes[0], new DossierTopicRow
            {
                Text = "Neue Information",
                StyleRanges =
                [
                    new DossierTextStyleRange
                    {
                        Start = 0,
                        Length = 4,
                        Bold = true,
                        ColorHex = "C00000"
                    }
                ]
            });
            DossierTopicRichTextEditor.SetValue(boxes[1], new DossierTopicRow
            {
                Text = "Frei bearbeitbarer Text"
            });

            var stored = Assert.Single(dossier.Topics);
            Assert.Equal("Neue Information", stored.Title);
            Assert.Equal("Frei bearbeitbarer Text", stored.Text);

            var resolved = Assert.Single(DossierTopicResolver.Resolve(area, dossier));
            Assert.Equal("Neue Information", resolved.Title);
            Assert.Equal("Frei bearbeitbarer Text", resolved.Text);
            Assert.NotEmpty(DossierTopicTitleEditing.Styles(
                dossier, "Neue Information", "Neue Information"));
        });
    }

    [Fact]
    public void Mehrere_leere_Themenzeilen_bearbeiten_jeweils_ihre_eigene_Zeile()
    {
        RunOnSta(() =>
        {
            var dossier = new DossierDefinition
            {
                Topics = [new DossierTopicRow(), new DossierTopicRow()]
            };
            var panel = CreatePanel(new DossierAreaSettings(), dossier);

            var editor = BuildPrivate(panel, "BaueThemenEditor", RowsField("Themen"));
            var boxes = Descendants(editor).OfType<RichTextBox>().ToList();
            Assert.Equal(4, boxes.Count);

            DossierTopicRichTextEditor.SetValue(
                boxes[2], new DossierTopicRow { Text = "Zweite Zeile" });
            DossierTopicRichTextEditor.SetValue(
                boxes[3], new DossierTopicRow { Text = "Zweiter Text" });

            Assert.True(string.IsNullOrWhiteSpace(dossier.Topics[0].Title));
            Assert.True(string.IsNullOrWhiteSpace(dossier.Topics[0].Text));
            Assert.Equal("Zweite Zeile", dossier.Topics[1].Title);
            Assert.Equal("Zweiter Text", dossier.Topics[1].Text);

            var remove = Descendants(editor).OfType<Button>()
                .Where(button => Equals(button.Content, "✕"))
                .ToList();
            Assert.Equal(2, remove.Count);
            remove[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var remaining = Assert.Single(dossier.Topics);
            Assert.True(string.IsNullOrWhiteSpace(remaining.Title));
            Assert.True(string.IsNullOrWhiteSpace(remaining.Text));
        });
    }

    [Fact]
    public void Neu_benannte_Schaedenzeile_zeigt_sofort_den_Listenimport()
    {
        RunOnSta(() =>
        {
            var dossier = new DossierDefinition();
            var panel = CreatePanel(new DossierAreaSettings(), dossier);
            var editor = BuildPrivate(panel, "BaueThemenEditor", RowsField("Themen"));
            var title = Descendants(editor).OfType<RichTextBox>().First();

            Assert.DoesNotContain(
                Descendants(editor).OfType<Button>(),
                button => Equals(button.Content, "Import aus Liste"));

            DossierTopicRichTextEditor.SetValue(
                title, new DossierTopicRow { Text = "Schäden" });

            Assert.Contains(
                Descendants(editor).OfType<Button>(),
                button => Equals(button.Content, "Import aus Liste"));
        });
    }

    [Fact]
    public void Erzeugte_Eigentuemerbeschriftungen_haben_eigene_Klickziele()
    {
        RunOnSta(() =>
        {
            var dossier = new DossierDefinition
            {
                Owners = [new DossierOwnerRow { Phone = "041" }]
            };
            var panel = CreatePanel(new DossierAreaSettings(), dossier);

            BuildPrivate(panel, "BaueZeilenEditor", RowsField("Eigentuemer"));

            Assert.All(DossierOwnerCellLabels.All, label => Assert.True(panel.Kennt(
                DossierPreviewTarget.RowCell("Eigentuemer", 0, label.CellKey)),
                label.CellKey));
        });
    }

    private static DossierPreviewField RowsField(string key)
        => new(key, key, DossierPreviewFieldKind.Rows, () => string.Empty, null);

    private static UIElement BuildPrivate(
        DossierPreviewFieldPanel panel,
        string methodName,
        DossierPreviewField field)
    {
        var method = typeof(DossierPreviewFieldPanel).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<UIElement>(method.Invoke(panel, [field]));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static DossierPreviewFieldPanel CreatePanel(
        DossierAreaSettings area,
        DossierDefinition dossier)
        => new(
            new StackPanel(),
            area,
            dossier,
            System.IO.Path.GetTempPath(),
            new DossierPreviewDocument([]),
            new PlanImageConverterStub(),
            new PlanImageAdjusterStub(),
            () => new Dictionary<string, string>(),
            () => { },
            _ => { },
            (_, _) => { },
            _ => Brushes.Black,
            _ => { },
            () => new Window());

    private static void RunOnSta(Action test)
    {
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        error?.Throw();
    }

    private sealed class PlanImageConverterStub : IPlanImageConverter
    {
        public bool NeedsConversion(string? path) => false;

        public Task<PlanImageResult> ConvertAsync(
            string sourcePath,
            string targetFolder,
            CancellationToken ct = default)
            => Task.FromResult(PlanImageResult.Failed("Im Test nicht verwendet."));
    }

    private sealed class PlanImageAdjusterStub : IPlanImageAdjuster
    {
        public PlanImageResult Rotate(string? imagePath, string targetFolder, int degrees)
            => PlanImageResult.Failed("Im Test nicht verwendet.");

        public PlanImageResult Crop(
            string? imagePath,
            string targetFolder,
            int x,
            int y,
            int width,
            int height)
            => PlanImageResult.Failed("Im Test nicht verwendet.");
    }
}
