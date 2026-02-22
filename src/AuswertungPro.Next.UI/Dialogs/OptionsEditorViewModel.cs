using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Dialogs
{
    public class OptionsEditorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<string> Items { get; }
        public string? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem == value) return;
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                _removeCommand.NotifyCanExecuteChanged();
                _moveUpCommand.NotifyCanExecuteChanged();
                _moveDownCommand.NotifyCanExecuteChanged();
            }
        }

        public string? NewItemText
        {
            get => _newItemText;
            set
            {
                if (_newItemText == value) return;
                _newItemText = value;
                OnPropertyChanged(nameof(NewItemText));
            }
        }

        public bool IsReadOnly { get; }
        public bool IsEditingEnabled => !IsReadOnly;

        public ICommand AddCommand => _addCommand;
        public ICommand RemoveCommand => _removeCommand;
        public ICommand MoveUpCommand => _moveUpCommand;
        public ICommand MoveDownCommand => _moveDownCommand;
        public ICommand SaveCommand => _saveCommand;
        public ICommand CancelCommand => _cancelCommand;
        public event PropertyChangedEventHandler? PropertyChanged;
        public bool? DialogResult
        {
            get => _dialogResult;
            set
            {
                if (_dialogResult == value) return;
                _dialogResult = value;
                OnPropertyChanged(nameof(DialogResult));
            }
        }

        private readonly RelayCommand _addCommand;
        private readonly RelayCommand _removeCommand;
        private readonly RelayCommand _moveUpCommand;
        private readonly RelayCommand _moveDownCommand;
        private readonly RelayCommand _saveCommand;
        private readonly RelayCommand _cancelCommand;
        private string? _selectedItem;
        private string? _newItemText;
        private bool? _dialogResult;

        public OptionsEditorViewModel(IEnumerable<string> items, bool isReadOnly = false)
        {
            IsReadOnly = isReadOnly;
            Items = new ObservableCollection<string>(NormalizeItems(items));
            _addCommand = new RelayCommand(Add, () => IsEditingEnabled);
            _removeCommand = new RelayCommand(Remove, () => IsEditingEnabled && SelectedItem != null);
            _moveUpCommand = new RelayCommand(MoveUp, () => IsEditingEnabled && CanMoveUp);
            _moveDownCommand = new RelayCommand(MoveDown, () => IsEditingEnabled && CanMoveDown);
            _saveCommand = new RelayCommand(Save, () => IsEditingEnabled);
            _cancelCommand = new RelayCommand(Cancel);
        }

        private void Add()
        {
            var text = (NewItemText ?? "").Trim();
            if (string.IsNullOrEmpty(text))
                return;
            if (Items.Any(x => x.Equals(text, System.StringComparison.OrdinalIgnoreCase)))
                return;
            Items.Add(text);
            NewItemText = string.Empty;
        }
        private void Remove()
        {
            if (SelectedItem != null)
                Items.Remove(SelectedItem);
        }
        private void MoveUp()
        {
            var idx = Items.IndexOf(SelectedItem!);
            if (idx > 0)
            {
                Items.Move(idx, idx - 1);
            }
        }
        private void MoveDown()
        {
            var idx = Items.IndexOf(SelectedItem!);
            if (idx >= 0 && idx < Items.Count - 1)
            {
                Items.Move(idx, idx + 1);
            }
        }
        private void Save()
        {
            if (!ValidateItems())
                return;
            DialogResult = true;
        }
        private void Cancel() => DialogResult = false;
        public bool CanMoveUp => SelectedItem != null && Items.IndexOf(SelectedItem) > 0;
        public bool CanMoveDown => SelectedItem != null && Items.IndexOf(SelectedItem) < Items.Count - 1;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private static IEnumerable<string> NormalizeItems(IEnumerable<string> items)
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                var value = (item ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(value))
                    continue;
                if (seen.Add(value))
                    yield return value;
            }
        }

        private bool ValidateItems()
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var item in Items)
            {
                var value = (item ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(value))
                {
                    MessageBox.Show("Leere Eintraege sind nicht erlaubt.", "Ungueltige Liste",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                if (!seen.Add(value))
                {
                    MessageBox.Show("Doppelte Eintraege sind nicht erlaubt.", "Ungueltige Liste",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }
    }

    // (Lokale RelayCommand-Implementierung entfernt, es wird CommunityToolkit.Mvvm.Input.RelayCommand verwendet)
}
