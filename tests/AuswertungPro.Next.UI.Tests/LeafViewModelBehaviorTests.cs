using System.IO;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models.Costs;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LeafViewModelBehaviorTests
{
    [Fact]
    public void Measure_selection_maps_templates_and_keeps_each_selection_independent()
    {
        var templates = new MeasureTemplates
        {
            Templates =
            [
                new MeasureTemplate { Id = "m1", Name = "Inliner", Description = "Rohr auskleiden" },
                new MeasureTemplate { Id = "m2", Name = "Roboter", Description = "Schaden fraesen" }
            ]
        };

        var viewModel = new MeasureSelectionViewModel(templates);

        Assert.Collection(
            viewModel.Rows,
            row =>
            {
                Assert.Equal("m1", row.Id);
                Assert.Equal("Inliner", row.Name);
                Assert.Equal("Rohr auskleiden", row.Description);
                Assert.False(row.IsSelected);
            },
            row =>
            {
                Assert.Equal("m2", row.Id);
                Assert.False(row.IsSelected);
            });

        viewModel.Rows[0].IsSelected = true;

        Assert.True(viewModel.Rows[0].IsSelected);
        Assert.False(viewModel.Rows[1].IsSelected);
    }

    [Fact]
    public void Code_parameter_reports_required_number_errors_and_recovers_after_valid_input()
    {
        var viewModel = new CodeParameterViewModel(new CodeParameter
        {
            Name = "Ausmass",
            Type = "number",
            Unit = "mm",
            Required = true
        });

        Assert.False(viewModel.IsValid);
        Assert.Equal("Pflichtfeld.", viewModel.ErrorMessage);
        Assert.Equal("Ausmass *", viewModel.DisplayName);
        Assert.Equal("mm", viewModel.UnitSuffix);

        viewModel.Value = "abc";
        Assert.False(viewModel.IsValid);
        Assert.Equal("Numerischer Wert erwartet.", viewModel.ErrorMessage);

        viewModel.Value = "12,5";
        Assert.True(viewModel.IsValid);
        Assert.Empty(viewModel.ErrorMessage);
    }

    [Fact]
    public void Media_conflict_candidate_exposes_file_and_parent_folder()
    {
        var fullPath = Path.Combine("C:\\", "Videoquelle", "80454.mp4");

        var viewModel = new MediaConflictCandidateViewModel(fullPath);

        Assert.Equal(fullPath, viewModel.FullPath);
        Assert.Equal("80454.mp4", viewModel.FileName);
        Assert.Equal(Path.Combine("C:\\", "Videoquelle"), viewModel.DirectoryName);
    }

    [Fact]
    public void Catalog_column_sorts_items_by_visible_label()
    {
        var zNode = new CatalogTreeNode("ZZ", "Zulu");
        var aNode = new CatalogTreeNode("AA", "Alpha");

        var viewModel = new CatalogColumnViewModel(
            2,
            [CatalogItem.FromNode(zNode), CatalogItem.FromNode(aNode)]);

        Assert.Equal(2, viewModel.Index);
        Assert.Equal(["Alpha", "Zulu"], viewModel.Items.Select(item => item.Label));
    }

    [Fact]
    public void Parameter_value_rejects_unknown_enum_and_accepts_catalog_value()
    {
        var viewModel = new ParameterValueViewModel(
            new CodeParameter
            {
                Name = "Material",
                Type = "enum",
                Required = true,
                AllowedValues = ["Beton", "Steinzeug"]
            },
            existingValue: "Holz");

        Assert.False(viewModel.Validate(out var invalidError));
        Assert.Contains("Material", invalidError, StringComparison.Ordinal);
        Assert.Contains("ungueltig", invalidError, StringComparison.OrdinalIgnoreCase);

        viewModel.Value = "Steinzeug";

        Assert.True(viewModel.Validate(out var validError));
        Assert.Empty(validError);
    }
}
