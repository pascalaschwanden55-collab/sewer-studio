namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Hält fest, ob das sichtbare Blatt wirklich zum neuesten Dossierstand und
/// zur aktuell gewählten Seite gehört. Die Klasse kennt keine WPF-Elemente;
/// das Fenster setzt nur noch den hier geprüften Zustand sichtbar um.
/// </summary>
internal sealed class DossierExactPreviewState
{
    private int _requestedOutputVersion;
    private int _appliedOutputVersion;
    private int _pageRenderVersion;
    private bool _hasCurrentOutput;
    private bool _hasCurrentPage;

    public int RequestedOutputVersion => _requestedOutputVersion;

    public bool NeedsOutputRefresh
        => _appliedOutputVersion != _requestedOutputVersion;

    private bool HasCurrentOutput
        => _hasCurrentOutput && !NeedsOutputRefresh;

    public bool CanAccept
        => HasCurrentOutput && _hasCurrentPage;

    public bool CanInteractWithPage
        => CanAccept;

    public int RequestOutputRefresh()
    {
        _requestedOutputVersion++;
        _pageRenderVersion++;
        _hasCurrentOutput = false;
        _hasCurrentPage = false;
        return _requestedOutputVersion;
    }

    public bool TryCompleteOutput(int version, bool success)
    {
        if (version != _requestedOutputVersion)
            return false;

        _appliedOutputVersion = version;
        _pageRenderVersion++;
        _hasCurrentOutput = success;
        _hasCurrentPage = false;
        return true;
    }

    public int BeginPageRender()
    {
        _pageRenderVersion++;
        _hasCurrentPage = false;
        return _pageRenderVersion;
    }

    public bool IsCurrentPageRender(int version)
        => version == _pageRenderVersion && HasCurrentOutput;

    public bool TryCompletePage(int version, bool success)
    {
        if (version != _pageRenderVersion || !HasCurrentOutput)
            return false;

        _hasCurrentPage = success;
        return true;
    }
}
