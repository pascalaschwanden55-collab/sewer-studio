using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.ViewModels.Protocol
{
    public class ProtocolEntryEditorKiViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ProtocolEntryEditorViewModel Editor { get; }
        public AiSuggestion? KiSuggestion { get; private set; }
        public bool IsKiLoading { get; private set; }
        public string KiStatus { get; private set; } = "";

        private readonly IProtocolAiService _aiService;

        public ProtocolEntryEditorKiViewModel(ProtocolEntryEditorViewModel editor, IProtocolAiService aiService)
        {
            Editor = editor;
            _aiService = aiService;
        }

        public async Task GetKiSuggestionAsync()
        {
            IsKiLoading = true;
            KiStatus = "KI-Vorschlag wird geladen...";
            OnPropertyChanged(nameof(IsKiLoading));
            OnPropertyChanged(nameof(KiStatus));
            try
            {
                var input = new AiInput(
                    ProjectFolderAbs: string.Empty,
                    HaltungId: null,
                    Meter: null,
                    ExistingCode: null,
                    ExistingText: null,
                    AllowedCodes: Editor.AllowedCodes
                );
                KiSuggestion = await _aiService.SuggestAsync(input);
                KiStatus = KiSuggestion != null ? $"Vorschlag: {KiSuggestion.SuggestedCode} ({KiSuggestion.Confidence:P0})" : "Kein Vorschlag";
            }
            catch (Exception ex)
            {
                KiStatus = $"KI-Fehler: {ex.Message}";
            }
            finally
            {
                IsKiLoading = false;
                OnPropertyChanged(nameof(IsKiLoading));
                OnPropertyChanged(nameof(KiSuggestion));
                OnPropertyChanged(nameof(KiStatus));
            }
        }
    }
}
