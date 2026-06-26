using System;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private AppProtocol.IVsaCodeSelectionCatalog? CodeSelectionCatalog
        => _protocolContext.Dependencies.CodeSelectionCatalog;

    private AppProtocol.ICodeCatalogProvider? CodeCatalog
        => _protocolContext.Dependencies.CodeCatalog;

    private VsaCodeExplorerViewModel CreateVsaCodeExplorerViewModel(
        ProtocolEntry entry,
        double? presetMeter,
        TimeSpan? presetZeit)
        => new(entry, presetMeter, presetZeit, CodeSelectionCatalog);
}
