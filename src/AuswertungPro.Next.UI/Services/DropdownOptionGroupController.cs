using System.Collections.ObjectModel;

namespace AuswertungPro.Next.UI.Services;

public sealed record DropdownOptionEditorResult(
    bool Accepted,
    IReadOnlyList<string> Items);

public sealed record DropdownOptionGroupSettings(
    string PreviewTitle,
    IReadOnlyList<string> ResetItems,
    bool LockedToResetItems = false);

public sealed record DropdownOptionGroupActions(
    Func<IReadOnlyList<string>, DropdownOptionEditorResult> EditOptions,
    Action<string, string> ShowInfo,
    Action Save);

public sealed class DropdownOptionGroupController
{
    private readonly ObservableCollection<string> _options;
    private readonly DropdownOptionGroupSettings _settings;
    private readonly DropdownOptionGroupActions _actions;

    public DropdownOptionGroupController(
        ObservableCollection<string> options,
        DropdownOptionGroupSettings settings,
        DropdownOptionGroupActions actions)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public void Edit()
    {
        var result = _actions.EditOptions(_options.ToArray());
        if (!result.Accepted)
            return;

        DropdownOptionList.ReplaceWith(_options, result.Items);
        ApplyLockedResetItems();
        _actions.Save();
    }

    public void Preview()
    {
        var items = string.Join("\n", _options);
        _actions.ShowInfo(items, _settings.PreviewTitle);
    }

    public void Reset()
    {
        DropdownOptionList.ReplaceWith(_options, _settings.ResetItems);
        _actions.Save();
    }

    public void Add(object? value)
    {
        if (_settings.LockedToResetItems)
        {
            ApplyLockedResetItems();
            _actions.Save();
            return;
        }

        if (DropdownOptionList.AddIfMissing(_options, DropdownOptionList.ExtractText(value)))
            _actions.Save();
    }

    public void Remove(object? value)
    {
        if (_settings.LockedToResetItems)
        {
            ApplyLockedResetItems();
            _actions.Save();
            return;
        }

        if (DropdownOptionList.Remove(_options, DropdownOptionList.ExtractText(value)))
            _actions.Save();
    }

    private void ApplyLockedResetItems()
    {
        if (_settings.LockedToResetItems)
            DropdownOptionList.EnsureExact(_options, _settings.ResetItems);
    }
}
