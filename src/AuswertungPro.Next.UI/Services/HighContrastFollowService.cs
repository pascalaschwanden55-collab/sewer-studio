using System;
using System.ComponentModel;
using System.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration): haengt die Hochkontrast-Ueberlagerung
/// (<c>Theme/ThemeHighContrast.xaml</c>) ein, solange <see cref="SystemParameters.HighContrast"/>
/// wahr ist, und reagiert auf einen Wechsel WAEHREND SewerStudio laeuft (Benutzer schaltet
/// Hochkontrast in den Windows-Erleichterte-Bedienung-Einstellungen um) - ohne Neustart.
///
/// <see cref="SystemParameters.StaticPropertyChanged"/> ist WPF-eigen und feuert bereits auf dem
/// UI-Thread (anders als <see cref="Microsoft.Win32.SystemEvents.UserPreferenceChanged"/>); kein
/// Dispatcher-Marshalling noetig.
/// </summary>
public sealed class HighContrastFollowService : IDisposable
{
    private readonly ResourceDictionary _rootResources;
    private readonly Func<bool> _readHighContrast;
    private bool _subscribed;
    private bool _disposed;

    public HighContrastFollowService(ResourceDictionary rootResources, Func<bool>? readHighContrast = null)
    {
        _rootResources = rootResources ?? throw new ArgumentNullException(nameof(rootResources));
        _readHighContrast = readHighContrast ?? (() => SystemParameters.HighContrast);
    }

    public void Start()
    {
        Apply();
        if (_subscribed || _disposed)
            return;

        SystemParameters.StaticPropertyChanged += OnStaticPropertyChanged;
        _subscribed = true;
    }

    private void OnStaticPropertyChanged(object? sender, PropertyChangedEventArgs e) => Apply();

    private void Apply() => ThemeManager.SetHighContrastOverlay(_rootResources, _readHighContrast());

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_subscribed)
        {
            SystemParameters.StaticPropertyChanged -= OnStaticPropertyChanged;
            _subscribed = false;
        }

        _disposed = true;
    }
}
