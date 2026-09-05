using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;

namespace AuswertungPro.Next.UI.Player;

/// <summary>Eine Zeile der Karte "KI-Vorschlaege" — reine Anzeige, kein Ereignis.</summary>
public sealed class CodingSuggestionRow : INotifyPropertyChanged
{
    private bool _isConfirmed;

    public CodingSuggestionRow(CodingSuggestion suggestion)
    {
        Suggestion = suggestion ?? throw new ArgumentNullException(nameof(suggestion));
        Text = CodingSuggestionText.Zeile(suggestion);
        Glyph = suggestion.Kind switch
        {
            CodingSuggestionKind.Bogen => "",       // Bogen: gebogener Pfeil
            CodingSuggestionKind.Rohranfang => "",  // Rohranfang: Pfeil nach rechts
            CodingSuggestionKind.Rohrende => "",    // Rohrende: Haken
            _ => ""
        };
    }

    public CodingSuggestion Suggestion { get; }
    public CodingSuggestionKind Kind => Suggestion.Kind;
    public string Text { get; }
    public string Glyph { get; }
    public double TimeSeconds => Suggestion.PeakTimeSeconds;

    public bool IsConfirmed
    {
        get => _isConfirmed;
        set { if (_isConfirmed == value) return; _isConfirmed = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
