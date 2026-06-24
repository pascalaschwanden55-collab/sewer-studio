using System;
using System.ComponentModel;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingSessionViewModelOwner
{
    private readonly PropertyChangedEventHandler _propertyChangedHandler;
    private CodingSessionViewModel? _subscribedViewModel;

    public CodingSessionViewModelOwner(PropertyChangedEventHandler propertyChangedHandler)
    {
        ArgumentNullException.ThrowIfNull(propertyChangedHandler);
        _propertyChangedHandler = propertyChangedHandler;
    }

    public CodingSessionViewModel? ViewModel { get; private set; }

    public bool HasViewModel => ViewModel is not null;

    public void Set(CodingSessionViewModel viewModel, bool observePropertyChanged)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DetachPropertyChanged();
        ViewModel = viewModel;

        if (!observePropertyChanged)
            return;

        viewModel.PropertyChanged += _propertyChangedHandler;
        _subscribedViewModel = viewModel;
    }

    public void DetachPropertyChanged()
    {
        if (_subscribedViewModel is null)
            return;

        _subscribedViewModel.PropertyChanged -= _propertyChangedHandler;
        _subscribedViewModel = null;
    }

    public void Clear()
    {
        DetachPropertyChanged();
        ViewModel = null;
    }
}
