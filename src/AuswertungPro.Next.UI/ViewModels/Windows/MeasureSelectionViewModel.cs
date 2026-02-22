using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using AuswertungPro.Next.Domain.Models.Costs;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public sealed partial class MeasureSelectionViewModel : ObservableObject
{
    public ObservableCollection<MeasureSelectionRow> Rows { get; } = new();
    private readonly MeasureTemplates _templates;

    public MeasureSelectionViewModel(MeasureTemplates templates)
    {
        _templates = templates;
        foreach (var t in templates.Templates)
        {
            Rows.Add(new MeasureSelectionRow(t)
            {
                IsSelected = false
            });
        }
    }
}

public sealed partial class MeasureSelectionRow : ObservableObject
{
    public MeasureTemplate Template { get; }

    [ObservableProperty] private bool _isSelected;

    public string Id => Template.Id;
    public string Name => Template.Name;
    public string Description => Template.Description;

    public MeasureSelectionRow(MeasureTemplate template)
    {
        Template = template;
    }
}
