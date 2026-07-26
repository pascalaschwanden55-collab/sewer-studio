using System.Windows;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Rein lesendes Fotoalbum der persoenlich bestaetigten Goldbeispiele.</summary>
public partial class PersonalGoldAlbumWindow : Window
{
    private readonly PersonalGoldAlbumViewModel _viewModel;

    public PersonalGoldAlbumWindow(
        IPersonalGoldAlbumService albumService,
        string confirmedByUser)
    {
        InitializeComponent();
        WindowStateManager.Track(this);
        _viewModel = new PersonalGoldAlbumViewModel(albumService, confirmedByUser);
        DataContext = _viewModel;
        Loaded += PersonalGoldAlbumWindow_Loaded;
    }

    private async void PersonalGoldAlbumWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PersonalGoldAlbumWindow_Loaded;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
